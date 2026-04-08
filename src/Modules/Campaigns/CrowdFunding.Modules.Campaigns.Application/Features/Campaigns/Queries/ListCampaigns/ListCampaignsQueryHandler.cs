using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.ListCampaigns;

public sealed class ListCampaignsQueryHandler : IQueryHandler<ListCampaignsQuery, PagedResult<ListCampaignsResult>>
{
    private readonly ICampaignReadService _campaignReadService;

    public ListCampaignsQueryHandler(ICampaignReadService campaignReadService)
    {
        _campaignReadService = campaignReadService;
    }

    public async Task<PagedResult<ListCampaignsResult>> Handle(
        ListCampaignsQuery query,
        CancellationToken cancellationToken)
    {
        return await _campaignReadService.ListAsync(
            query.PageRequest,
            query.Filter,
            cancellationToken);
    }
}
