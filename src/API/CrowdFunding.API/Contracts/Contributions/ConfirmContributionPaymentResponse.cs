namespace CrowdFunding.API.Contracts.Contributions;

/// <summary>
/// Represents the HTTP response payload returned after confirming contribution payment.
/// </summary>
/// <param name="ContributionId">The unique identifier of the contribution.</param>
/// <param name="Status">The updated status of the contribution (Succeeded).</param>
/// <param name="PaymentReference">The external payment reference associated with the transaction.</param>
public sealed record ConfirmContributionPaymentResponse(
    Guid ContributionId,
    string Status,
    string PaymentReference);
