namespace CrowdFunding.API.Contracts.CampaignUpdates;

/// <summary>
/// Represents the HTTP response payload for a single webhook subscription. Deliberately excludes
/// <c>secretKey</c> — it is shown only once, in the registration response.
/// </summary>
public sealed record WebhookSubscriptionResponse(
    Guid SubscriptionId,
    Guid CampaignId,
    string TargetUrl,
    bool IsActive,
    int ConsecutiveFailureCount,
    DateTime CreatedAtUtc);
