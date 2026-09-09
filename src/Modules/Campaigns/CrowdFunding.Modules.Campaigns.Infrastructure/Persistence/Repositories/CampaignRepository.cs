using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Domain.Enums;
using CrowdFunding.Modules.Campaigns.Infrastructure.Caching;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implements repository operations for Campaign.
/// </summary>
public sealed class CampaignRepository : ICampaignRepository
{
    private readonly CampaignsDbContext _dbContext;
    private readonly ICampaignTransactionExecutor _transactionExecutor;

    public CampaignRepository(CampaignsDbContext dbContext, ICampaignTransactionExecutor transactionExecutor)
    {
        _dbContext = dbContext;
        _transactionExecutor = transactionExecutor;
    }

    /// <inheritdoc/>
    public Task AddAsync(Campaign campaign, CancellationToken cancellationToken)
    {
        // Add (not AddAsync) — EF's AddAsync exists only for value generators that need async DB
        // access (e.g. SQL Server HiLo), which Campaign's client-generated Guid key doesn't use.
        _dbContext.Campaigns.Add(campaign);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<Campaign?> GetByIdAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return await _dbContext.Campaigns
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(Campaign campaign, CancellationToken cancellationToken)
    {
        _dbContext.Campaigns.Update(campaign);

        // TICKET-037: queued, not evicted immediately — ICampaignTransactionExecutor only
        // actually removes this key from Redis after its surrounding transaction has committed.
        // Evicting here, before SaveChanges/Commit even runs, previously left a window where a
        // concurrent read could repopulate the cache with the about-to-be-overwritten value and
        // serve it for a full 30s TTL instead of until this write.
        _transactionExecutor.EnqueueCacheInvalidation(CampaignCacheKeys.Details(campaign.Id));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Guid>> GetExpiredPublishedCampaignIdsAsync(DateTime asOfUtc, CancellationToken cancellationToken)
    {
        return await _dbContext.Campaigns
            .Where(x => x.Status == CampaignStatus.Published && x.DeadlineUtc <= asOfUtc)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
    }
}
