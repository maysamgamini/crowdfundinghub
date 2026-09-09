namespace CrowdFunding.API.Contracts.Notifications;

/// <summary>
/// Response payload containing user notification preferences.
/// </summary>
public sealed record NotificationPreferencesResponse(
    Guid UserId,
    bool CampaignUpdatesEnabled,
    bool MarketingAnnouncementsEnabled,
    DateTime UpdatedAtUtc);

/// <summary>
/// Request payload to update notification preferences.
/// </summary>
public sealed record UpdateNotificationPreferencesRequest(
    bool CampaignUpdatesEnabled,
    bool MarketingAnnouncementsEnabled);

/// <summary>
/// Request payload to unsubscribe from non-transactional notifications.
/// </summary>
public sealed record UnsubscribeRequest(Guid? UserId);

/// <summary>
/// Response payload for unsubscribe operation.
/// </summary>
public sealed record UnsubscribeResponse(Guid UserId, bool Unsubscribed);
