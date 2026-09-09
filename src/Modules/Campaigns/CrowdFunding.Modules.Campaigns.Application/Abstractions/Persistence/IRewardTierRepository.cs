using CrowdFunding.Modules.Campaigns.Domain.Aggregates;

namespace CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;

public interface IRewardTierRepository
{
    Task AddAsync(RewardTier rewardTier, CancellationToken cancellationToken);
    Task<RewardTier?> GetByIdAsync(Guid rewardTierId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RewardTier>> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken);
    Task UpdateAsync(RewardTier rewardTier, CancellationToken cancellationToken);
}
