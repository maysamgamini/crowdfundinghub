using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CrowdFunding.API.Observability;

/// <summary>
/// Readiness check for a module's database connection. Registered per DbContext (tagged
/// "ready") so /health/ready fails when any module can't reach PostgreSQL, while /health/live
/// stays independent of the database — a severed DB connection should stop traffic routing to
/// the pod without the orchestrator concluding the process itself is dead and restarting it.
/// </summary>
public sealed class DbContextHealthCheck<TContext> : IHealthCheck
    where TContext : DbContext
{
    private readonly TContext _dbContext;

    public DbContextHealthCheck(TContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy($"{typeof(TContext).Name} database connection is healthy.")
                : HealthCheckResult.Unhealthy($"{typeof(TContext).Name} could not connect to its database.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy($"{typeof(TContext).Name} health check threw an exception.", exception);
        }
    }
}
