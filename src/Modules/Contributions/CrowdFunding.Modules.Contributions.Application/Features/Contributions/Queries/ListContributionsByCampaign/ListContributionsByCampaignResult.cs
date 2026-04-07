namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;

public sealed record ListContributionsByCampaignResult(
    Guid Id,
    Guid CampaignId,
    Guid ContributorId,
    decimal Amount,
    string Currency,
    string Status,
    string? PaymentReference,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc);
