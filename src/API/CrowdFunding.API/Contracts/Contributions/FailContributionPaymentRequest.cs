namespace CrowdFunding.API.Contracts.Contributions;

/// <summary>
/// Represents the HTTP request payload for Fail Contribution Payment.
/// </summary>
public sealed record FailContributionPaymentRequest(string FailureReason);
