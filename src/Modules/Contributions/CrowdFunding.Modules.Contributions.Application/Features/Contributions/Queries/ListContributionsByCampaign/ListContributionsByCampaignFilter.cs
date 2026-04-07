namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;

public sealed record ListContributionsByCampaignFilter(
    Guid? ContributorId,
    string? Currency);
