using CrowdFunding.BuildingBlocks.Application.Pagination;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.ListCampaigns;

/// <summary>
/// Represents the request to execute the List Campaigns query.
/// </summary>
public sealed record ListCampaignsQuery(
    PageRequest PageRequest,
    ListCampaignsFilter Filter);
