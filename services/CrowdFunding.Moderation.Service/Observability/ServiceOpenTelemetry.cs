using CrowdFunding.BuildingBlocks.Infrastructure.Observability;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CrowdFunding.Moderation.Service.Observability;

/// <summary>
/// OpenTelemetry traces and metrics for the extracted Moderation service. The same
/// instrumentation as the monolith's <c>CrowdFunding.API.Observability.OpenTelemetryConfiguration</c>
/// under its own <c>service.name</c>, so a trace that starts in the monolith continues here.
/// Export is enabled by <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>; without it nothing is exported.
/// </summary>
public static class ServiceOpenTelemetry
{
    /// <summary>The <c>service.name</c> resource attribute.</summary>
    public const string ServiceName = "CrowdFunding.Moderation.Service";

    /// <summary>
    /// Registers tracing (ASP.NET Core, HttpClient, EF Core, Npgsql, outbox) and metrics
    /// (ASP.NET Core, HttpClient, Npgsql, .NET runtime).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration holding the <c>OTEL_*</c> settings.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddServiceOpenTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var exportEnabled = !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddNpgsql()
                    .AddSource(CrowdFundingTelemetry.ActivitySourceNames);

                if (exportEnabled)
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter("Npgsql", "System.Runtime");

                if (exportEnabled)
                {
                    metrics.AddOtlpExporter();
                }
            });

        return services;
    }
}
