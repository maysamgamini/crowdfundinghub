using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Notifications.Application.DependencyInjection;

/// <summary>
/// Registers services from the surrounding layer with the dependency injection container.
/// </summary>
public static class NotificationsApplicationDependencyInjection
{
    /// <summary>
    /// Registers Notifications application services and event handlers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services)
    {
        // The preference handlers are Tier 2 and live in the Infrastructure project; the host
        // registers them by scanning that assembly.
        return services;
    }
}
