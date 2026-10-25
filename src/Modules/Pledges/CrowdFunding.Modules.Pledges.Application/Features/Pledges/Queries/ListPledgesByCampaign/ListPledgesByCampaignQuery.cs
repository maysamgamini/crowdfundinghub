using CrowdFunding.BuildingBlocks.Application.Pagination;

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.ListPledgesByCampaign;

/// <summary>
/// Represents the request to execute the List Pledges By Campaign query.
/// </summary>
public sealed record ListPledgesByCampaignQuery(
    Guid CampaignId,
    PageRequest PageRequest,
    ListPledgesByCampaignFilter Filter);
