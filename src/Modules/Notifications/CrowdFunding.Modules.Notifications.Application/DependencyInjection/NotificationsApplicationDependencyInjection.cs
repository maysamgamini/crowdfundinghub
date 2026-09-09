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
        services.AddScoped<Features.Preferences.Queries.GetNotificationPreferences.GetNotificationPreferencesQueryHandler>();
        services.AddScoped<Features.Preferences.Commands.UpdateNotificationPreferences.UpdateNotificationPreferencesCommandHandler>();
        services.AddScoped<Features.Preferences.Commands.Unsubscribe.UnsubscribeCommandHandler>();

        return services;
    }
}
