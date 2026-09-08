using CrowdFunding.BuildingBlocks.Application.Metering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Metering;

/// <summary>
/// Extension methods for configuring OpenMeter usage metering and resilient HTTP client services.
/// </summary>
public static class MeteringDependencyInjection
{
    /// <summary>
    /// Registers OpenMeter options, fee configuration, and a resilient HTTP client for <see cref="IUsageMeteringClient"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddOpenMeterMetering(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(OpenMeterOptions.SectionName).Get<OpenMeterOptions>() ?? new OpenMeterOptions();
        var feeOptions = configuration.GetSection(OpenMeterFeeOptions.SectionName).Get<OpenMeterFeeOptions>() ?? new OpenMeterFeeOptions();
        services.AddSingleton(options);
        services.AddSingleton(feeOptions);

        services.AddHttpClient<IUsageMeteringClient, OpenMeterClient>(client =>
            {
                client.BaseAddress = options.BaseUrl;

                if (!string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    client.DefaultRequestHeaders.Authorization = new("Bearer", options.ApiKey);
                }
            })
            .AddStandardResilienceHandler();

        return services;
    }
}
