# QA Ticket: TICKET-009

**Title:** `GlobalExceptionHandler` Converts `ConcurrencyConflictException` and Database Unique Constraint Violations into 500 Internal Server Errors  
**Severity:** 🟡 P2 (Medium - RFC 9457 Non-Compliance & Status Code Semantics)  
**QA Focus Area:** API Contracts & Error Handling QA  
**Found By:** `qa-api-contracts`  
**Status:** Open  
**Project Mode:** Greenfield (No backward compatibility required)  

---

## 1. Description
In `GlobalExceptionHandler.cs`, unhandled exceptions are mapped to HTTP status codes via a switch expression:

```csharp
var (statusCode, title, detail) = exception switch
{
    ArgumentException => (HttpStatusCode.BadRequest, "Bad Request", exception.Message),
    InvalidOperationException => (HttpStatusCode.BadRequest, "Bad Request", exception.Message),
    UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized", "Authentication required or invalid credentials."),
    ForbiddenAccessException => (HttpStatusCode.Forbidden, "Forbidden", "You do not have permission to perform this action."),
    KeyNotFoundException => (HttpStatusCode.NotFound, "Not Found", exception.Message),
    _ => (HttpStatusCode.InternalServerError, "Internal Server Error", "An unexpected error occurred. Please contact support quoting the TraceId.")
};
```

Notice that:
1. `ConcurrencyConflictException` (defined in `BuildingBlocks.Application.Exceptions`) is NOT handled in this switch statement. It falls into the catch-all `_` branch and returns `500 Internal Server Error`.
2. Under HTTP semantics and RFC 9110 §15.5.10, a concurrency conflict (optimistic lock failure where an aggregate was modified concurrently) is a client conflict that must return `409 Conflict`.
3. Furthermore, when concurrent operations collide on database unique constraints (e.g. duplicate email registration or duplicate review creation resulting in a PostgreSQL unique violation `23505`), the exception bubbles up as a `DbUpdateException` and is likewise reported to the client as a `500 Internal Server Error`.

## 2. Blast Radius & Defect Reproduction
1. Two concurrent requests attempt to register the same user email or update a campaign.
2. The loser of the race encounters an optimistic concurrency token mismatch or a unique constraint collision.
3. The API responds with `500 Internal Server Error` instead of `409 Conflict`.
4. Client SDKs, frontend applications, and monitoring dashboards log server error alerts (5xx) for normal, expected concurrency contention scenarios that should be signaled with 409 Conflict for automatic client retry.

## 3. Affected Files
- [`src/API/CrowdFunding.API/Observability/GlobalExceptionHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Observability/GlobalExceptionHandler.cs#L30-L38)

## 4. Recommended Fix (Greenfield)
Extend the exception mapping in `GlobalExceptionHandler.cs`:
```csharp
var (statusCode, title, detail) = exception switch
{
    ArgumentException => (HttpStatusCode.BadRequest, "Bad Request", exception.Message),
    InvalidOperationException => (HttpStatusCode.BadRequest, "Bad Request", exception.Message),
    UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized", "Authentication required or invalid credentials."),
    ForbiddenAccessException => (HttpStatusCode.Forbidden, "Forbidden", "You do not have permission to perform this action."),
    KeyNotFoundException => (HttpStatusCode.NotFound, "Not Found", exception.Message),
    ConcurrencyConflictException => (HttpStatusCode.Conflict, "Conflict", "The resource was updated concurrently by another request. Please retry."),
    DbUpdateException dbEx when dbEx.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation } =>
        (HttpStatusCode.Conflict, "Conflict", "A record with the same unique value already exists."),
    _ => (HttpStatusCode.InternalServerError, "Internal Server Error", "An unexpected error occurred. Please contact support quoting the TraceId.")
};
```
