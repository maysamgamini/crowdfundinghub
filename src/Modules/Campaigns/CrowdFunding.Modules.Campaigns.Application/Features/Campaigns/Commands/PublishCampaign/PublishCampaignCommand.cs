namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.PublishCampaign;

/// <summary>
/// Represents the request to execute the Publish Campaign use case.
/// </summary>
public sealed record PublishCampaignCommand(Guid CampaignId);
