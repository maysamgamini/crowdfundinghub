using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;

namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Queries.ListRewardTiersByCampaign;

/// <summary>
/// Handles List Reward Tiers By Campaign query requests — lets backers browse a campaign's
/// available reward perks and remaining inventory. See TICKET-047.
/// </summary>
public sealed class ListRewardTiersByCampaignQueryHandler : IQueryHandler<ListRewardTiersByCampaignQuery, ListRewardTiersByCampaignResult>
{
    private readonly IRewardTierRepository _rewardTierRepository;

    public ListRewardTiersByCampaignQueryHandler(IRewardTierRepository rewardTierRepository)
    {
        _rewardTierRepository = rewardTierRepository;
    }

    public async Task<ListRewardTiersByCampaignResult> Handle(ListRewardTiersByCampaignQuery query, CancellationToken cancellationToken)
    {
        var rewardTiers = await _rewardTierRepository.GetByCampaignIdAsync(query.CampaignId, cancellationToken);

        var summaries = rewardTiers
            .Select(rewardTier => new RewardTierSummary(
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
                rewardTier.CreatedAtUtc))
            .ToList();

        return new ListRewardTiersByCampaignResult(summaries);
    }
}
