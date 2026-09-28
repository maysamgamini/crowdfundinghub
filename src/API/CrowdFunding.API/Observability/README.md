# Observability & Reliability

## Purpose
Houses cross-cutting observability, diagnostic endpoints, structured logging, health check implementations, and centralized RFC 9457 problem details exception handling, and OpenTelemetry for the web application.

## Files
- `CorrelationIdMiddleware.cs`: ASP.NET Core middleware that resolves or generates a distributed correlation identifier from incoming request headers (`X-Correlation-ID` or W3C `traceparent`). It enriches the Serilog `LogContext` and returns `X-Correlation-ID` on all HTTP responses for distributed tracing.
- `DbContextHealthCheck.cs`: Generic `IHealthCheck` implementation for Entity Framework Core contexts (`DbContextHealthCheck<TContext>`). Tests database connectivity via `Database.CanConnectAsync()` for readiness probes.
- `GlobalExceptionHandler.cs`: Centralized `IExceptionHandler` that writes RFC 9457 `application/problem+json` for every unhandled exception, with a `traceId`. The status and client-safe detail come from `BuildingBlocks.Infrastructure/ErrorHandling/ExceptionClassifier`: validation (400 with `errors`), domain rule violations (400), domain state conflicts and concurrency losses (409), not found (404), 401/403, database overload (503 with `Retry-After`), and a generic 500 for anything else (framework and database messages are never returned).
- `HealthCheckResponseWriter.cs`: Serializes health check results into structured JSON containing overall status, total execution duration, and individual per-database check results.
- `OpenTelemetryConfiguration.cs`: `AddCrowdFundingOpenTelemetry` registers the OpenTelemetry SDK: traces from ASP.NET Core (health probes filtered out), HttpClient, EF Core, Npgsql and the outbox `ActivitySource`; metrics from ASP.NET Core, HttpClient, Npgsql and the .NET runtime. Exports over OTLP only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set (appsettings.Development.json points it at the Aspire dashboard from docker-compose.yml).
- `LoggingConfiguration.cs`: Serilog bootstrap configuration (`UseCrowdFundingSerilog`) that wires structured logging, console output, enrichers, and level filtering based on application environment settings.

## Endpoints
- `GET /health/live`: Liveness probe indicating process responsiveness without evaluating downstream database dependencies (returns 200 OK).
- `GET /health/ready`: Readiness probe verifying connectivity across all module databases (`campaigns-db`, `contributions-db`, `identity-db`, `moderation-db`, `notifications-db`, `campaign-updates-db`, `audit-db`).
