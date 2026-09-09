using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Contracts.Contributions;
using CrowdFunding.API.Contracts.Moderation;
using CrowdFunding.API.Migrations;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// TICKET-027's end-to-end proof: a campaign with several confirmed backers is cancelled, and
/// every one of them is refunded purely through event choreography — Campaigns never opens a
/// transaction against Contributions' database to do this.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class RefundSagaE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public RefundSagaE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CancellingACampaign_ShouldRefundEveryConfirmedBacker()
    {
        using var client = _factory.CreateClient();

        var (_, creatorToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("refund-saga-creator"), "Refund Saga Creator");
        client.SetBearerToken(creatorToken);

        var createResponse = await client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest(
            "Refund Saga Test Campaign",
            "A story that is definitely longer than twenty characters to satisfy validation rules.",
            "Community",
            10_000m,
            "USD",
            DateTime.UtcNow.AddDays(30)));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateCampaignResponse>();
        var campaignId = created!.CampaignId;

        await _factory.ProcessOutboxMessagesAsync();

        var adminEmail = ApiTestExtensions.UniqueEmail("refund-saga-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Refund Saga Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);
        client.SetBearerToken(adminToken);
        var approveResponse = await client.PostAsJsonAsync(
            $"/api/moderation/reviews/{campaignId}/approve", new ReviewCampaignRequest(Notes: null));
        approveResponse.EnsureSuccessStatusCode();

        client.SetBearerToken(creatorToken);
        var publishResponse = await client.PostAsync($"/api/campaigns/{campaignId}/publish", content: null);
        publishResponse.EnsureSuccessStatusCode();
        await _factory.ProcessOutboxMessagesAsync();

        // Three backers pledge and have their payment confirmed.
        var contributionIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var backerEmail = ApiTestExtensions.UniqueEmail($"refund-saga-backer-{i}");
            var (_, backerToken) = await client.RegisterAndLoginAsync(backerEmail, $"Refund Saga Backer {i}");
            client.SetBearerToken(backerToken);

            var contributeResponse = await client.PostAsJsonAsync(
                $"/api/campaigns/{campaignId}/contributions", new MakeContributionRequest(100m, "USD"));
            contributeResponse.EnsureSuccessStatusCode();
            var contribution = await contributeResponse.Content.ReadFromJsonAsync<MakeContributionResponse>();
            contributionIds.Add(contribution!.ContributionId);

            client.SetBearerToken(adminToken);
            var confirmResponse = await client.PostAsJsonAsync(
                $"/api/campaigns/{campaignId}/contributions/{contribution.ContributionId}/confirm-payment",
                new ConfirmContributionPaymentRequest($"test-payment-ref-{i}"));
            confirmResponse.EnsureSuccessStatusCode();
        }

        // Crediting Campaign.RaisedAmount from each confirmed payment.
        await _factory.ProcessOutboxMessagesAsync();

        client.SetBearerToken(creatorToken);
        var cancelResponse = await client.PostAsync($"/api/campaigns/{campaignId}/cancel", content: null);
        cancelResponse.EnsureSuccessStatusCode();

        // Propagates CampaignCancelledApplicationEvent to Contributions' CampaignTerminationRefundHandler.
        await _factory.ProcessOutboxMessagesAsync();

        foreach (var contributionId in contributionIds)
        {
            var contributionResponse = await client.GetAsync($"/api/campaigns/{campaignId}/contributions/{contributionId}");
            contributionResponse.EnsureSuccessStatusCode();
            var contribution = await contributionResponse.Content.ReadFromJsonAsync<GetContributionByIdResponse>();
            Assert.Equal("Refunded", contribution!.Status);
        }

        // Re-processing the outbox again (simulating the CampaignCancelledApplicationEvent's
        // outbox row being reclaimed, e.g. after a transient publish failure) must not throw or
        // attempt to double-refund — the handler's GetSucceededByCampaignIdAsync query now
        // returns nothing for this campaign, since every contribution is already Refunded.
        await _factory.ProcessOutboxMessagesAsync();

        foreach (var contributionId in contributionIds)
        {
            var contributionResponse = await client.GetAsync($"/api/campaigns/{campaignId}/contributions/{contributionId}");
            contributionResponse.EnsureSuccessStatusCode();
            var contribution = await contributionResponse.Content.ReadFromJsonAsync<GetContributionByIdResponse>();
            Assert.Equal("Refunded", contribution!.Status);
        }
    }
}
