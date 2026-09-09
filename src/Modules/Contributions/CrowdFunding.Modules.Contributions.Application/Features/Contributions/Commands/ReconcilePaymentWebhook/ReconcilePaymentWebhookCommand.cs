namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ReconcilePaymentWebhook;

/// <summary>
/// A single payment-gateway webhook delivery to reconcile against the contribution its
/// <see cref="PaymentIntentId"/> correlates to. See TICKET-033.
/// </summary>
/// <param name="EventId">The gateway's own unique id for this webhook delivery — the idempotency
/// key. Re-delivering the same <paramref name="EventId"/> must be a safe no-op.</param>
/// <param name="PaymentIntentId">The gateway's payment-intent/charge reference, attached to the
/// contribution when it was created (<c>Contribution.AttachPaymentIntent</c>).</param>
/// <param name="EventType">The gateway event type (e.g. <c>payment_intent.succeeded</c>,
/// <c>payment_intent.payment_failed</c>). Any other value is acknowledged but produces no state
/// transition — this module only reacts to the two outcomes a pledge cares about.</param>
public sealed record ReconcilePaymentWebhookCommand(string EventId, string PaymentIntentId, string EventType);
