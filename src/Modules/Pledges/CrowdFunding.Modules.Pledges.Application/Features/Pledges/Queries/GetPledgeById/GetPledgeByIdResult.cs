namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.GetPledgeById;

/// <summary>
/// Represents the outcome returned by Get Pledge By Id.
/// </summary>
public sealed record GetPledgeByIdResult(
    Guid Id,
    Guid CampaignId,
    Guid BackerId,
    decimal Amount,
    string Currency,
    string Status,
    string? PaymentReference,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc,
    string? ExternalPaymentIntentId);
