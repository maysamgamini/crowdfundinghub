using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.CampaignUpdates.Application.DependencyInjection;

/// <summary>
/// Registers services from the surrounding layer with the dependency injection container.
/// </summary>
public static class CampaignUpdatesApplicationDependencyInjection
{
    public static IServiceCollection AddCampaignUpdatesApplication(this IServiceCollection services)
    {
        return services;
    }
}
