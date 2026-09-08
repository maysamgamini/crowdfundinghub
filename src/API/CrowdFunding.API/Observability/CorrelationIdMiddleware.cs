using Serilog.Context;

namespace CrowdFunding.API.Observability;

/// <summary>
/// Ensures every request carries a correlation id, preferring the current W3C trace id so logs
/// can be joined with distributed traces, and echoes it back to the caller so client-reported
/// errors can be matched to server-side log entries.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string CorrelationHeader = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationIdMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the request pipeline.</param>
    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Captures or derives a correlation ID, pushes it to Serilog's LogContext, and echoes it in response headers.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the asynchronous middleware execution.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = System.Diagnostics.Activity.Current?.TraceId.ToHexString()
            ?? (context.Request.Headers.TryGetValue(CorrelationHeader, out var headerValue) ? headerValue.ToString() : null)
            ?? Guid.NewGuid().ToString("N");

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationHeader] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
