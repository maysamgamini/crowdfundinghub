namespace CrowdFunding.API.Contracts.Contributions;

/// <summary>
/// Represents the HTTP request payload for confirming payment of a contribution.
/// </summary>
/// <param name="PaymentReference">The external payment provider's transaction or settlement identifier.</param>
public sealed record ConfirmContributionPaymentRequest(string PaymentReference);
