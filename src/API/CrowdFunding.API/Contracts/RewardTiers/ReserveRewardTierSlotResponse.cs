namespace CrowdFunding.API.Contracts.RewardTiers;

/// <summary>
/// Represents the HTTP response payload returned upon reserving a reward tier slot.
/// </summary>
/// <param name="RewardTierId">The reward tier the slot was reserved on.</param>
/// <param name="AvailableCount">Slots remaining on the tier after this reservation.</param>
/// <param name="ReservationId">Pass this back as <c>RewardTierReservationId</c> when creating
/// the contribution to check out against the held slot within the 15-minute reservation
/// window.</param>
public sealed record ReserveRewardTierSlotResponse(Guid RewardTierId, int AvailableCount, Guid ReservationId);
