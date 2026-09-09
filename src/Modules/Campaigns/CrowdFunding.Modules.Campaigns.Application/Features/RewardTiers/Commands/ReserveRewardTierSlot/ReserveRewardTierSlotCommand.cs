namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Commands.ReserveRewardTierSlot;

public sealed record ReserveRewardTierSlotCommand(Guid CampaignId, Guid RewardTierId);
