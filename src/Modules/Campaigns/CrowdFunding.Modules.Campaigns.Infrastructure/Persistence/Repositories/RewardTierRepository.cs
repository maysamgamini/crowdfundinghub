using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Repositories;

public sealed class RewardTierRepository : IRewardTierRepository
{
    private readonly CampaignsDbContext _dbContext;

    public RewardTierRepository(CampaignsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(RewardTier rewardTier, CancellationToken cancellationToken)
    {
        _dbContext.RewardTiers.Add(rewardTier);
        return Task.CompletedTask;
    }

    public Task<RewardTier?> GetByIdAsync(Guid rewardTierId, CancellationToken cancellationToken)
        => _dbContext.RewardTiers.SingleOrDefaultAsync(x => x.Id == rewardTierId, cancellationToken);

    public async Task<IReadOnlyList<RewardTier>> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken)
        => await _dbContext.RewardTiers
            .Where(x => x.CampaignId == campaignId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task UpdateAsync(RewardTier rewardTier, CancellationToken cancellationToken)
    {
        _dbContext.RewardTiers.Update(rewardTier);
        return Task.CompletedTask;
    }
}
