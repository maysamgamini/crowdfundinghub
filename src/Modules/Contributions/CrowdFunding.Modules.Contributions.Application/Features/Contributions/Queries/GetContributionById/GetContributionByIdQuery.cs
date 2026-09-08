namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.GetContributionById;

/// <summary>
/// Represents the request to execute the Get Contribution By Id query — the single-resource read
/// a client needs after `POST .../contributions` to poll payment status, previously missing
/// entirely (QA TICKET-006).
/// </summary>
public sealed record GetContributionByIdQuery(Guid CampaignId, Guid ContributionId);
