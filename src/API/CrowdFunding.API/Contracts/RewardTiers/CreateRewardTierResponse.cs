namespace CrowdFunding.API.Contracts.RewardTiers;

/// <summary>
/// Represents the HTTP response payload returned upon defining a reward tier.
/// </summary>
public sealed record CreateRewardTierResponse(Guid RewardTierId, Guid CampaignId, string Title, int TotalCapacity, int AvailableCount);
