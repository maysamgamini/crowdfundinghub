namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Services;

/// <summary>
/// The payment service provider (a Stripe-shaped boundary). Everything that moves money goes through here.
/// Every call carries an idempotency key, and a provider that receives the same key twice returns the first
/// result instead of acting twice: that is what makes it safe to retry after a timeout or a crash.
/// </summary>
public interface IPaymentProvider
{
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
