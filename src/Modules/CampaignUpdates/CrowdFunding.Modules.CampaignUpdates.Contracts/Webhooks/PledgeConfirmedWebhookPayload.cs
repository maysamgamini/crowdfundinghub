namespace CrowdFunding.Modules.CampaignUpdates.Contracts.Webhooks;

/// <summary>
/// The JSON body POSTed to a campaign owner's webhook endpoint when a pledge is confirmed.
/// Serialized with camelCase property names.
/// </summary>
/// <param name="EventType">Always <see cref="WebhookEventTypes.PledgeConfirmed"/>.</param>
/// <param name="ContributionId">The confirmed contribution.</param>
/// <param name="CampaignId">The campaign the pledge was made to.</param>
/// <param name="Amount">The pledged amount.</param>
/// <param name="Currency">The ISO 4217 currency code.</param>
public sealed record PledgeConfirmedWebhookPayload(
    string EventType,
    Guid ContributionId,
    Guid CampaignId,
    decimal Amount,
    string Currency);
