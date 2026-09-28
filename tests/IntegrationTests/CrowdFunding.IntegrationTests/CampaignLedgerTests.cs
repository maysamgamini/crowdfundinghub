using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.AddContributionToCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.RecordContributionRefund;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Domain.Enums;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Repositories;
using CrowdFunding.Modules.Campaigns.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// The append-only campaign ledger against real PostgreSQL. Every test ends by asserting the core
/// invariant: <c>campaigns.raised_amount = SUM(campaign_ledger_entries.Amount)</c>.
/// </summary>
[Collection(nameof(CampaignsPostgresCollection))]
public sealed class CampaignLedgerTests
{
    private static readonly IDistributedCache NoOpDistributedCache =
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private readonly CampaignsPostgresFixture _fixture;

    public CampaignLedgerTests(CampaignsPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConcurrentPledges_ShouldSumCorrectly()
    {
        var campaignId = await CreatePublishedCampaignAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => PledgeAsync(campaignId, Guid.NewGuid(), 10m)));

        Assert.All(results, result => Assert.True(result.Credited));
        Assert.Equal(200m, await RaisedAmountAsync(campaignId));
        await AssertSnapshotEqualsLedgerSumAsync(campaignId);
    }

    [Fact]
    public async Task RedeliveredPledge_ShouldCreditOnce_SequentiallyAndConcurrently()
    {
        var campaignId = await CreatePublishedCampaignAsync();
        var contributionId = Guid.NewGuid();

        await PledgeAsync(campaignId, contributionId, 50m);
        var concurrentRedeliveries = await Task.WhenAll(
            PledgeAsync(campaignId, contributionId, 50m),
            PledgeAsync(campaignId, contributionId, 50m));

        Assert.All(concurrentRedeliveries, result => Assert.False(result.Credited));
        Assert.Equal(50m, await RaisedAmountAsync(campaignId));
        Assert.Single(await EntriesAsync(campaignId));
        await AssertSnapshotEqualsLedgerSumAsync(campaignId);
    }

    [Fact]
    public async Task Refund_ShouldReverseThePledge_WithACompensatingEntry_Once()
    {
        var campaignId = await CreatePublishedCampaignAsync();
        var refunded = Guid.NewGuid();
        await PledgeAsync(campaignId, refunded, 50m);
        await PledgeAsync(campaignId, Guid.NewGuid(), 30m);
        await CancelAsync(campaignId);

        var first = await RefundAsync(campaignId, refunded, 50m);
        var redelivered = await RefundAsync(campaignId, refunded, 50m);

        Assert.True(first.Reversed);
        Assert.False(redelivered.Reversed);
        Assert.Equal(30m, await RaisedAmountAsync(campaignId));

        // History is preserved: the pledge is still there, reversed by a separate negative entry.
        var entries = await EntriesAsync(campaignId);
        Assert.Contains(entries, entry => entry.ContributionId == refunded && entry is { Kind: CampaignLedgerEntryKind.Pledge, Amount: 50m });
        Assert.Contains(entries, entry => entry.ContributionId == refunded && entry is { Kind: CampaignLedgerEntryKind.Refund, Amount: -50m });
        await AssertSnapshotEqualsLedgerSumAsync(campaignId);
    }

    [Fact]
    public async Task RefundOfAContributionThatWasNeverCredited_ShouldChangeNothing()
    {
        var campaignId = await CreatePublishedCampaignAsync();

        var result = await RefundAsync(campaignId, Guid.NewGuid(), 50m);

        Assert.False(result.Reversed);
        Assert.Empty(await EntriesAsync(campaignId));
        await AssertSnapshotEqualsLedgerSumAsync(campaignId);
    }

    [Fact]
    public async Task CancelledCampaign_ShouldRejectPledges()
    {
        var campaignId = await CreatePublishedCampaignAsync();
        await CancelAsync(campaignId);

        var result = await PledgeAsync(campaignId, Guid.NewGuid(), 50m);

        Assert.False(result.Credited);
        Assert.Empty(await EntriesAsync(campaignId));
        Assert.Equal(0m, await RaisedAmountAsync(campaignId));
    }

    [Fact]
    public async Task CancelRacingPledges_ShouldNeverCreditAfterTheCancellation()
    {
        var campaignId = await CreatePublishedCampaignAsync();

        var pledges = Enumerable.Range(0, 10).Select(_ => PledgeAsync(campaignId, Guid.NewGuid(), 10m)).ToList();
        var cancellation = CancelThroughTheLockAsync(campaignId);
        await Task.WhenAll(pledges.Cast<Task>().Append(cancellation));

        // Every pledge the lock ordered before the cancellation is credited, none after it, and
        // the snapshot still equals the ledger.
        var credited = pledges.Count(pledge => pledge.Result.Credited);
        Assert.Equal(credited, (await EntriesAsync(campaignId)).Count);
        Assert.Equal(credited * 10m, await RaisedAmountAsync(campaignId));
        await AssertSnapshotEqualsLedgerSumAsync(campaignId);
    }

    [Fact]
    public async Task Ledger_ShouldBeAppendOnly_EnforcedByTheDatabase()
    {
        var campaignId = await CreatePublishedCampaignAsync();
        await PledgeAsync(campaignId, Guid.NewGuid(), 50m);
        await using var dbContext = _fixture.CreateDbContext();

        var update = () => dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE campaign_ledger_entries SET "Amount" = 5000 WHERE "CampaignId" = {campaignId}""");
        var delete = () => dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""DELETE FROM campaign_ledger_entries WHERE "CampaignId" = {campaignId}""");

        Assert.Contains("append-only", (await Assert.ThrowsAsync<PostgresException>(update)).MessageText);
        Assert.Contains("append-only", (await Assert.ThrowsAsync<PostgresException>(delete)).MessageText);
        await AssertSnapshotEqualsLedgerSumAsync(campaignId);
    }

    [Fact]
    public async Task WritingASnapshotThatDisagreesWithTheLedger_ShouldBeRejectedByTheDatabase()
    {
        var campaignId = await CreatePublishedCampaignAsync();
        await PledgeAsync(campaignId, Guid.NewGuid(), 50m);

        // Through the ORM (an aggregate whose in-memory total was tampered with) ...
        await using (var dbContext = _fixture.CreateDbContext())
        {
            var campaign = await dbContext.Campaigns.SingleAsync(c => c.Id == campaignId);
            campaign.ApplyLedgerTotal(new Money(999_999m, "USD"));

            var ormWrite = await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(ormWrite.InnerException).SqlState);
        }

        // ... and directly in SQL.
        await using (var dbContext = _fixture.CreateDbContext())
        {
            var sqlWrite = () => dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE campaigns SET raised_amount = 1 WHERE \"Id\" = {campaignId}");
            Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(sqlWrite)).SqlState);
        }

        Assert.Equal(50m, await RaisedAmountAsync(campaignId));
        await AssertSnapshotEqualsLedgerSumAsync(campaignId);
    }

    private async Task<Guid> CreatePublishedCampaignAsync()
    {
        var now = DateTime.UtcNow;
        var campaign = Campaign.Create(
            Guid.NewGuid(),
            "Ledger Test Campaign",
            "A campaign created purely to exercise the append-only funding ledger in tests.",
            "Technology",
            new Money(1_000_000m, "USD"),
            now.AddDays(30),
            now);
        campaign.Publish(now.AddMinutes(1));

        await using var dbContext = _fixture.CreateDbContext();
        dbContext.Campaigns.Add(campaign);
        await dbContext.SaveChangesAsync();

        return campaign.Id;
    }

    /// <summary>
    /// Each call gets its own DbContext and executor, like separate requests or outbox consumers.
    /// </summary>
    private async Task<AddContributionToCampaignResult> PledgeAsync(Guid campaignId, Guid contributionId, decimal amount)
    {
        await using var dbContext = _fixture.CreateDbContext();
        var (executor, repository, ledger) = CreateInfrastructure(dbContext);
        var handler = new AddContributionToCampaignCommandHandler(
            repository, ledger, new RewardTierRepository(dbContext), new RewardTierReservationRepository(dbContext),
            executor, new NoOpCampaignRealtimeNotifier(), new SystemClock());

        return await handler.Handle(new AddContributionToCampaignCommand(campaignId, contributionId, amount, "USD"), CancellationToken.None);
    }

    private async Task<RecordContributionRefundResult> RefundAsync(Guid campaignId, Guid contributionId, decimal amount)
    {
        await using var dbContext = _fixture.CreateDbContext();
        var (executor, repository, ledger) = CreateInfrastructure(dbContext);
        var handler = new RecordContributionRefundCommandHandler(repository, ledger, executor, new NoOpCampaignRealtimeNotifier(), new SystemClock());

        return await handler.Handle(new RecordContributionRefundCommand(campaignId, contributionId, amount, "USD"), CancellationToken.None);
    }

    private async Task CancelAsync(Guid campaignId)
    {
        await using var dbContext = _fixture.CreateDbContext();
        var campaign = await dbContext.Campaigns.SingleAsync(c => c.Id == campaignId);
        campaign.Cancel();
        await dbContext.SaveChangesAsync();
    }

    /// <summary>Cancels the way CancelCampaignCommandHandler does: inside the campaign's advisory lock.</summary>
    private async Task CancelThroughTheLockAsync(Guid campaignId)
    {
        await using var dbContext = _fixture.CreateDbContext();
        var (executor, repository, _) = CreateInfrastructure(dbContext);

        await executor.ExecuteAsync(CrowdFunding.BuildingBlocks.Domain.Common.AdvisoryLockKey.FromGuid(campaignId), async ct =>
        {
            var campaign = (await repository.GetByIdAsync(campaignId, ct))!;
            campaign.Cancel();
            await repository.UpdateAsync(campaign, ct);
        }, CancellationToken.None);
    }

    private static (CampaignTransactionExecutor Executor, CampaignRepository Repository, CampaignLedger Ledger) CreateInfrastructure(CampaignsDbContext dbContext)
    {
        var executor = new CampaignTransactionExecutor(dbContext, NoOpDistributedCache, NullLogger<CampaignTransactionExecutor>.Instance);
        return (executor, new CampaignRepository(dbContext, executor), new CampaignLedger(dbContext, executor));
    }

    private async Task<decimal> RaisedAmountAsync(Guid campaignId)
    {
        await using var dbContext = _fixture.CreateDbContext();
        return (await dbContext.Campaigns.AsNoTracking().SingleAsync(c => c.Id == campaignId)).RaisedAmount.Amount;
    }

    private async Task<List<CampaignLedgerEntry>> EntriesAsync(Guid campaignId)
    {
        await using var dbContext = _fixture.CreateDbContext();
        return await dbContext.LedgerEntries.AsNoTracking().Where(entry => entry.CampaignId == campaignId).ToListAsync();
    }

    private async Task AssertSnapshotEqualsLedgerSumAsync(Guid campaignId)
    {
        var entries = await EntriesAsync(campaignId);
        Assert.Equal(entries.Sum(entry => entry.Amount), await RaisedAmountAsync(campaignId));
    }

    private sealed class SystemClock : IDateTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
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
