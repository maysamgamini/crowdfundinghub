using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using CrowdFunding.API.Contracts.CampaignUpdates;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.Modules.CampaignUpdates.Domain.Aggregates;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// TICKET-035: registration-time SSRF rejection and ownership enforcement against real HTTP +
/// Postgres, plus an end-to-end delivery proving the outbound HMAC signature the dispatcher
/// produces is exactly what a real receiving server would verify.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class WebhookSubscriptionE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public WebhookSubscriptionE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(Guid CampaignId, string CreatorToken)> CreateCampaignAsync(HttpClient client, string label)
    {
        var (_, creatorToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail($"{label}-creator"));
        client.SetBearerToken(creatorToken);

        var createResponse = await client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest(
            "Webhook Subscription Test Campaign",
            "A story that is definitely longer than twenty characters to satisfy validation rules.",
            "Community",
            5000m,
            "USD",
            DateTime.UtcNow.AddDays(30)));
        createResponse.EnsureSuccessStatusCode();
        var campaignId = (await createResponse.Content.ReadFromJsonAsync<CreateCampaignResponse>())!.CampaignId;

        await _factory.ProcessOutboxMessagesAsync();

        return (campaignId, creatorToken);
    }

    [Fact]
    public async Task RegisteringAPrivateNetworkTarget_ShouldBeRejected()
    {
        using var client = _factory.CreateClient();
        var (campaignId, creatorToken) = await CreateCampaignAsync(client, "ssrf");
        client.SetBearerToken(creatorToken);

        var response = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/webhook-subscriptions",
            new RegisterWebhookSubscriptionRequest("https://169.254.169.254/latest/meta-data/"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisteringAPublicHttpsTarget_ShouldSucceed_AndReturnTheSecretOnlyOnce()
    {
        using var client = _factory.CreateClient();
        var (campaignId, creatorToken) = await CreateCampaignAsync(client, "register-ok");
        client.SetBearerToken(creatorToken);

        var response = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/webhook-subscriptions",
            new RegisterWebhookSubscriptionRequest("https://example.com/webhooks/pledges"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RegisterWebhookSubscriptionResponse>();
        Assert.Equal(campaignId, body!.CampaignId);
        Assert.False(string.IsNullOrWhiteSpace(body.SecretKey));
    }

    [Fact]
    public async Task RegisteringOnSomeoneElsesCampaign_ShouldBeForbidden()
    {
        using var client = _factory.CreateClient();
        var (campaignId, _) = await CreateCampaignAsync(client, "not-owner");

        var (_, otherUserToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("not-owner-attacker"));
        client.SetBearerToken(otherUserToken);

        var response = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/webhook-subscriptions",
            new RegisterWebhookSubscriptionRequest("https://example.com/webhooks/pledges"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DispatchingADueDelivery_ShouldPostASignedRequest_ThatARealReceiverCanVerify()
    {
        // A stand-in for the creator's own server — asserts the signature the dispatcher computed
        // is exactly what an independent verifier (this receiver, not the dispatcher's own code)
        // recomputes over the exact bytes it received.
        var receivedSignature = string.Empty;
        var receivedBody = string.Empty;
        var received = new TaskCompletionSource();

        var receiverApp = WebApplication.CreateBuilder().Build();
        receiverApp.MapPost("/webhooks/pledges", async (HttpContext context) =>
        {
            receivedSignature = context.Request.Headers["X-CrowdFunding-Signature"].ToString();
            using var reader = new StreamReader(context.Request.Body);
            receivedBody = await reader.ReadToEndAsync();
            received.TrySetResult();
            return Results.Ok();
        });
        receiverApp.Urls.Add("http://127.0.0.1:0");
        await receiverApp.StartAsync();
        var receiverUrl = receiverApp.Urls.First();

        try
        {
            const string secretKey = "test-secret-for-dispatch-verification";
            const string payloadJson = """{"eventType":"pledge.confirmed","amount":100}""";

            await using var scope = _factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CampaignUpdatesDbContext>();

            // Registered directly against the DB rather than through the HTTP registration
            // endpoint: the endpoint's own SSRF guard would (correctly) refuse this loopback
            // target, since dispatch itself deliberately does not re-validate reachability —
            // see UrlSecurityValidator's remarks on why that check lives only at registration.
            var subscription = WebhookSubscription.Create(Guid.NewGuid(), $"{receiverUrl}/webhooks/pledges", secretKey, DateTime.UtcNow);
            dbContext.WebhookSubscriptions.Add(subscription);

            var task = WebhookDeliveryTask.Create(subscription.Id, "pledge.confirmed", payloadJson, DateTime.UtcNow);
            dbContext.WebhookDeliveryTasks.Add(task);
            await dbContext.SaveChangesAsync();

            var dispatcher = _factory.Services.GetRequiredService<WebhookDispatcherBackgroundService>();
            await dispatcher.ProcessBatchAsync(CancellationToken.None);

            await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(payloadJson, receivedBody);
            Assert.Matches(@"^t=\d+,v1=[0-9a-f]{64}$", receivedSignature);

            var parts = receivedSignature.Split(',');
            var timestamp = parts[0]["t=".Length..];
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            var expectedHash = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payloadJson}")));
            Assert.Equal($"t={timestamp},v1={expectedHash}", receivedSignature);

            await using var verificationScope = _factory.Services.CreateAsyncScope();
            var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<CampaignUpdatesDbContext>();
            var deliveredTask = await verificationDbContext.WebhookDeliveryTasks.AsNoTracking().FirstAsync(x => x.Id == task.Id);
            Assert.Equal(WebhookDeliveryStatus.Delivered, deliveredTask.Status);
        }
        finally
        {
            await receiverApp.StopAsync();
        }
    }
}
