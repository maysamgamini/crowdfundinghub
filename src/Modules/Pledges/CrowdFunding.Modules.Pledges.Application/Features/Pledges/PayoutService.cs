using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Application.Metering;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Services;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using Microsoft.Extensions.Logging;

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges;

/// <summary>
/// Pays a succeeded campaign's creator: what was really charged, less the platform fee, exactly once.
/// </summary>
/// <remarks>
/// Two steps, each safe to repeat. First the amounts are fixed, once every pledge has settled (a card can still be
/// declined until then), and saved. Only then is the provider asked, with a key built from the campaign and the
/// attempt number: a timeout or a crash retries the same attempt with the same key and the same saved amount, so
/// the provider answers with the stored result instead of paying again. A refusal moves to the next attempt number.
/// The provider call is outside the database transaction for the same reason as in <see cref="PledgeRefunder"/>.
/// </remarks>
public sealed class PayoutService
{
    private readonly IPayoutStore _payoutStore;
    private readonly IPledgeTransactionExecutor _transactionExecutor;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IPaymentSettings _settings;
    private readonly IPledgeDateTimeProvider _dateTimeProvider;
    private readonly OpenMeterFeeOptions _feeOptions;
    private readonly ILogger<PayoutService> _logger;

    public PayoutService(
        IPayoutStore payoutStore,
        IPledgeTransactionExecutor transactionExecutor,
        IPaymentProvider paymentProvider,
        IPaymentSettings settings,
        IPledgeDateTimeProvider dateTimeProvider,
        OpenMeterFeeOptions feeOptions,
        ILogger<PayoutService> logger)
    {
        _payoutStore = payoutStore;
        _transactionExecutor = transactionExecutor;
        _paymentProvider = paymentProvider;
        _settings = settings;
        _dateTimeProvider = dateTimeProvider;
        _feeOptions = feeOptions;
        _logger = logger;
    }

    /// <summary>Works on up to <paramref name="batchSize"/> due payouts. Returns how many it looked at.</summary>
    public async Task<int> PayDueAsync(int batchSize, CancellationToken cancellationToken)
    {
        var due = await _payoutStore.GetDueAsync(_dateTimeProvider.UtcNow, batchSize, cancellationToken);

        foreach (var payout in due)
        {
            try
            {
                await ProcessAsync(payout, cancellationToken);
            }
            catch (PaymentProviderException exception)
            {
                _logger.LogWarning(
                    exception,
                    "The payment provider did not complete the payout for campaign {CampaignId}. It will be retried with the same key.",
                    payout.CampaignId);
            }
            catch (Exception exception) when (exception is ConcurrencyConflictException or PayoutLedgerLineAlreadyExistsException)
            {
                // Another worker changed this payout first (its ledger lines or its row); the next pass sees the result.
                _logger.LogInformation("Payout for campaign {CampaignId} was changed by another worker.", payout.CampaignId);
            }
        }

        return due.Count;
    }

    /// <summary>
    /// Records, in the payout ledger, every pledge that was counted in a payout and has been refunded since (a late
    /// payment refunded after the amounts were fixed, for example). The payout amount is never changed after it is
    /// fixed; the ledger then shows what the creator owes back. Safe to repeat: each line is written once.
    /// </summary>
    public async Task<int> RecordRefundsAfterPayoutAsync(int batchSize, CancellationToken cancellationToken)
    {
        var refunded = await _payoutStore.GetRefundedAfterPayoutAsync(batchSize, cancellationToken);

        foreach (var refund in refunded)
        {
            try
            {
                await _transactionExecutor.ExecuteAsync(async ct =>
                {
                    var now = _dateTimeProvider.UtcNow;
                    var lines = new List<PayoutLedgerEntry>
                    {
                        PayoutLedgerEntry.ChargeRefunded(refund.PayoutId, refund.PledgeId, refund.Amount, refund.Currency, now),
                    };

                    if (refund.PlatformFee > 0)
                    {
                        lines.Add(PayoutLedgerEntry.FeeReturned(refund.PayoutId, refund.PledgeId, refund.PlatformFee, refund.Currency, now));
                    }

                    await _payoutStore.AddLedgerEntriesAsync(lines, ct);
                }, cancellationToken);

                _logger.LogWarning(
                    "Pledge {PledgeId} was refunded after payout {PayoutId} counted it; the creator owes {Amount} back.",
                    refund.PledgeId,
                    refund.PayoutId,
                    refund.Amount - refund.PlatformFee);
            }
            catch (PayoutLedgerLineAlreadyExistsException)
            {
                // Another worker wrote the same lines first; nothing more to do.
            }
        }

        return refunded.Count;
    }

    private async Task ProcessAsync(Payout payout, CancellationToken cancellationToken)
    {
        if (payout.Status == PayoutStatus.Pending)
        {
            // The amount is unknown while any pledge is still waiting for its charge.
            if (await _payoutStore.CountUnsettledPledgesAsync(payout.CampaignId, cancellationToken) > 0)
            {
                return;
            }

            var charged = await _payoutStore.GetChargedPledgesAsync(payout.CampaignId, cancellationToken);

            await _transactionExecutor.ExecuteAsync(async ct =>
            {
                var now = _dateTimeProvider.UtcNow;
                payout.Prepare(charged.Select(x => x.Amount).ToArray(), _feeOptions.PlatformFeeBps);
                await _payoutStore.UpdateAsync(payout, ct);

                // The ledger gets one charge line, and one fee line, for each pledge the amounts were built from.
                var lines = new List<PayoutLedgerEntry>();
                foreach (var pledge in charged)
                {
                    lines.Add(PayoutLedgerEntry.Charge(payout.Id, pledge.PledgeId, pledge.Amount, payout.Currency, now));

                    var fee = PlatformFee.ForPledge(pledge.Amount, _feeOptions.PlatformFeeBps);
                    if (fee > 0)
                    {
                        lines.Add(PayoutLedgerEntry.PlatformFee(payout.Id, pledge.PledgeId, fee, payout.Currency, now));
                    }
                }

                await _payoutStore.AddLedgerEntriesAsync(lines, ct);
            }, cancellationToken);
        }

        if (payout.Status != PayoutStatus.Ready)
        {
            return;
        }

        var result = await _paymentProvider.PayoutAsync(
            $"creator:{payout.OwnerId:N}",
            payout.NetAmount!.Value,
            payout.Currency,
            Payout.KeyFor(payout.CampaignId, payout.Failures + 1),
            cancellationToken);

        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            var now = _dateTimeProvider.UtcNow;

            if (result.Outcome == PayoutOutcome.Succeeded)
            {
                payout.MarkPaid(result.ProviderPayoutId!, now);
                await _payoutStore.AddLedgerEntriesAsync(
                    [PayoutLedgerEntry.PaidOut(payout.Id, payout.NetAmount!.Value, payout.Currency, now)],
                    ct);
            }
            else
            {
                payout.RecordFailure(result.FailureCode ?? "unknown", now + _settings.PayoutRetryDelay);
            }

            await _payoutStore.UpdateAsync(payout, ct);
        }, cancellationToken);
    }
}
