namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Queries.ListRewardTiersByCampaign;

/// <summary>
/// One reward tier row in a List Reward Tiers By Campaign result.
/// </summary>
public sealed record RewardTierSummary(
    Guid RewardTierId,
    Guid CampaignId,
    string Title,
    string Description,
    decimal MinimumPledgeAmount,
    string MinimumPledgeCurrency,
    int TotalCapacity,
    int ClaimedCount,
    int ReservedCount,
    int AvailableCount,
    DateTime CreatedAtUtc);

/// <summary>
/// Represents the outcome returned by List Reward Tiers By Campaign.
/// </summary>
public sealed record ListRewardTiersByCampaignResult(IReadOnlyList<RewardTierSummary> RewardTiers);
