using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implements <see cref="IActiveCampaignCacheRepository"/> against Pledges' own
/// <c>active_campaigns_cache</c> table.
/// </summary>
public sealed class ActiveCampaignCacheRepository : IActiveCampaignCacheRepository
{
    private readonly PledgesDbContext _dbContext;

    public ActiveCampaignCacheRepository(PledgesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task UpsertAsync(
        Guid campaignId,
        Guid ownerId,
        string title,
        string currency,
        bool isActive,
        DateTime deadlineUtc,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.ActiveCampaignsCache.FindAsync([campaignId], cancellationToken);

        if (existing is null)
        {
            _dbContext.ActiveCampaignsCache.Add(new ActiveCampaignCache
            {
                CampaignId = campaignId,
                OwnerId = ownerId,
                Title = title,
                Currency = currency,
                IsActive = isActive,
                DeadlineUtc = deadlineUtc,
                UpdatedAtUtc = updatedAtUtc,
            });
        }
        else
        {
            existing.OwnerId = ownerId;
            existing.Title = title;
            existing.Currency = currency;
            existing.DeadlineUtc = deadlineUtc;
            existing.UpdatedAtUtc = updatedAtUtc;
        }
    }

    /// <inheritdoc/>
    public async Task SetActiveStatusAsync(Guid campaignId, bool isActive, DateTime updatedAtUtc, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.ActiveCampaignsCache.FindAsync([campaignId], cancellationToken);

        if (existing is null)
        {
            // The "created" event has not been handled yet (events can arrive out of order). Keep what this event says
            // instead of dropping it: a placeholder that accepts no pledges (its deadline is already behind it) until
            // "created" fills in the rest. Without this the later "created" would store the campaign as inactive and
            // nothing would ever make it active again.
            _dbContext.ActiveCampaignsCache.Add(new ActiveCampaignCache
            {
                CampaignId = campaignId,
                OwnerId = Guid.Empty,
                Title = string.Empty,
                Currency = string.Empty,
                IsActive = isActive,
                DeadlineUtc = updatedAtUtc,
                UpdatedAtUtc = updatedAtUtc,
            });
            return;
        }

        existing.IsActive = isActive;
        existing.UpdatedAtUtc = updatedAtUtc;
    }

    /// <inheritdoc/>
    public async Task<ActiveCampaignSnapshot?> GetAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return await _dbContext.ActiveCampaignsCache
            .AsNoTracking()
            .Where(x => x.CampaignId == campaignId)
            .Select(x => new ActiveCampaignSnapshot(x.CampaignId, x.OwnerId, x.Currency, x.IsActive, x.DeadlineUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
