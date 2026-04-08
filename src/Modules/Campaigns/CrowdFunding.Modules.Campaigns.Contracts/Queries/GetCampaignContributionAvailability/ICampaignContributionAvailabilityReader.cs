namespace CrowdFunding.Modules.Campaigns.Contracts.Queries.GetCampaignContributionAvailability;

public interface ICampaignContributionAvailabilityReader
{
    Task<GetCampaignContributionAvailabilityResult> GetCampaignContributionAvailabilityAsync(
        GetCampaignContributionAvailabilityQuery query,
        CancellationToken cancellationToken);
}
