namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Queries.ListRewardTiersByCampaign;

/// <summary>
/// Represents the request to execute the List Reward Tiers By Campaign query.
/// </summary>
public sealed record ListRewardTiersByCampaignQuery(Guid CampaignId);
