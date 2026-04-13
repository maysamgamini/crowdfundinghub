using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Notifications.Application.DependencyInjection;

/// <summary>
/// Registers services from the surrounding layer with the dependency injection container.
/// </summary>
public static class NotificationsApplicationDependencyInjection
{
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services)
    {
        return services;
    }
}
