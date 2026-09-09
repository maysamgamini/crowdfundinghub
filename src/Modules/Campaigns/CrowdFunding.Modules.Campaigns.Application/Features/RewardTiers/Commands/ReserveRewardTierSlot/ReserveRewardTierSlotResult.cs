namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Commands.ReserveRewardTierSlot;

public sealed record ReserveRewardTierSlotResult(Guid RewardTierId, int AvailableCount, Guid ReservationId);
