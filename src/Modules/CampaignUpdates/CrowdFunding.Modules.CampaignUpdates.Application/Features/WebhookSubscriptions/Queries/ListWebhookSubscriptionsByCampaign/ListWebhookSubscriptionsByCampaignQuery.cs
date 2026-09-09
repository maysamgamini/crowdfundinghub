namespace CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Queries.ListWebhookSubscriptionsByCampaign;

/// <summary>
/// Represents the request to execute the List Webhook Subscriptions By Campaign query.
/// </summary>
public sealed record ListWebhookSubscriptionsByCampaignQuery(Guid CampaignId);
