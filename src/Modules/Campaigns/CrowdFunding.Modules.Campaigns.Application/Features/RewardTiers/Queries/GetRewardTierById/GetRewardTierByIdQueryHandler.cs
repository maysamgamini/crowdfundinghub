using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;

namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Queries.GetRewardTierById;

/// <summary>
/// Handles Get Reward Tier By Id query requests. Backs the <c>Location</c> header returned by
/// <c>POST /api/campaigns/{campaignId}/reward-tiers</c> — TICKET-047.
/// </summary>
public sealed class GetRewardTierByIdQueryHandler : IQueryHandler<GetRewardTierByIdQuery, GetRewardTierByIdResult>
{
    private readonly IRewardTierRepository _rewardTierRepository;

    public GetRewardTierByIdQueryHandler(IRewardTierRepository rewardTierRepository)
    {
        _rewardTierRepository = rewardTierRepository;
    }

    public async Task<GetRewardTierByIdResult> Handle(GetRewardTierByIdQuery query, CancellationToken cancellationToken)
    {
        var rewardTier = await _rewardTierRepository.GetByIdAsync(query.RewardTierId, cancellationToken);

        if (rewardTier is null || rewardTier.CampaignId != query.CampaignId)
        {
            throw new KeyNotFoundException($"Reward tier '{query.RewardTierId}' was not found for campaign '{query.CampaignId}'.");
        }

        return new GetRewardTierByIdResult(
            rewardTier.Id,
            rewardTier.CampaignId,
            rewardTier.Title,
            rewardTier.Description,
            rewardTier.MinimumPledgeAmount.Amount,
            rewardTier.MinimumPledgeAmount.Currency,
            rewardTier.TotalCapacity,
            rewardTier.ClaimedCount,
            rewardTier.ReservedCount,
            rewardTier.AvailableCount,
            rewardTier.CreatedAtUtc);
    }
}
