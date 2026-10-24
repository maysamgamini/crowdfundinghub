using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions;

/// <summary>
/// Refunds one contribution: asks the payment provider to return the money, then records the refund.
/// </summary>
/// <remarks>
/// The provider call is not inside the database transaction (a transaction held across a network call holds
/// locks while it waits, and cannot undo what the provider already did). The two steps are made safe to repeat
/// instead. The key is fixed per contribution, so if the process dies after the provider acted and before the
/// status changed, the retry sends the same key, the provider answers with the stored result, and the status
/// then changes. No refund is ever issued twice, and none is lost.
/// </remarks>
public sealed class ContributionRefunder
{
    private readonly IContributionRepository _contributionRepository;
    private readonly IContributionTransactionExecutor _transactionExecutor;
    private readonly IContributionDateTimeProvider _dateTimeProvider;
    private readonly IPaymentProvider _paymentProvider;

    public ContributionRefunder(
        IContributionRepository contributionRepository,
        IContributionTransactionExecutor transactionExecutor,
        IContributionDateTimeProvider dateTimeProvider,
        IPaymentProvider paymentProvider)
    {
        _contributionRepository = contributionRepository;
        _transactionExecutor = transactionExecutor;
        _dateTimeProvider = dateTimeProvider;
        _paymentProvider = paymentProvider;
    }

    /// <summary>
    /// The idempotency key for one refund attempt. A retry of the same attempt (after a timeout or a crash) uses the
    /// same key, so the provider cannot refund twice. A new attempt gets a new number, and so a new key, but only
    /// after the provider has answered the previous one with a failure: a provider replays a failed answer for a
    /// repeated key, so reusing the key would make the failure permanent.
    /// </summary>
    public static string RefundKey(Guid contributionId, int attemptNumber) => $"contribution:{contributionId:N}:refund:{attemptNumber}";

    public async Task RefundAsync(Contribution contribution, CancellationToken cancellationToken)
    {
        var result = await _paymentProvider.RefundAsync(
            contribution.ExternalPaymentIntentId ?? $"contribution:{contribution.Id:N}",
            contribution.Money.Amount,
            contribution.Money.Currency,
            RefundKey(contribution.Id, contribution.RefundFailures + 1),
            cancellationToken);

        if (result.Outcome != RefundOutcome.Succeeded)
        {
            // The provider answered "no". Remember it, so the next attempt is attempt n + 1 with a new key.
            await _transactionExecutor.ExecuteAsync(async ct =>
            {
                contribution.RecordRefundFailure(result.FailureCode ?? "unknown");
                await _contributionRepository.UpdateAsync(contribution, ct);
            }, cancellationToken);

            throw new PaymentProviderException(
                $"The payment provider did not refund contribution '{contribution.Id}': {result.FailureCode ?? "no reason given"}.");
        }

        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            contribution.Refund(_dateTimeProvider.UtcNow, result.ProviderRefundId);
            await _contributionRepository.UpdateAsync(contribution, ct);
        }, cancellationToken);
    }
}
