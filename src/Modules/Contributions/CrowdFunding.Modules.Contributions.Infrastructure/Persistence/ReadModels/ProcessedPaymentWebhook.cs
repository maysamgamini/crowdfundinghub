namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.ReadModels;

/// <summary>
/// One row per payment-gateway webhook event id already applied — the idempotency guard against
/// at-least-once webhook redelivery (Stripe retries a webhook endpoint for up to 72 hours on any
/// non-2xx response). Checking this table for <see cref="WebhookEventId"/> before touching the
/// <c>Contribution</c> aggregate is what makes replaying the same event a safe no-op instead of
/// double-crediting or throwing. See TICKET-033.
/// </summary>
public sealed class ProcessedPaymentWebhook
{
    public string WebhookEventId { get; set; } = string.Empty;
    public string PaymentIntentId { get; set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; set; }
}
