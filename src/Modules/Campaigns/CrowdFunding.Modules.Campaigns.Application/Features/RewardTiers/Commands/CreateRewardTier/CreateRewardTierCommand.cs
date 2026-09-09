namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Commands.CreateRewardTier;

public sealed record CreateRewardTierCommand(
    Guid CampaignId,
    string Title,
    string Description,
    decimal MinimumPledgeAmount,
    string Currency,
    int TotalCapacity);
