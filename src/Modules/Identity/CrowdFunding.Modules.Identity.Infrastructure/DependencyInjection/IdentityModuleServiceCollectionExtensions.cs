using CrowdFunding.Modules.Identity.Application.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Identity.Infrastructure.DependencyInjection;

public static class IdentityModuleServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddIdentityApplication();
        services.AddIdentityInfrastructure(configuration);

        return services;
    }
}
