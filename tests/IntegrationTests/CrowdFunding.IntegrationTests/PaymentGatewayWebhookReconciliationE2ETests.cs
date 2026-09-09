using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Contracts.Contributions;
using CrowdFunding.API.Contracts.Moderation;
using CrowdFunding.API.Migrations;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// TICKET-033: proves idempotent webhook redelivery, cryptographic signature enforcement, and
/// out-of-order safety against real HTTP + Postgres — the three acceptance criteria that matter,
/// not just the domain-level state machine.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class PaymentGatewayWebhookReconciliationE2ETests
{
    // Matches appsettings.json's PaymentGatewayWebhook:Secret — the test host reads the same
    // configuration file (only ConnectionStrings/Jwt/OpenMeter/RateLimiting are overridden by
    // CrowdFundingApiFactory via environment variables).
    private const string WebhookSecret = "whsec_dev_local_only_change_in_production";

    private readonly CrowdFundingApiFactory _factory;

    public PaymentGatewayWebhookReconciliationE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    private static string SignHeader(string payload, DateTimeOffset signedAt)
    {
        var timestamp = signedAt.ToUnixTimeSeconds();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(WebhookSecret));
        var hash = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}")));
        return $"t={timestamp},v1={hash}";
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(HttpClient client, string eventId, string paymentIntentId, string eventType, bool signed = true)
    {
        var payload = JsonSerializer.Serialize(new
        {
            id = eventId,
            type = eventType,
            data = new { @object = new { id = paymentIntentId } },
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/payments/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        if (signed)
        {
            request.Headers.Add("Stripe-Signature", SignHeader(payload, DateTimeOffset.UtcNow));
        }

        return await client.SendAsync(request);
    }

    private async Task<(Guid CampaignId, Guid ContributionId, string PaymentIntentId, string CreatorToken, string BackerToken)>
        CreatePendingContributionAsync(HttpClient client, string label)
    {
        var (_, creatorToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail($"{label}-creator"));
        client.SetBearerToken(creatorToken);

        var createResponse = await client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest(
            "Payment Webhook Test Campaign",
            "A story that is definitely longer than twenty characters to satisfy validation rules.",
            "Community",
            10_000m,
            "USD",
            DateTime.UtcNow.AddDays(30)));
        createResponse.EnsureSuccessStatusCode();
        var campaignId = (await createResponse.Content.ReadFromJsonAsync<CreateCampaignResponse>())!.CampaignId;

        await _factory.ProcessOutboxMessagesAsync();

        var adminEmail = ApiTestExtensions.UniqueEmail($"{label}-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Webhook Test Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);
        client.SetBearerToken(adminToken);
        var approveResponse = await client.PostAsJsonAsync($"/api/moderation/reviews/{campaignId}/approve", new ReviewCampaignRequest(Notes: null));
        approveResponse.EnsureSuccessStatusCode();

        client.SetBearerToken(creatorToken);
        var publishResponse = await client.PostAsync($"/api/campaigns/{campaignId}/publish", content: null);
        publishResponse.EnsureSuccessStatusCode();
        await _factory.ProcessOutboxMessagesAsync();

        var (_, backerToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail($"{label}-backer"));
        client.SetBearerToken(backerToken);
        var contributeResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/contributions", new MakeContributionRequest(100m, "USD"));
        contributeResponse.EnsureSuccessStatusCode();
        var contributionId = (await contributeResponse.Content.ReadFromJsonAsync<MakeContributionResponse>())!.ContributionId;

        var getResponse = await client.GetAsync($"/api/campaigns/{campaignId}/contributions/{contributionId}");
        getResponse.EnsureSuccessStatusCode();
        var contribution = await getResponse.Content.ReadFromJsonAsync<GetContributionByIdResponse>();

        return (campaignId, contributionId, contribution!.ExternalPaymentIntentId!, creatorToken, backerToken);
    }

    [Fact]
    public async Task ValidWebhook_ShouldConfirmThePendingContribution()
    {
        using var client = _factory.CreateClient();
        var (campaignId, contributionId, paymentIntentId, _, backerToken) = await CreatePendingContributionAsync(client, "confirm");

        var response = await PostWebhookAsync(client, $"evt_{Guid.NewGuid():N}", paymentIntentId, "payment_intent.succeeded");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        client.SetBearerToken(backerToken);
        var getResponse = await client.GetAsync($"/api/campaigns/{campaignId}/contributions/{contributionId}");
        var contribution = await getResponse.Content.ReadFromJsonAsync<GetContributionByIdResponse>();
        Assert.Equal("Succeeded", contribution!.Status);
    }

    [Fact]
    public async Task RedeliveredWebhook_WithTheSameEventId_ShouldBeIgnoredNotReapplied()
    {
        using var client = _factory.CreateClient();
        var (campaignId, contributionId, paymentIntentId, _, backerToken) = await CreatePendingContributionAsync(client, "idempotent");
        var eventId = $"evt_{Guid.NewGuid():N}";

        var first = await PostWebhookAsync(client, eventId, paymentIntentId, "payment_intent.succeeded");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Stripe retries webhooks for up to 72 hours on any non-2xx response, and can redeliver
        // under normal operation too — this must not throw or double-process.
        var second = await PostWebhookAsync(client, eventId, paymentIntentId, "payment_intent.succeeded");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        client.SetBearerToken(backerToken);
        var getResponse = await client.GetAsync($"/api/campaigns/{campaignId}/contributions/{contributionId}");
        var contribution = await getResponse.Content.ReadFromJsonAsync<GetContributionByIdResponse>();
        Assert.Equal("Succeeded", contribution!.Status);
    }

    [Fact]
    public async Task WebhookWithoutASignature_ShouldBeRejected()
    {
        using var client = _factory.CreateClient();
        var (_, _, paymentIntentId, _, _) = await CreatePendingContributionAsync(client, "unsigned");

        var response = await PostWebhookAsync(client, $"evt_{Guid.NewGuid():N}", paymentIntentId, "payment_intent.succeeded", signed: false);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WebhookWithAnInvalidSignature_ShouldBeRejected()
    {
        using var client = _factory.CreateClient();
        var (_, _, paymentIntentId, _, _) = await CreatePendingContributionAsync(client, "tampered");

        var payload = JsonSerializer.Serialize(new
        {
            id = $"evt_{Guid.NewGuid():N}",
            type = "payment_intent.succeeded",
            data = new { @object = new { id = paymentIntentId } },
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/payments/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Stripe-Signature", "t=1700000000,v1=0000000000000000000000000000000000000000000000000000000000000000");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ClientConfirmCall_AfterWebhookAlreadyConfirmedIt_ShouldSucceedGracefully()
    {
        using var client = _factory.CreateClient();
        var (campaignId, contributionId, paymentIntentId, _, _) = await CreatePendingContributionAsync(client, "out-of-order");

        // The webhook arrives first (e.g. before the customer's browser redirects back).
        var webhookResponse = await PostWebhookAsync(client, $"evt_{Guid.NewGuid():N}", paymentIntentId, "payment_intent.succeeded");
        webhookResponse.EnsureSuccessStatusCode();

        var adminEmail = ApiTestExtensions.UniqueEmail("out-of-order-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Out Of Order Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);
        client.SetBearerToken(adminToken);

        // The client's own /confirm-payment call arrives after — must not throw
        // "Only pending contributions can be confirmed."
        var confirmResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/contributions/{contributionId}/confirm-payment",
            new ConfirmContributionPaymentRequest("client-side-confirmation"));

        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var result = await confirmResponse.Content.ReadFromJsonAsync<ConfirmContributionPaymentResponse>();
        Assert.Equal("Succeeded", result!.Status);
    }

    [Fact]
    public async Task WebhookArrivingAfterCampaignCancellation_ShouldConfirmThenImmediatelyRefund()
    {
        using var client = _factory.CreateClient();
        var (campaignId, contributionId, paymentIntentId, creatorToken, backerToken) =
            await CreatePendingContributionAsync(client, "midflight-cancel");

        // The campaign is cancelled while the payment is still "in flight" at the gateway (the
        // contribution is still Pending here — no payment has been confirmed yet).
        client.SetBearerToken(creatorToken);
        var cancelResponse = await client.PostAsync($"/api/campaigns/{campaignId}/cancel", content: null);
        cancelResponse.EnsureSuccessStatusCode();
        await _factory.ProcessOutboxMessagesAsync();

        // The gateway's success webhook arrives after the cancellation already went through.
        var webhookResponse = await PostWebhookAsync(client, $"evt_{Guid.NewGuid():N}", paymentIntentId, "payment_intent.succeeded");
        webhookResponse.EnsureSuccessStatusCode();

        client.SetBearerToken(backerToken);
        var getResponse = await client.GetAsync($"/api/campaigns/{campaignId}/contributions/{contributionId}");
        var contribution = await getResponse.Content.ReadFromJsonAsync<GetContributionByIdResponse>();

        // Never left stuck as "Succeeded" against a campaign that no longer exists to fulfil it —
        // charged and immediately refunded in the same reconciliation.
        Assert.Equal("Refunded", contribution!.Status);
    }
}
