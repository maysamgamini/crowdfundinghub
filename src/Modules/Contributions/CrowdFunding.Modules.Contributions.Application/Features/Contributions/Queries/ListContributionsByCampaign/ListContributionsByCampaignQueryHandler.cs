using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;

public sealed class ListContributionsByCampaignQueryHandler
{
    private readonly IContributionReadService _contributionReadService;

    public ListContributionsByCampaignQueryHandler(IContributionReadService contributionReadService)
    {
        _contributionReadService = contributionReadService;
    }

    public async Task<PagedResult<ListContributionsByCampaignResult>> Handle(
        ListContributionsByCampaignQuery query,
        CancellationToken cancellationToken)
    {
        return await _contributionReadService.ListByCampaignAsync(
            query.CampaignId,
            query.PageRequest,
            cancellationToken);
    }
}
