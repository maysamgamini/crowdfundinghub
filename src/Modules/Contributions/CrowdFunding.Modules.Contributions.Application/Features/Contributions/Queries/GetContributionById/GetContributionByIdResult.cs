namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.GetContributionById;

/// <summary>
/// Represents the outcome returned by Get Contribution By Id.
/// </summary>
public sealed record GetContributionByIdResult(
    Guid Id,
    Guid CampaignId,
    Guid ContributorId,
    decimal Amount,
    string Currency,
    string Status,
    string? PaymentReference,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc,
    string? ExternalPaymentIntentId);
