using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Campaigns.Application.DependencyInjection;

public static class CampaignsApplicationDependencyInjection
{
    public static IServiceCollection AddCampaignsApplication(this IServiceCollection services)
    {
        // Register handlers, validators, mappings, etc.
        return services;
    }
}