namespace CrowdFunding.Modules.Notifications.Application.Features.Preferences.Commands.UpdateNotificationPreferences;

/// <summary>
/// Command to update the user's notification preferences.
/// </summary>
public sealed record UpdateNotificationPreferencesCommand(
    Guid UserId,
    bool CampaignUpdatesEnabled,
    bool MarketingAnnouncementsEnabled);

/// <summary>
/// Result containing updated notification preferences.
/// </summary>
public sealed record UpdateNotificationPreferencesResult(
    Guid UserId,
    bool CampaignUpdatesEnabled,
    bool MarketingAnnouncementsEnabled,
    DateTime UpdatedAtUtc);
