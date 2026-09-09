namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Commands.CreateRewardTier;

public sealed record CreateRewardTierResult(Guid RewardTierId, Guid CampaignId, string Title, int TotalCapacity, int AvailableCount);
