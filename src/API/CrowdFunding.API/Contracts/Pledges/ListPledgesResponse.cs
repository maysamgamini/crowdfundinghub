namespace CrowdFunding.API.Contracts.Pledges;

/// <summary>
/// Represents a pledge item within a paginated list response.
/// </summary>
/// <param name="Id">The unique identifier of the pledge.</param>
/// <param name="CampaignId">The unique identifier of the associated campaign.</param>
/// <param name="BackerId">The unique identifier of the backer user.</param>
/// <param name="Amount">The pledged financial amount.</param>
/// <param name="Currency">The currency code of the pledge.</param>
/// <param name="Status">The current status of the pledge (Pending, Succeeded, Failed).</param>
/// <param name="PaymentReference">The optional payment gateway transaction reference.</param>
/// <param name="FailureReason">The optional reason why payment processing failed.</param>
/// <param name="CreatedAtUtc">The UTC timestamp when the pledge was created.</param>
/// <param name="ProcessedAtUtc">The UTC timestamp when payment processing concluded.</param>
public sealed record ListPledgesResponse(
    Guid Id,
    Guid CampaignId,
    Guid BackerId,
    decimal Amount,
    string Currency,
    string Status,
    string? PaymentReference,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc);
