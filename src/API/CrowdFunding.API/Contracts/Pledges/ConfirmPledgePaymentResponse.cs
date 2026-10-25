namespace CrowdFunding.API.Contracts.Pledges;

/// <summary>
/// Represents the HTTP response payload returned after confirming pledge payment.
/// </summary>
/// <param name="PledgeId">The unique identifier of the pledge.</param>
/// <param name="Status">The updated status of the pledge (Succeeded).</param>
/// <param name="PaymentReference">The external payment reference associated with the transaction.</param>
public sealed record ConfirmPledgePaymentResponse(
    Guid PledgeId,
    string Status,
    string PaymentReference);
