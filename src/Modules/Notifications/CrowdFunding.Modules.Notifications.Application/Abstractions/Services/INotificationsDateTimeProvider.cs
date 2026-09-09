namespace CrowdFunding.Modules.Notifications.Application.Abstractions.Services;

/// <summary>
/// Provides UTC time abstraction for the Notifications module.
/// </summary>
public interface INotificationsDateTimeProvider
{
    DateTime UtcNow { get; }
}
