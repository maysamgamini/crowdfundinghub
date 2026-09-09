namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.FailCampaign;

/// <summary>
/// Represents the request to execute the Fail Campaign use case — a published campaign whose
/// deadline has passed without reaching its funding goal transitions to Failed. Dispatched only
/// by <c>CampaignExpirationBackgroundService</c>, never directly by an API caller.
/// </summary>
public sealed record FailCampaignCommand(Guid CampaignId);
