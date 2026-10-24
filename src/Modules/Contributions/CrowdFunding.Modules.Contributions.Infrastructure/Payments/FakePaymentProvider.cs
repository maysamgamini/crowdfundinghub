using System.Collections.Concurrent;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Contributions.Infrastructure.Payments;

/// <summary>
/// A stand-in for a real payment provider, with the one behaviour that matters for safety: the same
/// idempotency key returns the first answer, whether that answer was a success or a failure. Nothing is sent
/// anywhere. It lives in memory, so it forgets everything when the process restarts (a real provider would not).
/// </summary>
/// <remarks>
/// Tests steer it with <see cref="FailRefundsWith"/> to make a refund fail, and read <see cref="RefundCalls"/>
/// to see how often the provider was really asked. A key that has an answer is not asked again, which is how a
/// real provider behaves when a client retries.
/// </remarks>
public sealed class FakePaymentProvider : IPaymentProvider
{
    private readonly ConcurrentDictionary<string, RefundResult> _refundsByKey = new();
    private string? _failureCodeForNewRefunds;
    private int _refundCalls;

    /// <summary>How many refund requests reached the provider, including repeats of a key it already knew.</summary>
    public int RefundCalls => Volatile.Read(ref _refundCalls);

    /// <summary>How many different refunds the provider actually performed.</summary>
    public int RefundsPerformed => _refundsByKey.Count(entry => entry.Value.Outcome == RefundOutcome.Succeeded);

    /// <summary>Makes every refund the provider has not seen before fail with this code (null turns that off).</summary>
    public void FailRefundsWith(string? failureCode) => _failureCodeForNewRefunds = failureCode;

    /// <inheritdoc/>
    public Task<RefundResult> RefundAsync(
        string paymentReference,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _refundCalls);

        var result = _refundsByKey.GetOrAdd(idempotencyKey, _ => _failureCodeForNewRefunds is { } code
            ? new RefundResult(RefundOutcome.Failed, null, code)
            : new RefundResult(RefundOutcome.Succeeded, $"re_mock_{Guid.NewGuid():N}", null));

        return Task.FromResult(result);
    }
}
