namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Commands.ReleaseExpiredRewardTierReservation;

/// <summary>
/// Releases one expired, still-<c>Reserved</c> reward tier slot reservation back into
/// availability. Dispatched only by <c>RewardTierReservationScavengerBackgroundService</c>, never
/// directly by an API caller. See TICKET-043.
/// </summary>
public sealed record ReleaseExpiredRewardTierReservationCommand(Guid ReservationId);
