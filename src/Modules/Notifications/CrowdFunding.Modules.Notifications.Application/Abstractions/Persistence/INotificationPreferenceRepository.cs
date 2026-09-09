using CrowdFunding.Modules.Notifications.Domain.Aggregates;

namespace CrowdFunding.Modules.Notifications.Application.Abstractions.Persistence;

/// <summary>
/// Persistence operations for user notification preferences.
/// </summary>
public interface INotificationPreferenceRepository
{
    Task<NotificationPreference?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task UpsertAsync(NotificationPreference preference, CancellationToken cancellationToken);
}
