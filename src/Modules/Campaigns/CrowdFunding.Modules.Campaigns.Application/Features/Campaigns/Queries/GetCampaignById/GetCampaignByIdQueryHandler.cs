using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;

public sealed class GetCampaignByIdQueryHandler : IQueryHandler<GetCampaignByIdQuery, GetCampaignByIdResult>
{
    private readonly ICampaignReadService _campaignReadService;

    public GetCampaignByIdQueryHandler(ICampaignReadService campaignReadService)
    {
        _campaignReadService = campaignReadService;
    }

    public async Task<GetCampaignByIdResult> Handle(
        GetCampaignByIdQuery query,
        CancellationToken cancellationToken)
    {
        var campaign = await _campaignReadService.GetByIdAsync(query.CampaignId, cancellationToken);

        if (campaign is null)
        {
            throw new KeyNotFoundException($"Campaign with id '{query.CampaignId}' was not found.");
        }

        return campaign;
    }
}