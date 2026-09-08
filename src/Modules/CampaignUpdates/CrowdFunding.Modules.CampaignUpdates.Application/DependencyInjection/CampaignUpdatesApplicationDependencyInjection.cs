using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.CampaignUpdates.Application.DependencyInjection;

/// <summary>
/// Registers services from the surrounding layer with the dependency injection container.
/// </summary>
public static class CampaignUpdatesApplicationDependencyInjection
{
    /// <summary>
    /// Registers CampaignUpdates application services and activity event handlers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddCampaignUpdatesApplication(this IServiceCollection services)
    {
        return services;
    }
}
