namespace CrowdFunding.Modules.CampaignUpdates.Contracts.Webhooks;

/// <summary>
/// The <c>eventType</c> values a webhook subscriber can receive.
/// </summary>
public static class WebhookEventTypes
{
    /// <summary>A backer's pledge was confirmed by the payment gateway. Payload: <see cref="PledgeConfirmedWebhookPayload"/>.</summary>
    public const string PledgeConfirmed = "pledge.confirmed";
}
