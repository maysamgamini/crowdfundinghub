namespace CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Queries.GetWebhookSubscriptionById;

/// <summary>
/// Represents the outcome returned by Get Webhook Subscription By Id. Deliberately excludes
/// <c>SecretKey</c> — it is shown only once, in the registration response.
/// </summary>
public sealed record GetWebhookSubscriptionByIdResult(
    Guid SubscriptionId,
    Guid CampaignId,
    string TargetUrl,
    bool IsActive,
    int ConsecutiveFailureCount,
    DateTime CreatedAtUtc);
