using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implements <see cref="IActiveCampaignCacheRepository"/> against Contributions' own
/// <c>active_campaigns_cache</c> table.
/// </summary>
public sealed class ActiveCampaignCacheRepository : IActiveCampaignCacheRepository
{
    private readonly ContributionsDbContext _dbContext;

    public ActiveCampaignCacheRepository(ContributionsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task UpsertAsync(
        Guid campaignId,
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
                Title = title,
                Currency = currency,
                IsActive = isActive,
                DeadlineUtc = deadlineUtc,
                UpdatedAtUtc = updatedAtUtc,
            });
        }
        else
        {
            existing.Title = title;
            existing.Currency = currency;
            existing.IsActive = isActive;
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
            .Select(x => new ActiveCampaignSnapshot(x.CampaignId, x.Currency, x.IsActive, x.DeadlineUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
