namespace CrowdFunding.API.Contracts.Webhooks;

/// <summary>
/// A minimal subset of Stripe's webhook event envelope — just enough to reconcile a pledge:
/// the event's own id (the idempotency key), its type, and the payment intent id nested under
/// <c>data.object.id</c>.
/// </summary>
public sealed record StripeWebhookPayload(string Id, string Type, StripeWebhookData Data);

public sealed record StripeWebhookData(StripeWebhookObject Object);

public sealed record StripeWebhookObject(string Id);
