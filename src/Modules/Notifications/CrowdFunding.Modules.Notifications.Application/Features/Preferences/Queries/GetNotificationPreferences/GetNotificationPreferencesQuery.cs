namespace CrowdFunding.Modules.Notifications.Application.Features.Preferences.Queries.GetNotificationPreferences;

/// <summary>
/// Query to retrieve notification preferences for the specified or current user.
/// </summary>
public sealed record GetNotificationPreferencesQuery(Guid UserId);

/// <summary>
/// Result containing user notification preferences.
/// </summary>
public sealed record GetNotificationPreferencesResult(
    Guid UserId,
    bool CampaignUpdatesEnabled,
    bool MarketingAnnouncementsEnabled,
    DateTime UpdatedAtUtc);
