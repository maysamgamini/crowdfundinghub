namespace CrowdFunding.API.Contracts.RewardTiers;

/// <summary>
/// Represents the HTTP response payload for listing a campaign's reward tiers.
/// </summary>
public sealed record ListRewardTiersResponse(IReadOnlyList<RewardTierResponse> RewardTiers);
