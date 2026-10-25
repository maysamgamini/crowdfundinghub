namespace CrowdFunding.API.Contracts.Pledges;

/// <summary>
/// Represents the HTTP request payload for confirming payment of a pledge.
/// </summary>
/// <param name="PaymentReference">The external payment provider's transaction or settlement identifier.</param>
public sealed record ConfirmPledgePaymentRequest(string PaymentReference);
