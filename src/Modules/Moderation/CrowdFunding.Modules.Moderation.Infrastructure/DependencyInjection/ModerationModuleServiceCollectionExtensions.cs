using CrowdFunding.Modules.Moderation.Application.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Moderation.Infrastructure.DependencyInjection;

public static class ModerationModuleServiceCollectionExtensions
{
    public static IServiceCollection AddModerationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddModerationApplication();
        services.AddModerationInfrastructure(configuration);

        return services;
    }
}
