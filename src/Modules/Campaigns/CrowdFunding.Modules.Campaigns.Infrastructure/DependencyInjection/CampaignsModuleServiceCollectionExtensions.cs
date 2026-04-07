using CrowdFunding.Modules.Campaigns.Application.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.DependencyInjection;

public static class CampaignsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddCampaignsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCampaignsApplication();
        services.AddCampaignsInfrastructure(configuration);

        return services;
    }
}
