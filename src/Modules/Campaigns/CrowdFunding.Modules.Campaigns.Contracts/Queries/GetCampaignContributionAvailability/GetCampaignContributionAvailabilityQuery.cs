namespace CrowdFunding.Modules.Campaigns.Contracts.Queries.GetCampaignContributionAvailability;

/// <summary>
/// Represents the request to execute the Get Campaign Contribution Availability query.
/// </summary>
public sealed record GetCampaignContributionAvailabilityQuery(Guid CampaignId);
