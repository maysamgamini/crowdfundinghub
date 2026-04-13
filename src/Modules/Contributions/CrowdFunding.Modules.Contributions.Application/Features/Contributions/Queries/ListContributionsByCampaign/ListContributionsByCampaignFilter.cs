namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;

/// <summary>
/// Represents filter criteria for List Contributions By Campaign.
/// </summary>
public sealed record ListContributionsByCampaignFilter(
    Guid? ContributorId,
    string? Currency,
    string? Status);
