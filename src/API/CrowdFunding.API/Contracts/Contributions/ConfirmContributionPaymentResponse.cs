namespace CrowdFunding.API.Contracts.Contributions;

/// <summary>
/// Represents the HTTP response payload for Confirm Contribution Payment.
/// </summary>
public sealed record ConfirmContributionPaymentResponse(
    Guid ContributionId,
    string Status,
    string PaymentReference);
