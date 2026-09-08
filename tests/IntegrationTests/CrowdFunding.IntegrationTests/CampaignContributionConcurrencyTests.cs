using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.AddContributionToCampaign;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Repositories;
using CrowdFunding.Modules.Campaigns.Infrastructure.Transactions;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Exercises Phase 1's financial concurrency hardening (improvement.md §2.4/§3.4) against a real
/// PostgreSQL instance: the xmin concurrency token + pg_advisory_xact_lock combination must
/// prevent lost updates under concurrent pledges, and the contribution ledger's unique
/// constraint must make redelivery of the same contribution an idempotent no-op.
/// </summary>
[Collection(nameof(CampaignsPostgresCollection))]
public sealed class CampaignContributionConcurrencyTests
{
    private readonly CampaignsPostgresFixture _fixture;

    public CampaignContributionConcurrencyTests(CampaignsPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConcurrentPledges_ShouldNotLoseUpdates()
    {
        var campaignId = await CreatePublishedCampaignAsync();

        const int concurrentPledges = 20;
        const decimal pledgeAmount = 10m;

        var tasks = Enumerable.Range(0, concurrentPledges)
            .Select(_ => ApplyContributionAsync(campaignId, Guid.NewGuid(), pledgeAmount, "USD"));

        await Task.WhenAll(tasks);

        await using var verifyContext = _fixture.CreateDbContext();
        var campaign = await verifyContext.Campaigns.SingleAsync(c => c.Id == campaignId);

        Assert.Equal(concurrentPledges * pledgeAmount, campaign.RaisedAmount.Amount);
    }

    [Fact]
    public async Task RedeliveredContribution_ShouldBeAppliedOnlyOnce()
    {
        var campaignId = await CreatePublishedCampaignAsync();
        var contributionId = Guid.NewGuid();

        // Simulate at-least-once outbox redelivery: the same ContributionId arrives twice,
        // concurrently, racing each other.
        await Task.WhenAll(
            ApplyContributionAsync(campaignId, contributionId, 50m, "USD"),
            ApplyContributionAsync(campaignId, contributionId, 50m, "USD"));

        await using var verifyContext = _fixture.CreateDbContext();
        var campaign = await verifyContext.Campaigns.SingleAsync(c => c.Id == campaignId);
        var ledgerEntries = await verifyContext.ContributionLedgerEntries
            .Where(entry => entry.ContributionId == contributionId)
            .ToListAsync();

        Assert.Equal(50m, campaign.RaisedAmount.Amount);
        Assert.Single(ledgerEntries);
    }

    private async Task<Guid> CreatePublishedCampaignAsync()
    {
        var now = DateTime.UtcNow;
        var campaign = Campaign.Create(
            Guid.NewGuid(),
            "Integration Test Campaign",
            "A campaign created purely to exercise concurrent-pledge handling in tests.",
            "Technology",
            new Money(1_000_000m, "USD"),
            now.AddDays(30),
            now);
        campaign.Publish(now.AddMinutes(1));

        await using var dbContext = _fixture.CreateDbContext();
        await dbContext.Campaigns.AddAsync(campaign);
        await dbContext.SaveChangesAsync();

        return campaign.Id;
    }

    private async Task ApplyContributionAsync(Guid campaignId, Guid contributionId, decimal amount, string currency)
    {
        // Each call gets its own DbContext/repository/executor, mirroring separate concurrent
        // requests/outbox-consumer instances hitting the same campaign row.
        await using var dbContext = _fixture.CreateDbContext();
        var repository = new CampaignRepository(dbContext);
        var ledger = new ContributionLedger(dbContext);
        var transactionExecutor = new CampaignTransactionExecutor(dbContext);
        var handler = new AddContributionToCampaignCommandHandler(repository, ledger, transactionExecutor, new NoOpCampaignRealtimeNotifier());

        await handler.Handle(
            new AddContributionToCampaignCommand(campaignId, contributionId, amount, currency),
            CancellationToken.None);
    }
}

[CollectionDefinition(nameof(CampaignsPostgresCollection))]
public sealed class CampaignsPostgresCollection : ICollectionFixture<CampaignsPostgresFixture>;

internal sealed class NoOpCampaignRealtimeNotifier : ICampaignRealtimeNotifier
{
    public Task NotifyPledgeReceivedAsync(
        Guid campaignId,
        decimal raisedAmount,
        string currency,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
