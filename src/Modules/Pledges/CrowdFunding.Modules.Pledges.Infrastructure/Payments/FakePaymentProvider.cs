using System.Collections.Concurrent;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Pledges.Infrastructure.Payments;

/// <summary>
/// A stand-in for a real payment provider, with the one behaviour that matters for safety: the same
/// idempotency key returns the first answer, whether that answer was a success or a failure. Nothing is sent
/// anywhere. It lives in memory, so it forgets everything when the process restarts (a real provider would not).
/// </summary>
/// <remarks>
/// A saved card is a reference string, and the reference decides how charges behave, so a test or a learner
/// can pick the behaviour by choosing the "card":
/// <list type="bullet">
/// <item><c>pm_ok</c> (or any other value): every charge succeeds.</item>
/// <item><c>pm_decline</c>: every charge is declined with <c>card_declined</c>.</item>
/// <item><c>pm_decline_once</c>: the first attempt is declined, the next one with a new key succeeds.</item>
/// <item><c>pm_auth_required</c>: every charge is declined with <c>authentication_required</c> (the bank wants the cardholder).</item>
/// <item><c>pm_timeout_once</c>: the first call charges the card and then fails to answer (a timeout), so the caller does not know; the same key later returns the stored success.</item>
/// </list>
/// Tests also steer refunds with <see cref="FailRefundsWith"/> and read the call counters.
/// </remarks>
public sealed class FakePaymentProvider : IPaymentProvider
{
    private readonly ConcurrentDictionary<string, RefundResult> _refundsByKey = new();
    private readonly ConcurrentDictionary<string, ChargeResult> _chargesByKey = new();
    private readonly ConcurrentDictionary<string, SetupIntentResult> _setupsByKey = new();
    private readonly ConcurrentDictionary<string, int> _distinctChargeKeysPerMethod = new();
    private readonly ConcurrentDictionary<string, byte> _timedOutKeys = new();
    private string? _failureCodeForNewRefunds;
    private int _refundCalls;
    private int _chargeCalls;

    /// <summary>How many refund requests reached the provider, including repeats of a key it already knew.</summary>
    public int RefundCalls => Volatile.Read(ref _refundCalls);

    /// <summary>How many different refunds the provider actually performed.</summary>
    public int RefundsPerformed => _refundsByKey.Count(entry => entry.Value.Outcome == RefundOutcome.Succeeded);

    /// <summary>How many charge requests reached the provider, including repeats of a key it already knew.</summary>
    public int ChargeCalls => Volatile.Read(ref _chargeCalls);

    /// <summary>How many different charges succeeded. A pledge charged twice would show up here as two.</summary>
    public int ChargesPerformed => _chargesByKey.Count(entry => entry.Value.Outcome == ChargeOutcome.Succeeded);

    /// <summary>Every idempotency key a charge was answered for, so a test can see which attempts reached the provider.</summary>
    public IReadOnlyCollection<string> ChargeKeys => _chargesByKey.Keys.ToArray();

    /// <summary>Makes every refund the provider has not seen before fail with this code (null turns that off).</summary>
    public void FailRefundsWith(string? failureCode) => _failureCodeForNewRefunds = failureCode;

    /// <inheritdoc/>
    public Task<SetupIntentResult> CreateSetupIntentAsync(
        Guid pledgeId,
        Guid backerId,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => Task.FromResult(_setupsByKey.GetOrAdd(
            idempotencyKey,
            _ => new SetupIntentResult($"seti_mock_{pledgeId:N}", $"seti_mock_{pledgeId:N}_secret_{Guid.NewGuid():N}")));

    /// <inheritdoc/>
    public Task<ChargeResult> ChargeSavedCardAsync(
        string paymentMethodReference,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _chargeCalls);

        var result = _chargesByKey.GetOrAdd(idempotencyKey, _ => Decide(paymentMethodReference));

        // The charge happened, but the answer never arrived: the caller sees a timeout, not a result.
        if (paymentMethodReference.StartsWith("pm_timeout_once", StringComparison.Ordinal) && _timedOutKeys.TryAdd(idempotencyKey, 0))
        {
            throw new PaymentProviderException("The payment provider did not answer in time.");
        }

        return Task.FromResult(result);
    }

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

    private ChargeResult Decide(string paymentMethodReference)
    {
        var distinctKeys = _distinctChargeKeysPerMethod.AddOrUpdate(paymentMethodReference, 1, (_, count) => count + 1);

        if (paymentMethodReference.StartsWith("pm_auth_required", StringComparison.Ordinal))
        {
            return new ChargeResult(ChargeOutcome.Declined, null, "authentication_required");
        }

        if (paymentMethodReference.StartsWith("pm_decline_once", StringComparison.Ordinal) && distinctKeys == 1)
        {
            return new ChargeResult(ChargeOutcome.Declined, null, "card_declined");
        }

        if (paymentMethodReference.StartsWith("pm_decline", StringComparison.Ordinal) && !paymentMethodReference.StartsWith("pm_decline_once", StringComparison.Ordinal))
        {
            return new ChargeResult(ChargeOutcome.Declined, null, "card_declined");
        }

        return new ChargeResult(ChargeOutcome.Succeeded, $"ch_mock_{Guid.NewGuid():N}", null);
    }
}
