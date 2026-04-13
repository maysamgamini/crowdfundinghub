namespace CrowdFunding.API.Contracts.Contributions;

/// <summary>
/// Represents the HTTP request payload for Confirm Contribution Payment.
/// </summary>
public sealed record ConfirmContributionPaymentRequest(string PaymentReference);
