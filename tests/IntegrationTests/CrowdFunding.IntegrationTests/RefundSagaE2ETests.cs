using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Contracts.Pledges;
using CrowdFunding.API.Contracts.Moderation;
using CrowdFunding.API.Migrations;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Domain.Enums;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// TICKET-027's end-to-end proof: a campaign with several confirmed backers is cancelled, and
/// every one of them is refunded purely through event choreography — Campaigns never opens a
/// transaction against Pledges' database to do this.
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
        var pledgeIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var backerEmail = ApiTestExtensions.UniqueEmail($"refund-saga-backer-{i}");
            var (_, backerToken) = await client.RegisterAndLoginAsync(backerEmail, $"Refund Saga Backer {i}");
            client.SetBearerToken(backerToken);

            var pledgeResponse = await client.PostAsJsonAsync(
                $"/api/campaigns/{campaignId}/pledges", new MakePledgeRequest(100m, "USD"));
            pledgeResponse.EnsureSuccessStatusCode();
            var pledge = await pledgeResponse.Content.ReadFromJsonAsync<MakePledgeResponse>();
            pledgeIds.Add(pledge!.PledgeId);

            client.SetBearerToken(adminToken);
            var confirmResponse = await client.PostAsJsonAsync(
                $"/api/campaigns/{campaignId}/pledges/{pledge.PledgeId}/confirm-payment",
                new ConfirmPledgePaymentRequest($"test-payment-ref-{i}"));
            confirmResponse.EnsureSuccessStatusCode();
        }

        // Crediting Campaign.RaisedAmount from each confirmed payment.
        await _factory.ProcessOutboxMessagesAsync();

        client.SetBearerToken(creatorToken);
        var cancelResponse = await client.PostAsync($"/api/campaigns/{campaignId}/cancel", content: null);
        cancelResponse.EnsureSuccessStatusCode();

        // Propagates CampaignCancelledApplicationEvent to Pledges' CampaignTerminationRefundHandler.
        await _factory.ProcessOutboxMessagesAsync();

        foreach (var pledgeId in pledgeIds)
        {
            var pledgeResponse = await client.GetAsync($"/api/campaigns/{campaignId}/pledges/{pledgeId}");
            pledgeResponse.EnsureSuccessStatusCode();
            var pledge = await pledgeResponse.Content.ReadFromJsonAsync<GetPledgeByIdResponse>();
            Assert.Equal("Refunded", pledge!.Status);
        }

        // Re-processing the outbox again (simulating the CampaignCancelledApplicationEvent's
        // outbox row being reclaimed, e.g. after a transient publish failure) must not throw or
        // attempt to double-refund — the handler's GetSucceededByCampaignIdAsync query now
        // returns nothing for this campaign, since every pledge is already Refunded.
        await _factory.ProcessOutboxMessagesAsync();

        foreach (var pledgeId in pledgeIds)
        {
            var pledgeResponse = await client.GetAsync($"/api/campaigns/{campaignId}/pledges/{pledgeId}");
            pledgeResponse.EnsureSuccessStatusCode();
            var pledge = await pledgeResponse.Content.ReadFromJsonAsync<GetPledgeByIdResponse>();
            Assert.Equal("Refunded", pledge!.Status);
        }

        // The books balance. Each refund reached the campaign as a negative compensating ledger entry
        // (nothing was edited or deleted), the exact ledger total is back to zero, and so is the stored balance.
        await using var scope = _factory.Services.CreateAsyncScope();
        var campaigns = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();
        var entries = await campaigns.Set<PledgeLedgerEntry>().AsNoTracking().Where(e => e.CampaignId == campaignId).ToListAsync();
        Assert.Equal(3, entries.Count(e => e.Kind == LedgerEntryKind.Pledge && e.Amount == 100m));
        Assert.Equal(3, entries.Count(e => e.Kind == LedgerEntryKind.Refund && e.Amount == -100m));
        Assert.Equal(0m, entries.Sum(e => e.Amount));
        var campaign = await campaigns.Campaigns.AsNoTracking().SingleAsync(c => c.Id == campaignId);
        Assert.Equal(0m, campaign.RaisedAmount.Amount);
    }
}
