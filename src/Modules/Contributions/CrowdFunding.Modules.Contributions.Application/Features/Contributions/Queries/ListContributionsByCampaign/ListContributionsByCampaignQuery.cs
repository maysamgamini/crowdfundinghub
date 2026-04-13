using CrowdFunding.BuildingBlocks.Application.Pagination;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;

/// <summary>
/// Represents the request to execute the List Contributions By Campaign query.
/// </summary>
public sealed record ListContributionsByCampaignQuery(
    Guid CampaignId,
    PageRequest PageRequest,
    ListContributionsByCampaignFilter Filter);
