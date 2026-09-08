using System.Diagnostics;
using System.Net;
using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Application.Security;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CrowdFunding.API.Observability;

/// <summary>
/// Converts unhandled exceptions into RFC 9457 <c>application/problem+json</c> responses.
/// Replaces the previous middleware, which returned a proprietary { StatusCode, Message } shape
/// and — for any exception, not just 500s — wrote <c>exception.Message</c> straight to the
/// client. A generic 500 can carry a raw Npgsql/db message (schema names, connection details),
/// so only the sanitized cases below expose their message; everything else gets a generic
/// message plus a TraceId the caller can quote when contacting support.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
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

        var (statusCode, title, detail) = exception switch
        {
            ArgumentException => (HttpStatusCode.BadRequest, "Bad Request", exception.Message),
            InvalidOperationException => (HttpStatusCode.BadRequest, "Bad Request", exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized", "Authentication required or invalid credentials."),
            ForbiddenAccessException => (HttpStatusCode.Forbidden, "Forbidden", "You do not have permission to perform this action."),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Not Found", exception.Message),
            // Resource-state conflicts (RFC 9110 §15.5.10) — duplicate unique values, invalid
            // state-machine transitions — are distinct from malformed input (400).
            ResourceConflictException => (HttpStatusCode.Conflict, "Conflict", exception.Message),
            // A losing optimistic-concurrency writer is also a conflict, not a server failure —
            // the client can safely retry with a fresh read.
            ConcurrencyConflictException => (HttpStatusCode.Conflict, "Conflict", "The resource was modified by another request. Please retry with the latest data."),
            // A unique-constraint violation that reaches here uncaught (i.e. no handler already
            // treated it as an idempotent no-op) is a client-facing conflict, not a server bug —
            // e.g. two concurrent requests racing to create the same logically-unique record.
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

        // WriteAsJsonAsync overwrites Content-Type with "application/json" unless a content
        // type is passed explicitly, so it must go here rather than as a prior assignment.
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }
}
