namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Queries.GetRewardTierById;

/// <summary>
/// Represents the outcome returned by Get Reward Tier By Id.
/// </summary>
public sealed record GetRewardTierByIdResult(
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
