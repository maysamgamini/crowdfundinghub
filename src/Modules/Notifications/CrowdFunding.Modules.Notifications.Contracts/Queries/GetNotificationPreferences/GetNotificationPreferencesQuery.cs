namespace CrowdFunding.Modules.Notifications.Contracts.Queries.GetNotificationPreferences;

/// <summary>
/// Reads a user's notification preferences (defaults when none were saved). Part of the
/// module's public contract: any module that must respect a user's opt-outs asks through this
/// query instead of reading the Notifications database.
/// </summary>
/// <param name="UserId">The user whose preferences to read.</param>
public sealed record GetNotificationPreferencesQuery(Guid UserId);

/// <summary>
/// A user's notification preferences.
/// </summary>
/// <param name="UserId">The user.</param>
/// <param name="CampaignUpdatesEnabled">Whether the user receives e-mails about campaigns they back.</param>
/// <param name="MarketingAnnouncementsEnabled">Whether the user receives marketing announcements.</param>
/// <param name="UpdatedAtUtc">When the preferences were last changed.</param>
public sealed record GetNotificationPreferencesResult(
    Guid UserId,
    bool CampaignUpdatesEnabled,
    bool MarketingAnnouncementsEnabled,
    DateTime UpdatedAtUtc);
