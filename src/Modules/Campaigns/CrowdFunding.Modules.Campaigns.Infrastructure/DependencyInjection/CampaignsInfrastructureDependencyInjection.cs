using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.DependencyInjection;

public static class CampaignsInfrastructureDependencyInjection
{
    public static IServiceCollection AddCampaignsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register DbContext, repositories, providers, etc.
        return services;
    }
}