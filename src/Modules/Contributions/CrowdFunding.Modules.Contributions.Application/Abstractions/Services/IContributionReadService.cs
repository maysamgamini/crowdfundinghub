using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;

namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Services;

/// <summary>
/// Defines read-model queries for contribution data.
/// </summary>
public interface IContributionReadService
{
    Task<PagedResult<ListContributionsByCampaignResult>> ListByCampaignAsync(
        Guid campaignId,
        PageRequest pageRequest,
        ListContributionsByCampaignFilter filter,
        CancellationToken cancellationToken);
}
