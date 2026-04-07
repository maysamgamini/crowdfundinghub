using CrowdFunding.Modules.Contributions.Application.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Contributions.Infrastructure.DependencyInjection;

public static class ContributionsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddContributionsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddContributionsApplication();
        services.AddContributionsInfrastructure(configuration);

        return services;
    }
}
