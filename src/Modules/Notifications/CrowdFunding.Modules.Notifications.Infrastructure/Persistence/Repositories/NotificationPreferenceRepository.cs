using CrowdFunding.Modules.Notifications.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Notifications.Domain.Aggregates;
using CrowdFunding.Modules.Notifications.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Notifications.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implements <see cref="INotificationPreferenceRepository"/> against <see cref="NotificationsDbContext"/>.
/// </summary>
public sealed class NotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly NotificationsDbContext _dbContext;

    public NotificationPreferenceRepository(NotificationsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<NotificationPreference?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _dbContext.NotificationPreferences
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public async Task UpsertAsync(NotificationPreference preference, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.NotificationPreferences.FindAsync([preference.UserId], cancellationToken);
        if (existing is null)
        {
            _dbContext.NotificationPreferences.Add(preference);
        }
        else
        {
            existing.Update(preference.CampaignUpdatesEnabled, preference.MarketingAnnouncementsEnabled, preference.UpdatedAtUtc);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
