namespace CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Commands.DeleteWebhookSubscription;

/// <summary>
/// Represents the request to execute the Delete Webhook Subscription use case — deactivates a
/// creator's registered webhook target. See TICKET-047.
/// </summary>
public sealed record DeleteWebhookSubscriptionCommand(Guid CampaignId, Guid SubscriptionId);
