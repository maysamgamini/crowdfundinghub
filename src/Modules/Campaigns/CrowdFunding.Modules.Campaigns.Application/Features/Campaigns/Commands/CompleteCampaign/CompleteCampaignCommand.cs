namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CompleteCampaign;

/// <summary>
/// Represents the request to execute the Complete Campaign use case — a published campaign whose
/// deadline has passed and whose funding goal was reached transitions to Successful. Dispatched
/// only by <c>CampaignExpirationBackgroundService</c>, never directly by an API caller.
/// </summary>
public sealed record CompleteCampaignCommand(Guid CampaignId);
