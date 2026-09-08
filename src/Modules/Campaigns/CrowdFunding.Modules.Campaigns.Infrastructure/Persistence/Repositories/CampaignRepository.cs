using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Infrastructure.Caching;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implements repository operations for Campaign.
/// </summary>
public sealed class CampaignRepository : ICampaignRepository
{
    private readonly CampaignsDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CampaignRepository> _logger;

    public CampaignRepository(CampaignsDbContext dbContext, IDistributedCache cache, ILogger<CampaignRepository> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task AddAsync(Campaign campaign, CancellationToken cancellationToken)
    {
        await _dbContext.Campaigns.AddAsync(campaign, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Campaign?> GetByIdAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return await _dbContext.Campaigns
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Campaign campaign, CancellationToken cancellationToken)
    {
        _dbContext.Campaigns.Update(campaign);

        // NOTE: this runs before the enclosing transaction commits (CampaignTransactionExecutor
        // calls SaveChanges/Commit after the handler's action, which is where UpdateAsync is
        // invoked). That leaves a narrow window where a concurrent read could repopulate the
        // cache with the pre-update value between this Remove and the commit. It's bounded by
        // CachedCampaignReadService's 30s TTL, which exists as a safety net for exactly this
        // case — a stale read here self-heals within 30s even in the worst case. Moving
        // invalidation to strictly after commit (e.g. inside the transaction executor) would
        // close the window entirely if this ever needs stronger freshness guarantees.
        try
        {
            await _cache.RemoveAsync(CampaignCacheKeys.Details(campaign.Id), cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to invalidate cache for campaign {CampaignId}.", campaign.Id);
        }
    }
}
