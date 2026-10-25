using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.AddPledgeToCampaign;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddPledgeToCampaign;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Repositories;
using CrowdFunding.Modules.Campaigns.Infrastructure.Transactions;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Exercises Phase 1's financial concurrency hardening (improvement.md §2.4/§3.4) against a real
/// PostgreSQL instance: the xmin concurrency token + pg_advisory_xact_lock combination must
/// prevent lost updates under concurrent pledges, and the pledge ledger's unique
/// constraint must make redelivery of the same pledge an idempotent no-op.
/// </summary>
[Collection(nameof(CampaignsPostgresCollection))]
public sealed class CampaignPledgeConcurrencyTests
{
    private static readonly IDistributedCache NoOpDistributedCache =
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private readonly CampaignsPostgresFixture _fixture;

    public CampaignPledgeConcurrencyTests(CampaignsPostgresFixture fixture)
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
            .Select(_ => ApplyPledgeAsync(campaignId, Guid.NewGuid(), pledgeAmount, "USD"));

        await Task.WhenAll(tasks);

        await using var verifyContext = _fixture.CreateDbContext();
        var campaign = await verifyContext.Campaigns.SingleAsync(c => c.Id == campaignId);

        Assert.Equal(concurrentPledges * pledgeAmount, campaign.RaisedAmount.Amount);
    }

    [Fact]
    public async Task RedeliveredPledge_ShouldBeAppliedOnlyOnce()
    {
        var campaignId = await CreatePublishedCampaignAsync();
        var pledgeId = Guid.NewGuid();

        // Simulate at-least-once outbox redelivery: the same PledgeId arrives twice,
        // concurrently, racing each other.
        await Task.WhenAll(
            ApplyPledgeAsync(campaignId, pledgeId, 50m, "USD"),
            ApplyPledgeAsync(campaignId, pledgeId, 50m, "USD"));

        await using var verifyContext = _fixture.CreateDbContext();
        var campaign = await verifyContext.Campaigns.SingleAsync(c => c.Id == campaignId);
        var ledgerEntries = await verifyContext.PledgeLedgerEntries
            .Where(entry => entry.PledgeId == pledgeId)
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

    private async Task ApplyPledgeAsync(Guid campaignId, Guid pledgeId, decimal amount, string currency)
    {
        // Each call gets its own DbContext/repository/executor, mirroring separate concurrent
        // requests/outbox-consumer instances hitting the same campaign row.
        await using var dbContext = _fixture.CreateDbContext();
        var transactionExecutor = new CampaignTransactionExecutor(dbContext, NoOpDistributedCache, NullLogger<CampaignTransactionExecutor>.Instance);
        var repository = new CampaignRepository(dbContext, transactionExecutor);
        var ledger = new PledgeLedger(dbContext);
        var rewardTierRepository = new RewardTierRepository(dbContext);
        var rewardTierReservationRepository = new RewardTierReservationRepository(dbContext);
        var handler = new AddPledgeToCampaignCommandHandler(
            repository, ledger, rewardTierRepository, rewardTierReservationRepository, transactionExecutor, new NoOpCampaignRealtimeNotifier(), new CrowdFunding.Modules.Campaigns.Infrastructure.Services.SystemDateTimeProvider());

        await handler.Handle(
            new AddPledgeToCampaignCommand(campaignId, pledgeId, amount, currency),
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

    public Task NotifyRewardTierSoldOutAsync(
        Guid campaignId,
        Guid rewardTierId,
        string title,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
