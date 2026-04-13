namespace CrowdFunding.API.Contracts.Contributions;

/// <summary>
/// Represents the HTTP response payload for Fail Contribution Payment.
/// </summary>
public sealed record FailContributionPaymentResponse(
    Guid ContributionId,
    string Status,
    string FailureReason);
