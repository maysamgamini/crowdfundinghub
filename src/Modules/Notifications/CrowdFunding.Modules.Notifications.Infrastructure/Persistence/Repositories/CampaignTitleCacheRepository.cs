using CrowdFunding.Modules.Notifications.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Notifications.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Notifications.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Notifications.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implements <see cref="ICampaignTitleCacheRepository"/> against Notifications' own
/// <c>campaign_title_cache</c> table.
/// </summary>
public sealed class CampaignTitleCacheRepository : ICampaignTitleCacheRepository
{
    private readonly NotificationsDbContext _dbContext;

    public CampaignTitleCacheRepository(NotificationsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task UpsertAsync(Guid campaignId, string title, Guid ownerId, DateTime updatedAtUtc, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.CampaignTitleCache.FindAsync([campaignId], cancellationToken);

        if (existing is null)
        {
            _dbContext.CampaignTitleCache.Add(new CampaignTitleCache
            {
                CampaignId = campaignId,
                Title = title,
                OwnerId = ownerId,
                UpdatedAtUtc = updatedAtUtc,
            });
        }
        else
        {
            existing.Title = title;
            existing.OwnerId = ownerId;
            existing.UpdatedAtUtc = updatedAtUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CampaignTitleSnapshot?> GetAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return await _dbContext.CampaignTitleCache
            .AsNoTracking()
            .Where(x => x.CampaignId == campaignId)
            .Select(x => new CampaignTitleSnapshot(x.CampaignId, x.Title, x.OwnerId))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
