namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.ListPledgesByCampaign;

/// <summary>
/// Represents filter criteria for List Pledges By Campaign.
/// </summary>
public sealed record ListPledgesByCampaignFilter(
    Guid? BackerId,
    string? Currency,
    string? Status);
