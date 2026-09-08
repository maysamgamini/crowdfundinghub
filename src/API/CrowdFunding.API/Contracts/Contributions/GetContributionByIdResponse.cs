namespace CrowdFunding.API.Contracts.Contributions;

/// <summary>
/// Represents the HTTP response payload for a single contribution resource.
/// </summary>
/// <param name="Id">The unique identifier of the contribution.</param>
/// <param name="CampaignId">The unique identifier of the associated campaign.</param>
/// <param name="ContributorId">The unique identifier of the contributor user.</param>
/// <param name="Amount">The contributed financial amount.</param>
/// <param name="Currency">The currency code of the contribution.</param>
/// <param name="Status">The current status of the contribution (Pending, Succeeded, Failed).</param>
/// <param name="PaymentReference">The optional payment gateway transaction reference.</param>
/// <param name="FailureReason">The optional reason why payment processing failed.</param>
/// <param name="CreatedAtUtc">The UTC timestamp when the contribution was created.</param>
/// <param name="ProcessedAtUtc">The UTC timestamp when payment processing concluded.</param>
public sealed record GetContributionByIdResponse(
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
