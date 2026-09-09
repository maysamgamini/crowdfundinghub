namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;

/// <summary>
/// Tracks which payment-gateway webhook event ids have already been applied, so an
/// at-least-once redelivery (Stripe retries for up to 72 hours on any non-2xx response) is a
/// safe no-op rather than a duplicate state transition. See TICKET-033.
/// </summary>
public interface IPaymentWebhookIdempotencyStore
{
    Task<bool> IsProcessedAsync(string webhookEventId, CancellationToken cancellationToken);

    Task MarkProcessedAsync(string webhookEventId, string paymentIntentId, DateTime processedAtUtc, CancellationToken cancellationToken);
}
