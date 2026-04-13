namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;

/// <summary>
/// Represents the outcome returned by Create Campaign.
/// </summary>
public sealed record CreateCampaignResult(Guid CampaignId);
