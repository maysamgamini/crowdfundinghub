using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Contracts.Contributions;
using CrowdFunding.API.Contracts.Moderation;
using CrowdFunding.API.Migrations;
using CrowdFunding.Modules.Notifications.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// TICKET-031: proves the email notification consumer is driven purely by the outbox — no
/// in-memory coupling from a command handler straight into <c>IEmailNotificationService</c>.
/// Confirming a contribution's payment (in Contributions) or cancelling a campaign (fanning out
/// through the TICKET-027 refund saga) commits a database write and an outbox row in the same
/// transaction; the actual "send" only happens once <see cref="CrowdFundingApiFactory.ProcessOutboxMessagesAsync"/>
/// claims and publishes that row, which is what the assertions below are actually exercising.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class EmailNotificationOutboxE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public EmailNotificationOutboxE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    private IEmailNotificationSink Sink => _factory.Services.GetRequiredService<IEmailNotificationSink>();

    private async Task<(Guid CampaignId, string CreatorToken, string AdminToken)> CreatePublishedCampaignAsync(HttpClient client, string label)
    {
        var (_, creatorToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail($"{label}-creator"));
        client.SetBearerToken(creatorToken);

        var createResponse = await client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest(
            "Email Notification Test Campaign",
            "A story that is definitely longer than twenty characters to satisfy validation rules.",
            "Community",
            10_000m,
            "USD",
            DateTime.UtcNow.AddDays(30)));
        createResponse.EnsureSuccessStatusCode();
        var campaignId = (await createResponse.Content.ReadFromJsonAsync<CreateCampaignResponse>())!.CampaignId;

        await _factory.ProcessOutboxMessagesAsync();

        var adminEmail = ApiTestExtensions.UniqueEmail($"{label}-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Email Notification Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);
        client.SetBearerToken(adminToken);
        var approveResponse = await client.PostAsJsonAsync($"/api/moderation/reviews/{campaignId}/approve", new ReviewCampaignRequest(Notes: null));
        approveResponse.EnsureSuccessStatusCode();

        client.SetBearerToken(creatorToken);
        var publishResponse = await client.PostAsync($"/api/campaigns/{campaignId}/publish", content: null);
        publishResponse.EnsureSuccessStatusCode();
        await _factory.ProcessOutboxMessagesAsync();

        return (campaignId, creatorToken, adminToken);
    }

    [Fact]
    public async Task ConfirmingContributionPayment_ShouldDeliverReceiptEmail_ThroughTheOutbox()
    {
        using var client = _factory.CreateClient();
        var (campaignId, _, adminToken) = await CreatePublishedCampaignAsync(client, "receipt");

        var (backerId, backerToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("receipt-backer"));
        client.SetBearerToken(backerToken);

        var contributeResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/contributions", new MakeContributionRequest(150m, "USD"));
        contributeResponse.EnsureSuccessStatusCode();
        var contributionId = (await contributeResponse.Content.ReadFromJsonAsync<MakeContributionResponse>())!.ContributionId;

        client.SetBearerToken(adminToken);
        var confirmResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/contributions/{contributionId}/confirm-payment",
            new ConfirmContributionPaymentRequest("test-payment-ref-receipt"));
        confirmResponse.EnsureSuccessStatusCode();

        // Nothing has been "sent" yet — the receipt only exists as a row in Contributions'
        // outbox table until a worker claims and publishes it.
        await _factory.ProcessOutboxMessagesAsync();

        var receipt = Sink.SentMessages.FirstOrDefault(m => m.IdempotencyKey == $"contribution-{contributionId}");
        Assert.NotNull(receipt);
        Assert.Equal("ContributionReceipt", receipt!.Kind);
        Assert.Equal(backerId, receipt.RecipientUserId);
        Assert.Equal(campaignId, receipt.CampaignId);
        Assert.Equal(150m, receipt.Amount);
        Assert.Equal("USD", receipt.Currency);
    }

    [Fact]
    public async Task CancellingACampaign_ShouldDeliverCancellationAlert_ToEveryRefundedBacker_ThroughTheOutbox()
    {
        using var client = _factory.CreateClient();
        var (campaignId, creatorToken, adminToken) = await CreatePublishedCampaignAsync(client, "cancel-alert");

        var (backerId, backerToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("cancel-alert-backer"));
        client.SetBearerToken(backerToken);

        var contributeResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/contributions", new MakeContributionRequest(200m, "USD"));
        contributeResponse.EnsureSuccessStatusCode();
        var contributionId = (await contributeResponse.Content.ReadFromJsonAsync<MakeContributionResponse>())!.ContributionId;

        client.SetBearerToken(adminToken);
        var confirmResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/contributions/{contributionId}/confirm-payment",
            new ConfirmContributionPaymentRequest("test-payment-ref-cancel-alert"));
        confirmResponse.EnsureSuccessStatusCode();
        await _factory.ProcessOutboxMessagesAsync();

        client.SetBearerToken(creatorToken);
        var cancelResponse = await client.PostAsync($"/api/campaigns/{campaignId}/cancel", content: null);
        cancelResponse.EnsureSuccessStatusCode();

        // Drives Campaigns' CampaignCancelledApplicationEvent -> Contributions' refund saga
        // (TICKET-027) -> ContributionRefundedApplicationEvent -> Notifications' cancellation
        // alert, each hop only happening because the previous one's outbox row was processed.
        await _factory.ProcessOutboxMessagesAsync();
        await _factory.ProcessOutboxMessagesAsync();

        var alert = Sink.SentMessages.FirstOrDefault(m => m.IdempotencyKey == $"cancellation-{contributionId}");
        Assert.NotNull(alert);
        Assert.Equal("CampaignCancellationAlert", alert!.Kind);
        Assert.Equal(backerId, alert.RecipientUserId);
        Assert.Equal(campaignId, alert.CampaignId);
        Assert.Equal(200m, alert.Amount);
    }
}
