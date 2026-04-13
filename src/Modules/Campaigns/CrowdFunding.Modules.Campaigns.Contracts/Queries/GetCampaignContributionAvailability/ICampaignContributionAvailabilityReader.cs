namespace CrowdFunding.Modules.Campaigns.Contracts.Queries.GetCampaignContributionAvailability;

/// <summary>
/// Defines cross-module reads for campaign contribution availability.
/// </summary>
public interface ICampaignContributionAvailabilityReader
{
    Task<GetCampaignContributionAvailabilityResult> GetCampaignContributionAvailabilityAsync(
        GetCampaignContributionAvailabilityQuery query,
        CancellationToken cancellationToken);
}
