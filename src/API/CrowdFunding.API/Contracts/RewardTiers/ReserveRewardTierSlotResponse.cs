namespace CrowdFunding.API.Contracts.RewardTiers;

/// <summary>
/// Represents the HTTP response payload returned upon reserving a reward tier slot.
/// </summary>
public sealed record ReserveRewardTierSlotResponse(Guid RewardTierId, int AvailableCount);
