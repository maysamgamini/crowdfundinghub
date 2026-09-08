using CrowdFunding.BuildingBlocks.Application.Metering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Metering;

public static class MeteringDependencyInjection
{
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
