using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.BuildingBlocks.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CrowdFunding.BuildingBlocks.Infrastructure.ErrorHandling;

/// <summary>
/// The HTTP-facing outcome of an exception: status code, ProblemDetails title and a detail
/// string that is always safe to return to the caller.
/// </summary>
/// <param name="StatusCode">The HTTP status code to return.</param>
/// <param name="Title">The RFC 9457 <c>title</c>.</param>
/// <param name="Detail">The RFC 9457 <c>detail</c>; never a framework or database message.</param>
public sealed record ExceptionClassification(int StatusCode, string Title, string Detail)
{
    /// <summary>
    /// Gets a value indicating whether the failure is the server's fault (5xx), which is logged
    /// as an error rather than a warning.
    /// </summary>
    public bool IsServerFault => StatusCode >= 500;
}

/// <summary>
/// Maps exceptions to HTTP outcomes for every host (the monolith API and the extracted
/// Moderation service), so both return identical RFC 9457 responses for the same failure.
/// </summary>
/// <remarks>
/// Only exception types this codebase owns become 4xx, and only they expose their message:
/// <see cref="DomainException"/> subtypes from the domain, and the application-layer
/// <see cref="NotFoundException"/>, <see cref="ResourceConflictException"/>,
/// <see cref="ConcurrencyConflictException"/>, <see cref="UnauthenticatedException"/> and
/// <see cref="ForbiddenAccessException"/>. BCL types such as <see cref="InvalidOperationException"/>
/// or <see cref="ArgumentException"/> are thrown by EF Core, Npgsql and the framework as well, with
/// messages that can name tables or connection settings, so they fall through to a generic 500.
/// </remarks>
public static class ExceptionClassifier
{
    private const string GenericServerError = "An unexpected error occurred. Please contact support quoting the TraceId.";

    /// <summary>
    /// Classifies an exception into the HTTP outcome the caller should see.
    /// </summary>
    /// <param name="exception">The unhandled exception.</param>
    /// <returns>The status code, title and client-safe detail.</returns>
    public static ExceptionClassification Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // Checked first because the overload can arrive wrapped (e.g. in a DbUpdateException).
        if (IsDatabaseOverloaded(exception))
        {
            return new(503, "Service Unavailable", "The service is busy. Please retry in a moment.");
        }

        return exception switch
        {
            DomainConflictException => new(409, "Conflict", exception.Message),
            DomainException => new(400, "Business Rule Violation", exception.Message),
            NotFoundException => new(404, "Not Found", exception.Message),
            // RFC 9110 §15.5.10: the request conflicts with the resource's current state.
            ResourceConflictException => new(409, "Conflict", exception.Message),
            // A losing optimistic-concurrency writer can safely retry with a fresh read.
            ConcurrencyConflictException => new(409, "Conflict", "The resource was modified by another request. Please retry with the latest data."),
            // Two requests racing to create the same logically unique record: a conflict, not a bug.
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                new(409, "Conflict", "A record with the same unique value already exists."),
            UnauthenticatedException => new(401, "Unauthorized", "Authentication required or invalid credentials."),
            ForbiddenAccessException => new(403, "Forbidden", "You do not have permission to perform this action."),
            _ => new(500, "Internal Server Error", GenericServerError),
        };
    }

    /// <summary>
    /// Returns <see langword="true"/> when the exception chain shows the database is overloaded
    /// rather than broken: Npgsql pool exhaustion (an <see cref="NpgsqlException"/> caused by a
    /// <see cref="TimeoutException"/> while waiting for a free connection), or PostgreSQL refusing
    /// a connection (<c>53300 too_many_connections</c>, <c>57P03 cannot_connect_now</c>).
    /// </summary>
    /// <param name="exception">The exception to inspect, including its inner exceptions.</param>
    /// <returns><see langword="true"/> when the caller should retry shortly (HTTP 503).</returns>
    public static bool IsDatabaseOverloaded(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.TooManyConnections or PostgresErrorCodes.CannotConnectNow })
            {
                return true;
            }

            if (current is NpgsqlException { InnerException: TimeoutException })
            {
                return true;
            }
        }

        return false;
    }
}
