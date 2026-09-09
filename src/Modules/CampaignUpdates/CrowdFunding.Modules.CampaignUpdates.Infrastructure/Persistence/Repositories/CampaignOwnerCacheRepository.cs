using CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Persistence;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.Repositories;

public sealed class CampaignOwnerCacheRepository : ICampaignOwnerCacheRepository
{
    private readonly CampaignUpdatesDbContext _dbContext;

    public CampaignOwnerCacheRepository(CampaignUpdatesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UpsertAsync(Guid campaignId, Guid ownerId, DateTime updatedAtUtc, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.CampaignOwnerCache.FindAsync([campaignId], cancellationToken);

        if (existing is null)
        {
            _dbContext.CampaignOwnerCache.Add(new CampaignOwnerCache { CampaignId = campaignId, OwnerId = ownerId, UpdatedAtUtc = updatedAtUtc });
        }
        else
        {
            existing.OwnerId = ownerId;
            existing.UpdatedAtUtc = updatedAtUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<CampaignOwnerSnapshot?> GetAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return await _dbContext.CampaignOwnerCache
            .AsNoTracking()
            .Where(x => x.CampaignId == campaignId)
            .Select(x => new CampaignOwnerSnapshot(x.CampaignId, x.OwnerId))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
