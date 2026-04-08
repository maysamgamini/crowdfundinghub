using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Contracts.Queries.GetCampaignContributionAvailability;
using CrowdFunding.Modules.Campaigns.Domain.Enums;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignContributionAvailability;

public sealed class GetCampaignContributionAvailabilityQueryHandler : IQueryHandler<GetCampaignContributionAvailabilityQuery, GetCampaignContributionAvailabilityResult>
{
    private readonly ICampaignRepository _campaignRepository;

    public GetCampaignContributionAvailabilityQueryHandler(ICampaignRepository campaignRepository)
    {
        _campaignRepository = campaignRepository;
    }

    public async Task<GetCampaignContributionAvailabilityResult> Handle(
        GetCampaignContributionAvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        var campaign = await _campaignRepository.GetByIdAsync(query.CampaignId, cancellationToken);

        if (campaign is null)
        {
            return new GetCampaignContributionAvailabilityResult(query.CampaignId, false, false, "Missing");
        }

        return new GetCampaignContributionAvailabilityResult(
            campaign.Id,
            true,
            campaign.Status == CampaignStatus.Published,
            campaign.Status.ToString());
    }
}
