using CrowdFunding.BuildingBlocks.Application.Retention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Retention;

public static class RetentionRegistration
{
    /// <summary>Registers the schedule that runs every <see cref="IRetentionTask"/> (safe to call from every module).</summary>
    public static IServiceCollection AddRetention(this IServiceCollection services)
    {
        services.AddOptions<RetentionOptions>().BindConfiguration(RetentionOptions.SectionName);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, RetentionBackgroundService>());
        return services;
    }
}
