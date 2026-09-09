using CrowdFunding.Modules.Notifications.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Notifications.Infrastructure.Services;

/// <summary>
/// System clock implementation of <see cref="INotificationsDateTimeProvider"/>.
/// </summary>
public sealed class NotificationsDateTimeProvider : INotificationsDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
