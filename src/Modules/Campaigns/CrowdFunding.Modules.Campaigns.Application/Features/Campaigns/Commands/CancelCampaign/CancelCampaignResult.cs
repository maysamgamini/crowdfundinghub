namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CancelCampaign;

/// <summary>
/// Represents the outcome returned by Cancel Campaign.
/// </summary>
public sealed record CancelCampaignResult(Guid CampaignId, string Status);
