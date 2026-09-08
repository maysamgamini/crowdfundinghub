namespace CrowdFunding.API.Contracts.Contributions;

/// <summary>
/// Represents the HTTP request payload for recording a contribution payment failure.
/// </summary>
/// <param name="FailureReason">The descriptive reason why the payment failed.</param>
public sealed record FailContributionPaymentRequest(string FailureReason);
