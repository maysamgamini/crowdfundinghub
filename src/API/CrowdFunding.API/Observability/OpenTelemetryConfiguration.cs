using CrowdFunding.BuildingBlocks.Infrastructure.Observability;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CrowdFunding.API.Observability;

/// <summary>
/// OpenTelemetry traces and metrics for the API host, exported over OTLP. Ported from
/// apps/backend's <c>OpenTelemetryConfiguration</c>, plus Npgsql instrumentation and this
/// codebase's own activity sources (the outbox dispatch span).
/// </summary>
/// <remarks>
/// <para>
/// Export is configured entirely by the standard OpenTelemetry environment variables:
/// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> (for example <c>http://localhost:4317</c>, the Aspire
/// dashboard in docker-compose.yml), optionally <c>OTEL_EXPORTER_OTLP_PROTOCOL</c>,
/// <c>OTEL_EXPORTER_OTLP_HEADERS</c> and <c>OTEL_SERVICE_NAME</c>. When no endpoint is set, spans
/// and metrics are still collected in-process (so trace ids reach logs and ProblemDetails) but
/// nothing is exported, which keeps tests and collector-less environments quiet.
/// </para>
/// <para>
/// Logs are not exported through OpenTelemetry: Serilog owns logging (see
/// <see cref="LoggingConfiguration"/>) and writes the active trace and span ids on every event.
/// </para>
/// </remarks>
public static class OpenTelemetryConfiguration
{
    /// <summary>The default <c>service.name</c> resource attribute.</summary>
    public const string ServiceName = "CrowdFunding.API";

    private const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>
    /// Registers tracing (ASP.NET Core, HttpClient, EF Core, Npgsql, outbox) and metrics
    /// (ASP.NET Core, HttpClient, Npgsql, .NET runtime).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration holding the <c>OTEL_*</c> settings.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddCrowdFundingOpenTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var exportEnabled = !string.IsNullOrWhiteSpace(configuration[OtlpEndpointKey]);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                        // Probes hit these every few seconds; tracing them only adds noise.
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
