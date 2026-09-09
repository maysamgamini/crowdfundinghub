namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CompleteCampaign;

/// <summary>
/// Represents the outcome returned by Complete Campaign.
/// </summary>
public sealed record CompleteCampaignResult(Guid CampaignId, string Status);
