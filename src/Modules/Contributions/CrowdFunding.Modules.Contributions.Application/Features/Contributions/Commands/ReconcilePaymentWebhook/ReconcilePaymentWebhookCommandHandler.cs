using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Contributions.Domain.Enums;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ReconcilePaymentWebhook;

/// <summary>
/// Reconciles one payment-gateway webhook delivery against the contribution it correlates to.
/// Deliberately anonymous at the API layer (a payment gateway cannot present a user JWT) — see
/// the controller for signature verification, which is this endpoint's actual authentication.
/// See TICKET-033.
/// </summary>
public sealed class ReconcilePaymentWebhookCommandHandler : ICommandHandler<ReconcilePaymentWebhookCommand, ReconcilePaymentWebhookResult>
{
    private const string PaymentSucceededEventType = "payment_intent.succeeded";
    private const string PaymentFailedEventType = "payment_intent.payment_failed";

    private readonly IContributionRepository _contributionRepository;
    private readonly IPaymentWebhookIdempotencyStore _idempotencyStore;
    private readonly IActiveCampaignCacheRepository _activeCampaignCacheRepository;
    private readonly IContributionDateTimeProvider _dateTimeProvider;
    private readonly IContributionTransactionExecutor _transactionExecutor;

    public ReconcilePaymentWebhookCommandHandler(
        IContributionRepository contributionRepository,
        IPaymentWebhookIdempotencyStore idempotencyStore,
        IActiveCampaignCacheRepository activeCampaignCacheRepository,
        IContributionDateTimeProvider dateTimeProvider,
        IContributionTransactionExecutor transactionExecutor)
    {
        _contributionRepository = contributionRepository;
        _idempotencyStore = idempotencyStore;
        _activeCampaignCacheRepository = activeCampaignCacheRepository;
        _dateTimeProvider = dateTimeProvider;
        _transactionExecutor = transactionExecutor;
    }

    public Task<ReconcilePaymentWebhookResult> Handle(ReconcilePaymentWebhookCommand command, CancellationToken cancellationToken)
        => _transactionExecutor.ExecuteAsync(ct => HandleWithinTransactionAsync(command, ct), cancellationToken);

    private async Task<ReconcilePaymentWebhookResult> HandleWithinTransactionAsync(
        ReconcilePaymentWebhookCommand command, CancellationToken cancellationToken)
    {
        // Idempotent redelivery (TICKET-033 criterion #1): the gateway retries a webhook for up
        // to 72 hours on any non-2xx response, and can also simply redeliver the same event more
        // than once under normal operation. Checked and recorded inside the same transaction as
        // the state transition itself, not just before/after it, so two concurrent deliveries of
        // the same event can't both pass the check before either has recorded it.
        if (await _idempotencyStore.IsProcessedAsync(command.EventId, cancellationToken))
        {
            return new ReconcilePaymentWebhookResult(null, "Ignored");
        }

        var contribution = await _contributionRepository.GetByExternalPaymentIntentIdAsync(command.PaymentIntentId, cancellationToken);
        if (contribution is null)
        {
            throw new KeyNotFoundException($"No contribution found for payment intent '{command.PaymentIntentId}'.");
        }

        if (contribution.Status == ContributionStatus.Pending)
        {
            switch (command.EventType)
            {
                case PaymentSucceededEventType:
                    contribution.ConfirmPayment(command.EventId, _dateTimeProvider.UtcNow);
                    await _contributionRepository.UpdateAsync(contribution, cancellationToken);

                    // Mid-flight cancellation (TICKET-033 §1, "Critical Race Conditions"): the
                    // campaign may have been cancelled while this payment was still in flight at
                    // the gateway. The refund saga (TICKET-027) fires exactly once, off
                    // CampaignCancelledApplicationEvent, for contributions that were already
                    // Succeeded at that moment — a contribution that only reaches Succeeded
                    // *after* cancellation would otherwise never be refunded. Refunding it
                    // immediately, in the same transaction as confirming it, closes that gap.
                    var campaign = await _activeCampaignCacheRepository.GetAsync(contribution.CampaignId, cancellationToken);
                    if (campaign is not null && !campaign.IsActive)
                    {
                        contribution.Refund(_dateTimeProvider.UtcNow);
                        await _contributionRepository.UpdateAsync(contribution, cancellationToken);
                    }

                    break;

                case PaymentFailedEventType:
                    contribution.FailPayment($"Payment gateway reported failure (event {command.EventId}).", _dateTimeProvider.UtcNow);
                    await _contributionRepository.UpdateAsync(contribution, cancellationToken);
                    break;

                default:
                    // An event type this module doesn't act on (e.g. a Stripe event unrelated to
                    // this pledge's lifecycle) — acknowledged, not an error, no transition.
                    break;
            }
        }
        // A non-Pending contribution receiving a webhook is not an error either — most likely
        // this exact event already produced the transition under a redelivery this handler
        // hasn't seen recorded yet (the idempotency row and the state change commit atomically,
        // so this should be rare), or a duplicate distinct event id describing the same fact.
        // Either way, re-running ConfirmPayment/FailPayment on a non-Pending contribution would
        // throw — silently no-op instead and still record the event as processed below.

        await _idempotencyStore.MarkProcessedAsync(command.EventId, command.PaymentIntentId, _dateTimeProvider.UtcNow, cancellationToken);

        return new ReconcilePaymentWebhookResult(contribution.Id, contribution.Status.ToString());
    }
}
