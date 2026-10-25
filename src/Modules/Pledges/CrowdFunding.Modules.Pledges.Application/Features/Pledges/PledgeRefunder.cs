using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Services;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges;

/// <summary>
/// Refunds one pledge: asks the payment provider to return the money, then records the refund.
/// </summary>
/// <remarks>
/// The provider call is not inside the database transaction (a transaction held across a network call holds
/// locks while it waits, and cannot undo what the provider already did). The two steps are made safe to repeat
/// instead. The key is fixed per pledge, so if the process dies after the provider acted and before the
/// status changed, the retry sends the same key, the provider answers with the stored result, and the status
/// then changes. No refund is ever issued twice, and none is lost.
/// </remarks>
public sealed class PledgeRefunder
{
    private readonly IPledgeRepository _pledgeRepository;
    private readonly IPledgeTransactionExecutor _transactionExecutor;
    private readonly IPledgeDateTimeProvider _dateTimeProvider;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IRefundStore _refundStore;
    private readonly IPaymentSettings _settings;

    public PledgeRefunder(
        IPledgeRepository pledgeRepository,
        IPledgeTransactionExecutor transactionExecutor,
        IPledgeDateTimeProvider dateTimeProvider,
        IPaymentProvider paymentProvider,
        IRefundStore refundStore,
        IPaymentSettings settings)
    {
        _pledgeRepository = pledgeRepository;
        _transactionExecutor = transactionExecutor;
        _dateTimeProvider = dateTimeProvider;
        _paymentProvider = paymentProvider;
        _refundStore = refundStore;
        _settings = settings;
    }

    /// <summary>
    /// The idempotency key for one refund attempt. A retry of the same attempt (after a timeout or a crash) uses the
    /// same key, so the provider cannot refund twice. A new attempt gets a new number, and so a new key, but only
    /// after the provider has answered the previous one with a failure: a provider replays a failed answer for a
    /// repeated key, so reusing the key would make the failure permanent.
    /// </summary>
    public static string RefundKey(Guid pledgeId, int attemptNumber) => $"pledge:{pledgeId:N}:refund:{attemptNumber}";

    public async Task RefundAsync(Pledge pledge, CancellationToken cancellationToken)
    {
        var attemptNumber = pledge.RefundFailures + 1;
        var key = RefundKey(pledge.Id, attemptNumber);

        var result = await _paymentProvider.RefundAsync(
            // A pledge charged at the deadline is refunded against the provider's payment (the charge);
            // one paid at confirmation against its payment intent.
            (pledge.PaymentMethodReference is not null ? pledge.PaymentReference : pledge.ExternalPaymentIntentId)
                ?? $"pledge:{pledge.Id:N}",
            pledge.Money.Amount,
            pledge.Money.Currency,
            key,
            cancellationToken);

        if (result.Outcome != RefundOutcome.Succeeded)
        {
            // The provider answered "no". Remember it, so the next attempt is attempt n + 1 with a new key, and keep a
            // record of the attempt and a time before which it is not tried again.
            var failureCode = result.FailureCode ?? "unknown";
            await _transactionExecutor.ExecuteAsync(async ct =>
            {
                var now = _dateTimeProvider.UtcNow;
                pledge.RecordRefundFailure(failureCode, now + _settings.RefundRetryDelay);
                await _pledgeRepository.UpdateAsync(pledge, ct);
                await _refundStore.AddAttemptAsync(RefundAttempt.Failed(pledge.Id, attemptNumber, key, failureCode, now), ct);
            }, cancellationToken);

            throw new PaymentProviderException(
                $"The payment provider did not refund pledge '{pledge.Id}': {result.FailureCode ?? "no reason given"}.");
        }

        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            var now = _dateTimeProvider.UtcNow;
            pledge.Refund(now, result.ProviderRefundId);
            await _pledgeRepository.UpdateAsync(pledge, ct);
            await _refundStore.AddAttemptAsync(RefundAttempt.Succeeded(pledge.Id, attemptNumber, key, result.ProviderRefundId, now), ct);
        }, cancellationToken);
    }
}
