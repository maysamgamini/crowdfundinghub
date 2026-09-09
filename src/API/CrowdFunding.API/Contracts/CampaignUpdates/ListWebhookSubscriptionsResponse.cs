namespace CrowdFunding.API.Contracts.CampaignUpdates;

/// <summary>
/// Represents the HTTP response payload for listing a campaign's webhook subscriptions.
/// </summary>
public sealed record ListWebhookSubscriptionsResponse(IReadOnlyList<WebhookSubscriptionResponse> Subscriptions);
