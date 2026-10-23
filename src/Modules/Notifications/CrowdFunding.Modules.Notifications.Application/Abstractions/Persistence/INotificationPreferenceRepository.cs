using CrowdFunding.Modules.Notifications.Domain.Aggregates;

namespace CrowdFunding.Modules.Notifications.Application.Abstractions.Persistence;

/// <summary>
/// Read access to user notification preferences for the email sender. Writing preferences is a
/// Tier 2 feature that uses the module's DbContext directly (see the Preferences handlers).
/// </summary>
public interface INotificationPreferenceRepository
{
    Task<NotificationPreference?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
}
