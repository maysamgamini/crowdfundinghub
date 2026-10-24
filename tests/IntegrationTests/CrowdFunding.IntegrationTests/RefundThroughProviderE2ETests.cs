using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Contracts.Contributions;
using CrowdFunding.API.Contracts.Moderation;
using CrowdFunding.API.Migrations;
using CrowdFunding.Modules.Contributions.Domain.Enums;
using CrowdFunding.Modules.Contributions.Infrastructure.Payments;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Refunds are real calls to the payment provider now, not just a status change. These tests cancel a funded
/// campaign and watch the whole path: the provider is asked once per pledge, the provider's reference is kept,
/// and when the provider refuses once the retry is a new attempt with a new key and goes through.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class RefundThroughProviderE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public RefundThroughProviderE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    private sealed record Funded(Guid CampaignId, string CreatorToken, IReadOnlyList<Guid> PledgeIds);

    private async Task<Funded> FundedCampaignAsync(string label, int backers)
    {
        using var client = _factory.CreateClient();
        var (_, creatorToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail($"{label}-creator"), "Creator");
        client.SetBearerToken(creatorToken);
        var created = await (await client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest(
            "Refund Through Provider Campaign",
            "A story that is definitely longer than twenty characters, describing this campaign's purpose in full.",
            "Community", 10_000m, "USD", DateTime.UtcNow.AddDays(30)))).Content.ReadFromJsonAsync<CreateCampaignResponse>();
        var campaignId = created!.CampaignId;
        await _factory.ProcessOutboxMessagesAsync();

        var adminEmail = ApiTestExtensions.UniqueEmail($"{label}-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);
        client.SetBearerToken(adminToken);
        (await client.PostAsJsonAsync($"/api/moderation/reviews/{campaignId}/approve", new ReviewCampaignRequest(Notes: null))).EnsureSuccessStatusCode();
        client.SetBearerToken(creatorToken);
        (await client.PostAsync($"/api/campaigns/{campaignId}/publish", content: null)).EnsureSuccessStatusCode();
        await _factory.ProcessOutboxMessagesAsync();

        var pledges = new List<Guid>();
        for (var i = 0; i < backers; i++)
        {
            var (_, backerToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail($"{label}-backer-{i}"), $"Backer {i}");
            client.SetBearerToken(backerToken);
            var pledge = await (await client.PostAsJsonAsync($"/api/campaigns/{campaignId}/contributions", new MakeContributionRequest(100m, "USD")))
                .Content.ReadFromJsonAsync<MakeContributionResponse>();
            pledges.Add(pledge!.ContributionId);

            client.SetBearerToken(adminToken);
            (await client.PostAsJsonAsync(
                $"/api/campaigns/{campaignId}/contributions/{pledge.ContributionId}/confirm-payment",
                new ConfirmContributionPaymentRequest($"payment-ref-{label}-{i}"))).EnsureSuccessStatusCode();
        }

        await _factory.ProcessOutboxMessagesAsync();
        return new Funded(campaignId, creatorToken, pledges);
    }

    private async Task CancelAsync(Funded funded)
    {
        using var client = _factory.CreateClient();
        client.SetBearerToken(funded.CreatorToken);
        (await client.PostAsync($"/api/campaigns/{funded.CampaignId}/cancel", content: null)).EnsureSuccessStatusCode();
    }

    private async Task<List<(ContributionStatus Status, string? RefundReference, int RefundFailures)>> StatesAsync(Funded funded)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ContributionsDbContext>();
        var rows = await db.Contributions.AsNoTracking().Where(c => funded.PledgeIds.Contains(c.Id)).ToListAsync();
        return rows.Select(c => (c.Status, c.RefundReference, c.RefundFailures)).ToList();
    }

    [Fact]
    public async Task CancellingAFundedCampaign_AsksTheProviderOncePerPledge_AndKeepsItsReferences()
    {
        var provider = _factory.Services.GetRequiredService<FakePaymentProvider>();
        var funded = await FundedCampaignAsync("provider-refund", backers: 3);
        var before = provider.RefundsPerformed;

        await CancelAsync(funded);
        await _factory.ProcessOutboxMessagesAsync();

        var states = await StatesAsync(funded);
        Assert.All(states, state =>
        {
            Assert.Equal(ContributionStatus.Refunded, state.Status);
            Assert.StartsWith("re_mock_", state.RefundReference);
            Assert.Equal(0, state.RefundFailures);
        });
        Assert.Equal(3, provider.RefundsPerformed - before);
        Assert.Equal(3, states.Select(s => s.RefundReference).Distinct().Count());
    }

    [Fact]
    public async Task WhenTheProviderRefusesOnce_ThePledgesStayRefundable_AndTheRetryIsANewAttempt()
    {
        var provider = _factory.Services.GetRequiredService<FakePaymentProvider>();
        var funded = await FundedCampaignAsync("provider-refuses", backers: 1);
        var before = provider.RefundsPerformed;

        provider.FailRefundsWith("insufficient_funds");
        try
        {
            await CancelAsync(funded);
            await _factory.ProcessOutboxMessagesAsync();

            var afterRefusal = Assert.Single(await StatesAsync(funded));
            Assert.Equal(ContributionStatus.Succeeded, afterRefusal.Status);
            Assert.Equal(1, afterRefusal.RefundFailures);
        }
        finally
        {
            provider.FailRefundsWith(null);
        }

        // The failed delivery was rescheduled a few seconds into the future; make it due now instead of waiting.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var campaigns = scope.ServiceProvider.GetRequiredService<CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts.CampaignsDbContext>();
            await campaigns.Database.ExecuteSqlRawAsync(
                "UPDATE campaigns.campaigns_outbox_messages SET \"ScheduledAtUtc\" = {0} WHERE \"Status\" = 0 AND \"Attempts\" > 0",
                DateTime.UtcNow.AddMinutes(-1));
        }

        await _factory.ProcessOutboxMessagesAsync();

        var afterRetry = Assert.Single(await StatesAsync(funded));
        Assert.Equal(ContributionStatus.Refunded, afterRetry.Status);
        Assert.Equal(1, afterRetry.RefundFailures);
        Assert.StartsWith("re_mock_", afterRetry.RefundReference);
        Assert.Equal(1, provider.RefundsPerformed - before);
    }
}
