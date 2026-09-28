using System.Diagnostics;
using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Infrastructure.ErrorHandling;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CrowdFunding.API.Observability;

/// <summary>
/// Converts unhandled exceptions into RFC 9457 <c>application/problem+json</c> responses.
/// </summary>
/// <remarks>
/// <see cref="ExceptionClassifier"/> decides the status code and the client-safe detail; this
/// handler only logs and writes the response. Domain and application exceptions become 4xx with
/// their own message. Everything else (framework, EF Core, Npgsql) becomes a generic 500 so no
/// schema name or connection detail reaches the client, except database overload, which becomes
/// 503 with <c>Retry-After</c>. Every response carries a <c>traceId</c> the caller can quote.
/// </remarks>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private const string RetryAfterSeconds = "2";

    private readonly ILogger<GlobalExceptionHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalExceptionHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.TraceId.ToHexString() ?? httpContext.TraceIdentifier;
        var outcome = ExceptionClassifier.Classify(exception);

        if (outcome.IsServerFault)
        {
            _logger.LogError(exception, "Request failed with {StatusCode}. TraceId: {TraceId}", outcome.StatusCode, traceId);
        }
        else
        {
            _logger.LogWarning(exception, "Request failed with {StatusCode}. TraceId: {TraceId}", outcome.StatusCode, traceId);
        }

        // A validation failure carries per-property messages in the RFC 9457 "errors" member.
        var problemDetails = exception is RequestValidationException validation
            ? new ValidationProblemDetails(validation.Errors.ToDictionary())
            : new ProblemDetails();
        problemDetails.Status = outcome.StatusCode;
        problemDetails.Title = outcome.Title;
        problemDetails.Detail = outcome.Detail;
        problemDetails.Instance = httpContext.Request.Path;
        problemDetails.Extensions["traceId"] = traceId;

        if (outcome.StatusCode == StatusCodes.Status503ServiceUnavailable)
        {
            httpContext.Response.Headers.RetryAfter = RetryAfterSeconds;
        }

        httpContext.Response.StatusCode = outcome.StatusCode;

        // WriteAsJsonAsync overwrites Content-Type with "application/json" unless a content
        // type is passed explicitly, so it must go here rather than as a prior assignment.
        // The runtime type is passed so a ValidationProblemDetails keeps its "errors" member.
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            problemDetails.GetType(),
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }
}
