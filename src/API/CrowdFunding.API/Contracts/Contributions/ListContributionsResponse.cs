namespace CrowdFunding.API.Contracts.Contributions;

/// <summary>
/// Represents the HTTP response payload for List Contributions.
/// </summary>
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
