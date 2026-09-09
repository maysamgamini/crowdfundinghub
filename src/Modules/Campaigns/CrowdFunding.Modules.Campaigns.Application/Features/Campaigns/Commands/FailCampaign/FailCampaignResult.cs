namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.FailCampaign;

/// <summary>
/// Represents the outcome returned by Fail Campaign.
/// </summary>
public sealed record FailCampaignResult(Guid CampaignId, string Status);
