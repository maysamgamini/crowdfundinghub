using CrowdFunding.BuildingBlocks.Application.Pagination;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;

public sealed record ListContributionsByCampaignQuery(
    Guid CampaignId,
    PageRequest PageRequest,
    ListContributionsByCampaignFilter Filter);
