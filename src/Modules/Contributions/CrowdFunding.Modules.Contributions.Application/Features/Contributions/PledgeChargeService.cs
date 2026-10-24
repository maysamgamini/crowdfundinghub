using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using Microsoft.Extensions.Logging;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions;

/// <summary>
/// Charges the pledges whose campaign was funded, each exactly once in effect: at most one successful charge per
/// pledge, however many workers, crashes and timeouts there are.
/// </summary>
/// <remarks>
/// How that holds. A pledge is claimed with a lease (several workers never take the same one). Each charge is a
/// NUMBERED attempt, and the attempt's record is committed in flight BEFORE the provider is called. The provider
/// is asked with a key built from the pledge and the attempt number: a retry of the same attempt (a timeout, or a
/// worker that died and whose lease ran out) sends the same key, so the provider answers with the stored result
/// instead of charging again. A NEW attempt starts only after the previous one was answered with a decline, and gets a
/// new key, because a provider replays a failed answer for a repeated key. The result is recorded by a state change
/// that only works from <c>Charging</c>, and the database allows one successful attempt per pledge.
/// </remarks>
public sealed class PledgeChargeService
{
    private readonly IPledgeSettlementStore _store;
    private readonly IContributionRepository _contributionRepository;
    private readonly IContributionTransactionExecutor _transactionExecutor;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IPaymentSettings _settings;
    private readonly IContributionDateTimeProvider _dateTimeProvider;
    private readonly ILogger<PledgeChargeService> _logger;

    public PledgeChargeService(
        IPledgeSettlementStore store,
        IContributionRepository contributionRepository,
        IContributionTransactionExecutor transactionExecutor,
        IPaymentProvider paymentProvider,
        IPaymentSettings settings,
        IContributionDateTimeProvider dateTimeProvider,
        ILogger<PledgeChargeService> logger)
    {
        _store = store;
        _contributionRepository = contributionRepository;
        _transactionExecutor = transactionExecutor;
        _paymentProvider = paymentProvider;
        _settings = settings;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    /// <summary>Claims up to <paramref name="batchSize"/> due pledges and charges them. Returns how many were claimed.</summary>
    public async Task<int> ChargeDueAsync(int batchSize, CancellationToken cancellationToken)
    {
        var claimed = await _store.ClaimDueForChargeAsync(_dateTimeProvider.UtcNow, _settings.ChargeLease, batchSize, cancellationToken);

        foreach (var pledge in claimed)
        {
            try
            {
                await ChargeAsync(pledge, cancellationToken);
            }
            catch (PaymentProviderException exception)
            {
                // The provider did not answer: we do not know whether the card was charged. Leave the attempt in
                // flight. When the lease runs out the same attempt is resumed with the same key.
                _logger.LogWarning(
                    exception,
                    "No answer from the payment provider for pledge {ContributionId}, attempt {Attempt}. It will be resumed with the same key when its lease ends.",
                    pledge.Id, pledge.ChargeAttempts);
            }
        }

        return claimed.Count;
    }

    private async Task ChargeAsync(Contribution pledge, CancellationToken cancellationToken)
    {
        var attemptNumber = pledge.ChargeAttempts;
        ChargeAttempt? attempt = null;

        // Commit the attempt record BEFORE calling the provider, so a crash after the call still has a record.
        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            attempt = await _store.GetOrCreateAttemptAsync(pledge.Id, attemptNumber, _dateTimeProvider.UtcNow, ct);
        }, cancellationToken);

        var result = await _paymentProvider.ChargeSavedCardAsync(
            pledge.PaymentMethodReference ?? throw new InvalidOperationException($"Pledge '{pledge.Id}' has no saved card."),
            pledge.Money.Amount,
            pledge.Money.Currency,
            attempt!.ProviderKey,
            cancellationToken);

        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            var now = _dateTimeProvider.UtcNow;

            if (result.Outcome == ChargeOutcome.Succeeded)
            {
                attempt.Succeed(result.ProviderPaymentId!);
                pledge.RecordChargeSucceeded(result.ProviderPaymentId!, now);
            }
            else
            {
                attempt.Fail(result.DeclineCode ?? "declined");
                pledge.RecordChargeDeclined(result.DeclineCode ?? "declined", _settings.MaxChargeAttempts, _settings.ChargeRetryDelay, now);
            }

            await _contributionRepository.UpdateAsync(pledge, ct);
        }, cancellationToken);
    }
}
