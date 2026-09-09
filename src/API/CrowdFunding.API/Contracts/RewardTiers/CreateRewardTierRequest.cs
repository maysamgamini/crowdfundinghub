namespace CrowdFunding.API.Contracts.RewardTiers;

/// <summary>
/// Represents the HTTP request payload for defining a reward tier.
/// </summary>
public sealed record CreateRewardTierRequest(
    string Title,
    string Description,
    decimal MinimumPledgeAmount,
    string Currency,
    int TotalCapacity);
