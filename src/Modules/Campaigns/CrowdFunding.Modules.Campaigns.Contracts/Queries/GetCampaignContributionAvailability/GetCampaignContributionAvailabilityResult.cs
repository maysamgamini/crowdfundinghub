namespace CrowdFunding.Modules.Campaigns.Contracts.Queries.GetCampaignContributionAvailability;

public sealed record GetCampaignContributionAvailabilityResult(
    Guid CampaignId,
    bool Exists,
    bool CanAcceptContributions,
    string Status);
