using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Repositories;
using CrowdFunding.Modules.Campaigns.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// TICKET-037: proves cache eviction for a campaign write happens strictly after the surrounding
/// PostgreSQL transaction commits — never before, and never at all if the transaction rolls back.
/// Previously <c>CampaignRepository.UpdateAsync</c> called <c>IDistributedCache.RemoveAsync</c>
/// immediately, ahead of <c>SaveChangesAsync</c>/<c>CommitAsync</c>, leaving a window where a
/// concurrent read could repopulate the cache with the soon-to-be-overwritten value.
/// </summary>
[Collection(nameof(CampaignsPostgresCollection))]
public sealed class CampaignCachePostCommitInvalidationTests
{
    private readonly CampaignsPostgresFixture _fixture;

    public CampaignCachePostCommitInvalidationTests(CampaignsPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpdateAsync_ShouldEvictCache_OnlyAfterTheTransactionCommits()
    {
        var campaignId = await CreateCampaignAsync();
        var recordingCache = new RecordingDistributedCache();

        await using var dbContext = _fixture.CreateDbContext();
        var transactionExecutor = new CampaignTransactionExecutor(dbContext, recordingCache, NullLogger<CampaignTransactionExecutor>.Instance);
        var repository = new CampaignRepository(dbContext, transactionExecutor);

        await transactionExecutor.ExecuteAsync(
            async ct =>
            {
                var campaign = await repository.GetByIdAsync(campaignId, ct) ?? throw new InvalidOperationException("not found");
                await repository.UpdateAsync(campaign, ct);

                // The whole point of deferring eviction: at this instant, mid-transaction, the
                // cache must not have been touched yet even though UpdateAsync (which used to
                // evict immediately) already ran.
                Assert.Empty(recordingCache.RemovedKeys);
                return 0;
            },
            CancellationToken.None);

        Assert.Single(recordingCache.RemovedKeys);
    }

    [Fact]
    public async Task UpdateAsync_ShouldNotEvictCache_WhenTheSurroundingTransactionRollsBack()
    {
        var campaignId = await CreateCampaignAsync();
        var recordingCache = new RecordingDistributedCache();

        await using var dbContext = _fixture.CreateDbContext();
        var transactionExecutor = new CampaignTransactionExecutor(dbContext, recordingCache, NullLogger<CampaignTransactionExecutor>.Instance);
        var repository = new CampaignRepository(dbContext, transactionExecutor);

        var action = async () => await transactionExecutor.ExecuteAsync<int>(
            async ct =>
            {
                var campaign = await repository.GetByIdAsync(campaignId, ct) ?? throw new InvalidOperationException("not found");
                await repository.UpdateAsync(campaign, ct);
                throw new InvalidOperationException("simulated failure after the cache key was queued but before commit");
            },
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(action);

        // The transaction rolled back, so nothing actually changed — evicting the cache here
        // would have been pure churn (or worse, have raced a concurrent read into caching data
        // that never stopped being current).
        Assert.Empty(recordingCache.RemovedKeys);
    }

    private async Task<Guid> CreateCampaignAsync()
    {
        var now = DateTime.UtcNow;
        var campaign = Campaign.Create(
            Guid.NewGuid(),
            "Post-Commit Cache Invalidation Test Campaign",
            "A campaign created purely to exercise post-commit cache eviction timing in tests.",
            "Technology",
            new Money(1_000_000m, "USD"),
            now.AddDays(30),
            now);

        await using var dbContext = _fixture.CreateDbContext();
        await dbContext.Campaigns.AddAsync(campaign);
        await dbContext.SaveChangesAsync();

        return campaign.Id;
    }
}

/// <summary>Records every key passed to <see cref="RemoveAsync"/>; every other member is a no-op.</summary>
internal sealed class RecordingDistributedCache : IDistributedCache
{
    private readonly List<string> _removedKeys = [];

    public IReadOnlyList<string> RemovedKeys => _removedKeys;

    public byte[]? Get(string key) => null;

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);

    public void Refresh(string key)
    {
    }

    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

    public void Remove(string key) => _removedKeys.Add(key);

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        _removedKeys.Add(key);
        return Task.CompletedTask;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        => Task.CompletedTask;
}
