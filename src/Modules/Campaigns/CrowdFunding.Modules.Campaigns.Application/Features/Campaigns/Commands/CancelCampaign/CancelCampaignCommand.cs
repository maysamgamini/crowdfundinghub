namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CancelCampaign;

/// <summary>
/// Represents the request to execute the Cancel Campaign use case.
/// </summary>
public sealed record CancelCampaignCommand(Guid CampaignId);
