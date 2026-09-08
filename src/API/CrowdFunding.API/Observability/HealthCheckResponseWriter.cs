using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CrowdFunding.API.Observability;

/// <summary>
/// Writes health check results as sanitized JSON — status per check, no exception details or
/// stack traces (a failed DbContextHealthCheck carries the raw exception, which must not reach
/// an unauthenticated caller hitting /health/ready).
/// </summary>
public static class HealthCheckResponseWriter
{
    public static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
            }),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
