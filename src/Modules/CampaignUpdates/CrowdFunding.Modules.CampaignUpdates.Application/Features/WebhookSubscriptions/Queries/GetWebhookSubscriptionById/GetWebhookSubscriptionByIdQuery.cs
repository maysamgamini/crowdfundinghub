namespace CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Queries.GetWebhookSubscriptionById;

/// <summary>
/// Represents the request to execute the Get Webhook Subscription By Id query.
/// </summary>
public sealed record GetWebhookSubscriptionByIdQuery(Guid CampaignId, Guid SubscriptionId);
