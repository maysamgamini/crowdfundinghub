namespace CrowdFunding.API.Contracts.RewardTiers;

/// <summary>
/// Represents the HTTP response payload for a single reward tier.
/// </summary>
public sealed record RewardTierResponse(
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
