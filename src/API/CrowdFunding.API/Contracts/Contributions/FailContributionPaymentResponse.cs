namespace CrowdFunding.API.Contracts.Contributions;

/// <summary>
/// Represents the HTTP response payload returned after recording payment failure.
/// </summary>
/// <param name="ContributionId">The unique identifier of the contribution.</param>
/// <param name="Status">The updated status of the contribution (Failed).</param>
/// <param name="FailureReason">The recorded reason for failure.</param>
public sealed record FailContributionPaymentResponse(
    Guid ContributionId,
    string Status,
    string FailureReason);
