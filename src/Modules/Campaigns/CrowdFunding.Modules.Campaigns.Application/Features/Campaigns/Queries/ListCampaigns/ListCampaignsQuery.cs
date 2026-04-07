using CrowdFunding.BuildingBlocks.Application.Pagination;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.ListCampaigns;

public sealed record ListCampaignsQuery(
    PageRequest PageRequest,
    ListCampaignsFilter Filter);
