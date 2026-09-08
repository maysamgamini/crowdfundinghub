# Observability & Reliability

## Purpose
Houses cross-cutting observability, diagnostic endpoints, structured logging, health check implementations, and centralized RFC 7807 problem details exception handling for the web application.

## Files
- `CorrelationIdMiddleware.cs`: ASP.NET Core middleware that resolves or generates a distributed correlation identifier from incoming request headers (`X-Correlation-ID` or W3C `traceparent`). It enriches the Serilog `LogContext` and returns `X-Correlation-ID` on all HTTP responses for distributed tracing.
- `DbContextHealthCheck.cs`: Generic `IHealthCheck` implementation for Entity Framework Core contexts (`DbContextHealthCheck<TContext>`). Tests database connectivity via `Database.CanConnectAsync()` for readiness probes.
- `GlobalExceptionHandler.cs`: Centralized `IExceptionHandler` that catches unhandled exceptions and maps them into RFC 7807 compliant `ProblemDetails` payloads. Specifically handles domain validation violations (400), `ConcurrencyConflictException` (409 Conflict), and internal errors (500) while scrubbing sensitive stack details in production.
- `HealthCheckResponseWriter.cs`: Serializes health check results into structured JSON containing overall status, total execution duration, and individual per-database check results.
- `LoggingConfiguration.cs`: Serilog bootstrap configuration (`UseCrowdFundingSerilog`) that wires structured logging, console output, enrichers, and level filtering based on application environment settings.

## Endpoints
- `GET /health/live`: Liveness probe indicating process responsiveness without evaluating downstream database dependencies (returns 200 OK).
- `GET /health/ready`: Readiness probe verifying connectivity across all module databases (`campaigns-db`, `contributions-db`, `identity-db`, `moderation-db`).
