namespace CrowdFunding.API.Security;

/// <summary>
/// Binds the <c>PaymentGatewayWebhook</c> configuration section — the shared secret used to
/// verify <c>Stripe-Signature</c> headers on inbound payment webhooks. See TICKET-033.
/// </summary>
public sealed class PaymentGatewayWebhookOptions
{
    public const string SectionName = "PaymentGatewayWebhook";

    public string Secret { get; set; } = string.Empty;

    /// <summary>How old a signed timestamp can be before it's rejected as a possible replay.</summary>
    public int ToleranceSeconds { get; set; } = 300;
}
