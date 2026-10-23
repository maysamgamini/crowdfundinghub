using CrowdFunding.Modules.Notifications.Domain.Aggregates;
using CrowdFunding.Modules.Notifications.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Notifications.Infrastructure.Features.Preferences;

/// <summary>
/// The one statement behind both preference commands. Preferences are a Tier 2 feature: no events,
/// no money, one row per user. The write is a single INSERT ... ON CONFLICT, so two requests that
/// arrive together for a user with no row cannot both insert and leave one of them with a
/// primary-key error; the later statement wins.
/// </summary>
internal static class NotificationPreferenceWriter
{
    public static async Task UpsertAsync(
        NotificationsDbContext dbContext,
        Guid userId,
        bool campaignUpdatesEnabled,
        bool marketingAnnouncementsEnabled,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(NotificationPreference))!;
        var table = $"\"{entityType.GetSchema()}\".\"{entityType.GetTableName()}\"";

#pragma warning disable EF1002 // the table name comes from the EF model, never from user input
        await dbContext.Database.ExecuteSqlRawAsync(
            $$"""
              INSERT INTO {{table}} ("UserId", "CampaignUpdatesEnabled", "MarketingAnnouncementsEnabled", "UpdatedAtUtc")
              VALUES ({0}, {1}, {2}, {3})
              ON CONFLICT ("UserId") DO UPDATE
              SET "CampaignUpdatesEnabled" = EXCLUDED."CampaignUpdatesEnabled",
                  "MarketingAnnouncementsEnabled" = EXCLUDED."MarketingAnnouncementsEnabled",
                  "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc"
              """,
            [userId, campaignUpdatesEnabled, marketingAnnouncementsEnabled, nowUtc],
            cancellationToken);
#pragma warning restore EF1002
    }
}
