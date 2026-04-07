using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;

namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Services;

public interface IContributionReadService
{
    Task<PagedResult<ListContributionsByCampaignResult>> ListByCampaignAsync(
        Guid campaignId,
        PageRequest pageRequest,
        ListContributionsByCampaignFilter filter,
        CancellationToken cancellationToken);
}
