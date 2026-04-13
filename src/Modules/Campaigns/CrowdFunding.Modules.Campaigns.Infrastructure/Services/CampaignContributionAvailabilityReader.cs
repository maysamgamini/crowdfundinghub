using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignContributionAvailability;
using CrowdFunding.Modules.Campaigns.Contracts.Queries.GetCampaignContributionAvailability;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Services;

/// <summary>
/// Provides query-side access for Campaign Contribution Availability.
/// </summary>
public sealed class CampaignContributionAvailabilityReader : ICampaignContributionAvailabilityReader
{
    private readonly GetCampaignContributionAvailabilityQueryHandler _handler;

    public CampaignContributionAvailabilityReader(GetCampaignContributionAvailabilityQueryHandler handler)
    {
        _handler = handler;
    }

    public Task<GetCampaignContributionAvailabilityResult> GetCampaignContributionAvailabilityAsync(
        GetCampaignContributionAvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        return _handler.Handle(query, cancellationToken);
    }
}
