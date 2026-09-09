using System.Diagnostics;
using System.Net;
using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Application.Security;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CrowdFunding.Moderation.Service.Observability;

/// <summary>
/// Converts unhandled exceptions into RFC 9457 <c>application/problem+json</c> responses — a
/// small duplicate of the monolith's <c>CrowdFunding.API.Observability.GlobalExceptionHandler</c>.
/// It only ever depends on <c>BuildingBlocks.Application</c>'s exception types (already a
/// dependency of every module), so this is presentation-layer glue duplicated for host
/// independence, not a fork of any business rule.
/// </summary>
public sealed class ServiceExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ServiceExceptionHandler> _logger;

    public ServiceExceptionHandler(ILogger<ServiceExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.TraceId.ToHexString() ?? httpContext.TraceIdentifier;

        var (statusCode, title, detail) = exception switch
        {
            ArgumentException => (HttpStatusCode.BadRequest, "Bad Request", exception.Message),
            InvalidOperationException => (HttpStatusCode.BadRequest, "Bad Request", exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized", "Authentication required or invalid credentials."),
            ForbiddenAccessException => (HttpStatusCode.Forbidden, "Forbidden", "You do not have permission to perform this action."),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Not Found", exception.Message),
            ResourceConflictException => (HttpStatusCode.Conflict, "Conflict", exception.Message),
            ConcurrencyConflictException => (HttpStatusCode.Conflict, "Conflict", "The resource was modified by another request. Please retry with the latest data."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (HttpStatusCode.Conflict, "Conflict", "A record with the same unique value already exists."),
            _ => (HttpStatusCode.InternalServerError, "Internal Server Error", "An unexpected error occurred. Please contact support quoting the TraceId.")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception occurred. TraceId: {TraceId}", traceId);
        }
        else
        {
            _logger.LogWarning(exception, "Request failed with {StatusCode}. TraceId: {TraceId}", (int)statusCode, traceId);
        }

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
        };
        problemDetails.Extensions["traceId"] = traceId;

        httpContext.Response.StatusCode = (int)statusCode;

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }
}
