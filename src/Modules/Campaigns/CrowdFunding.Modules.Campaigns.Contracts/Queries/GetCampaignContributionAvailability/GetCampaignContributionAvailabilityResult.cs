namespace CrowdFunding.Modules.Campaigns.Contracts.Queries.GetCampaignContributionAvailability;

/// <summary>
/// Represents the outcome returned by Get Campaign Contribution Availability.
/// </summary>
public sealed record GetCampaignContributionAvailabilityResult(
    Guid CampaignId,
    bool Exists,
    bool CanAcceptContributions,
    string Status);
