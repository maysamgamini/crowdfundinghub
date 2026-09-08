using Serilog;
using Serilog.Formatting.Compact;

namespace CrowdFunding.API.Observability;

/// <summary>
/// Wires up structured, machine-parseable logging so financial transactions, background jobs,
/// and authentication attempts are observable instead of silent (improvement.md §2.5/§3.5 —
/// previously ILogger was injected into exactly one class in the whole solution).
/// </summary>
public static class LoggingConfiguration
{
    public static ConfigureHostBuilder UseCrowdFundingSerilog(this ConfigureHostBuilder host)
    {
        host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .WriteTo.Console(new CompactJsonFormatter())
                .WriteTo.File(
                    new CompactJsonFormatter(),
                    path: "logs/crowdfunding-.json",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14);
        });

        return host;
    }
}
