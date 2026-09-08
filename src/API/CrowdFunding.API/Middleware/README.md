# Request Processing Middleware

## Purpose
Organizes ASP.NET Core middleware components participating in the HTTP request processing pipeline.

## Architectural Architecture
Modern ASP.NET Core middleware and cross-cutting pipeline handlers in this application are organized as follows:
- **Exception Handling**: Migrated to the modern `IExceptionHandler` abstraction located at `src/API/CrowdFunding.API/Observability/GlobalExceptionHandler.cs`, providing RFC 7807 problem details formatting and status code mapping.
- **Distributed Correlation**: Located at `src/API/CrowdFunding.API/Observability/CorrelationIdMiddleware.cs`, capturing or assigning correlation identifiers for every incoming request and pushing them to Serilog `LogContext`.
- **Rate Limiting**: Configured in `src/API/CrowdFunding.API/RateLimiting/RateLimitingConfiguration.cs` using ASP.NET Core's built-in `RateLimiter` middleware.
- **Security & JWKS**: Configured in `src/API/CrowdFunding.API/Security/` and exposed via endpoint routing.

## Middleware Execution Pipeline Order
In `Program.cs`, the pipeline is ordered to guarantee predictable security and error handling:
1. Serilog Request Logging (`UseCrowdFundingSerilog`)
2. Exception Handling (`app.UseExceptionHandler()`)
3. Correlation Tracking (`app.UseMiddleware<CorrelationIdMiddleware>()`)
4. Health Checks (`/health/live`, `/health/ready`)
5. JWKS Key Distribution (`/.well-known/jwks.json`)
6. Rate Limiter (`app.UseRateLimiter()`)
7. Authentication (`app.UseAuthentication()`)
8. Authorization (`app.UseAuthorization()`)
9. Endpoint Routing (Controllers & SignalR Hubs)
