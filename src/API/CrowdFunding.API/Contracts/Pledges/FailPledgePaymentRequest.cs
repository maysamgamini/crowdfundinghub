namespace CrowdFunding.API.Contracts.Pledges;

/// <summary>
/// Represents the HTTP request payload for recording a pledge payment failure.
/// </summary>
/// <param name="FailureReason">The descriptive reason why the payment failed.</param>
public sealed record FailPledgePaymentRequest(string FailureReason);
