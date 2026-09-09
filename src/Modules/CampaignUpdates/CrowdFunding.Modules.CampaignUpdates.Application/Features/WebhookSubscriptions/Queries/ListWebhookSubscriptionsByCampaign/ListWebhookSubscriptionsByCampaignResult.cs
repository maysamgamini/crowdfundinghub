namespace CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Queries.ListWebhookSubscriptionsByCampaign;

/// <summary>
/// One webhook subscription row in a List Webhook Subscriptions By Campaign result. Deliberately
/// excludes <c>SecretKey</c> — it is shown only once, in the registration response.
/// </summary>
public sealed record WebhookSubscriptionSummary(
    Guid SubscriptionId,
    Guid CampaignId,
    string TargetUrl,
    bool IsActive,
    int ConsecutiveFailureCount,
    DateTime CreatedAtUtc);

/// <summary>
/// Represents the outcome returned by List Webhook Subscriptions By Campaign.
/// </summary>
public sealed record ListWebhookSubscriptionsByCampaignResult(IReadOnlyList<WebhookSubscriptionSummary> Subscriptions);
