namespace CrowdFunding.API.Contracts.Pledges;

/// <summary>
/// Represents the HTTP response payload returned after recording payment failure.
/// </summary>
/// <param name="PledgeId">The unique identifier of the pledge.</param>
/// <param name="Status">The updated status of the pledge (Failed).</param>
/// <param name="FailureReason">The recorded reason for failure.</param>
public sealed record FailPledgePaymentResponse(
    Guid PledgeId,
    string Status,
    string FailureReason);
