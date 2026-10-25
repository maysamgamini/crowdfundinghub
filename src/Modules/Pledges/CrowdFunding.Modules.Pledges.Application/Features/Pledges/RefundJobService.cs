using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Services;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Transactions;
using Microsoft.Extensions.Logging;

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges;

/// <summary>
/// Works the refund jobs: every paid pledge of a campaign that failed or was cancelled is refunded, a few at a time.
/// </summary>
/// <remarks>
/// Rate limited: one pass starts at most <see cref="IPaymentSettings.RefundsPerPass"/> refunds across all jobs, so a
/// campaign with half a million backers cannot flood the payment provider. Resumable: the progress is in the pledges
/// (still <c>Succeeded</c> or already <c>Refunded</c>), so a pass that stops anywhere, for any reason, is continued by
/// the next one, and a refund the provider refused waits out a retry delay and then gets a new key. A pledge whose
/// refunds were refused <see cref="IPaymentSettings.MaxRefundAttempts"/> times is left alone and the job asks for a person.
/// </remarks>
public sealed class RefundJobService
{
    private const int MaxJobsPerPass = 20;

    private readonly IRefundStore _refundStore;
    private readonly PledgeRefunder _refunder;
    private readonly IPledgeTransactionExecutor _transactionExecutor;
    private readonly IPledgeDateTimeProvider _dateTimeProvider;
    private readonly IPaymentSettings _settings;
    private readonly ILogger<RefundJobService> _logger;

    public RefundJobService(
        IRefundStore refundStore,
        PledgeRefunder refunder,
        IPledgeTransactionExecutor transactionExecutor,
        IPledgeDateTimeProvider dateTimeProvider,
        IPaymentSettings settings,
        ILogger<RefundJobService> logger)
    {
        _refundStore = refundStore;
        _refunder = refunder;
        _transactionExecutor = transactionExecutor;
        _dateTimeProvider = dateTimeProvider;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>Starts up to <paramref name="maxRefunds"/> refunds. Returns how many it started.</summary>
    public async Task<int> RefundDueAsync(int maxRefunds, CancellationToken cancellationToken)
    {
        var started = 0;
        var jobs = await _refundStore.GetOpenJobsAsync(MaxJobsPerPass, cancellationToken);

        foreach (var job in jobs)
        {
            var room = maxRefunds - started;
            if (room > 0)
            {
                var pledges = await _refundStore.GetRefundablePledgesAsync(
                    job.CampaignId, _dateTimeProvider.UtcNow, _settings.MaxRefundAttempts, room, cancellationToken);

                foreach (var pledge in pledges)
                {
                    started++;
                    await TryRefundAsync(pledge, cancellationToken);
                }
            }

            await FinishIfDoneAsync(job, cancellationToken);
        }

        return started;
    }

    private async Task TryRefundAsync(Domain.Aggregates.Pledge pledge, CancellationToken cancellationToken)
    {
        try
        {
            await _refunder.RefundAsync(pledge, cancellationToken);
        }
        catch (PaymentProviderException exception)
        {
            // The provider refused or did not answer. A refusal is recorded and waits out the retry delay; with no
            // answer nothing was recorded, so the next pass sends the same attempt under the same key.
            _logger.LogWarning(exception, "Refund of pledge {PledgeId} did not complete; it will be retried.", pledge.Id);
        }
        catch (Exception exception) when (exception is ConcurrencyConflictException or RefundAttemptAlreadyRecordedException)
        {
            // Another worker handled this pledge first; the next pass sees the result.
            _logger.LogInformation("Pledge {PledgeId} was refunded or changed by another worker.", pledge.Id);
        }
    }

    private async Task FinishIfDoneAsync(Domain.Aggregates.RefundJob job, CancellationToken cancellationToken)
    {
        var progress = await _refundStore.GetProgressAsync(job.CampaignId, _settings.MaxRefundAttempts, cancellationToken);

        if (progress.Remaining > 0 && progress.Remaining > progress.Exhausted)
        {
            return;
        }

        try
        {
            await _transactionExecutor.ExecuteAsync(async ct =>
            {
                var now = _dateTimeProvider.UtcNow;
                if (progress.Remaining == 0)
                {
                    job.Complete(now);
                }
                else
                {
                    job.NeedAttention(now);
                    _logger.LogError(
                        "Refund job for campaign {CampaignId} needs attention: {Exhausted} pledges used up their refund attempts.",
                        job.CampaignId,
                        progress.Exhausted);
                }

                await _refundStore.UpdateJobAsync(job, ct);
            }, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Another worker finished the job first.
        }
    }
}
