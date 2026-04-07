namespace CrowdFunding.API.Contracts.Contributions;

public sealed record ListContributionsResponse(
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
