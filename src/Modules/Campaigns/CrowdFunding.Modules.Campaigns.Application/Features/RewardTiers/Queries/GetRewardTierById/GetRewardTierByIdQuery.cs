namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Queries.GetRewardTierById;

/// <summary>
/// Represents the request to execute the Get Reward Tier By Id query.
/// </summary>
public sealed record GetRewardTierByIdQuery(Guid CampaignId, Guid RewardTierId);
