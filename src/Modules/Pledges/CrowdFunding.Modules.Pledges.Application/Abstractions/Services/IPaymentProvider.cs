namespace CrowdFunding.Modules.Pledges.Application.Abstractions.Services;

/// <summary>
/// The payment service provider (a Stripe-shaped boundary). Everything that moves money goes through here.
/// Every call carries an idempotency key, and a provider that receives the same key twice returns the first
/// result instead of acting twice: that is what makes it safe to retry after a timeout or a crash.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>
    /// Starts saving a backer's card for a later charge. Nothing is charged. The backer's browser finishes the
    /// step with the provider directly using the returned secret, so card details never reach this system.
    /// </summary>
    Task<SetupIntentResult> CreateSetupIntentAsync(
        Guid pledgeId,
        Guid backerId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Charges a saved card while the backer is away (off-session). A repeated <paramref name="idempotencyKey"/>
    /// returns the stored answer, a decline included, so a retry of the same attempt cannot charge twice. A call
    /// that cannot be completed (a timeout) throws <see cref="PaymentProviderException"/>: the answer is unknown,
    /// and the caller retries the SAME attempt with the same key.
    /// </summary>
    Task<ChargeResult> ChargeSavedCardAsync(
        string paymentMethodReference,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns money for a payment. A repeated <paramref name="idempotencyKey"/> returns the stored result,
    /// success or failure alike, so a retry of the same attempt never refunds twice.
    /// </summary>
    Task<RefundResult> RefundAsync(
        string paymentReference,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

/// <summary>The provider's answer to starting a card save.</summary>
public sealed record SetupIntentResult(string SetupIntentId, string ClientSecret);

/// <summary>The provider's answer to a charge.</summary>
/// <param name="Outcome">Whether the card was charged.</param>
/// <param name="ProviderPaymentId">The provider's id for the payment, when the charge went through.</param>
/// <param name="DeclineCode">The provider's reason for a decline, such as <c>card_declined</c> or <c>authentication_required</c>.</param>
public sealed record ChargeResult(ChargeOutcome Outcome, string? ProviderPaymentId, string? DeclineCode);

public enum ChargeOutcome
{
    Succeeded = 1,
    Declined = 2,
}

/// <summary>What the provider answered to a refund.</summary>
/// <param name="Outcome">Whether the refund went through.</param>
/// <param name="ProviderRefundId">The provider's id for the refund, when there is one.</param>
/// <param name="FailureCode">The provider's reason when the refund did not go through.</param>
public sealed record RefundResult(RefundOutcome Outcome, string? ProviderRefundId, string? FailureCode);

public enum RefundOutcome
{
    Succeeded = 1,
    Failed = 2,
}

/// <summary>The provider could not complete a call; the caller retries later with the same key.</summary>
public sealed class PaymentProviderException : Exception
{
    public PaymentProviderException(string message)
        : base(message)
    {
    }
}
