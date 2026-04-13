namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.PublishCampaign;

/// <summary>
/// Represents the outcome returned by Publish Campaign.
/// </summary>
public sealed record PublishCampaignResult(Guid CampaignId, string Status);
