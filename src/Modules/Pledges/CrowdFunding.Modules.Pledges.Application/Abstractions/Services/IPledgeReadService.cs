using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.GetPledgeById;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.ListPledgesByCampaign;

namespace CrowdFunding.Modules.Pledges.Application.Abstractions.Services;

/// <summary>
/// Defines read-model queries for pledge data.
/// </summary>
public interface IPledgeReadService
{
    Task<PagedResult<ListPledgesByCampaignResult>> ListByCampaignAsync(
        Guid campaignId,
        PageRequest pageRequest,
        ListPledgesByCampaignFilter filter,
        CancellationToken cancellationToken);

    Task<GetPledgeByIdResult?> GetByIdAsync(Guid campaignId, Guid pledgeId, CancellationToken cancellationToken);
}
