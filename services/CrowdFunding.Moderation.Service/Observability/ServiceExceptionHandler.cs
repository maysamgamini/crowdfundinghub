using System.Diagnostics;
using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Infrastructure.ErrorHandling;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CrowdFunding.Moderation.Service.Observability;

/// <summary>
/// Converts unhandled exceptions into RFC 9457 <c>application/problem+json</c> responses for the
/// extracted Moderation service.
/// </summary>
/// <remarks>
/// The status-code decision lives in the shared <see cref="ExceptionClassifier"/>, so the service
/// and the monolith's <c>GlobalExceptionHandler</c> return identical responses for the same
/// failure. Only this thin logging-and-writing adapter is repeated per host, because writing an
/// HTTP response is transport glue each deployable owns.
/// </remarks>
public sealed class ServiceExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ServiceExceptionHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceExceptionHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ServiceExceptionHandler(ILogger<ServiceExceptionHandler> logger)
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
            httpContext.Response.Headers.RetryAfter = "2";
        }

        httpContext.Response.StatusCode = outcome.StatusCode;

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
