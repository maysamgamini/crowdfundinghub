# Request Processing Middleware

## Purpose
This folder is intentionally empty: it holds no code, only this README. The API has a single custom middleware class, and it lives next to the rest of the observability code. The folder is kept so a reader looking for "the middleware" finds a map of where each pipeline component is defined and in which order `Program.cs` registers it.

## Where the Pipeline Components Live
- **Exception handling**: `../Observability/GlobalExceptionHandler.cs`, an `IExceptionHandler` registered with `AddExceptionHandler` and activated by `app.UseExceptionHandler()`. It classifies the exception with `ExceptionClassifier` (BuildingBlocks.Infrastructure/ErrorHandling) and writes an RFC 9457 `application/problem+json` response with a `traceId`.
- **Correlation id**: `../Observability/CorrelationIdMiddleware.cs`, the only custom middleware class. It reads or assigns a correlation id for each request, returns it in a response header, and pushes it into the Serilog `LogContext`.
- **Forwarded headers and CORS**: `../Security/EdgeSecurityConfiguration.cs` configures the built-in `ForwardedHeaders` middleware (trusting loopback plus `ForwardedHeaders:KnownProxies` / `KnownNetworks`) and the default CORS policy from `Cors:AllowedOrigins`.
- **Rate limiting**: `../RateLimiting/RateLimitingConfiguration.cs` configures the built-in `RateLimiter` middleware (global limiter plus named policies; a rejected request gets a 429 ProblemDetails with `Retry-After`).
- **Authentication and JWKS**: JWT Bearer options are configured in `Program.cs`; the public key set is served by `../Security/JwksEndpoint.cs`.
- **Logging**: Serilog is attached to the host in `Program.cs` (`builder.Host.UseCrowdFundingSerilog()`, defined in `../Observability/LoggingConfiguration.cs`); it is not a pipeline middleware.

## Middleware Execution Pipeline Order
In `Program.cs`, the pipeline is ordered as follows:
1. Forwarded headers (`app.UseForwardedHeaders()`), first so every later component sees the real client IP and scheme.
2. Exception handling (`app.UseExceptionHandler()`).
3. Correlation tracking (`app.UseMiddleware<CorrelationIdMiddleware>()`).
4. HTTPS redirection (`app.UseHttpsRedirection()`).
5. CORS (`app.UseCors()`).
6. Health checks (`/health/live`, `/health/ready`) and JWKS (`/.well-known/jwks.json`), all excluded from rate limiting.
7. Authentication (`app.UseAuthentication()`), before the rate limiter so policies can partition by the caller's `sub`.
8. Rate limiter (`app.UseRateLimiter()`).
9. Authorization (`app.UseAuthorization()`).
10. Endpoints: Minimal API groups (`MapCrowdFundingEndpoints`), the comparison controller (`MapControllers`), and the SignalR hub (`/hubs/campaigns`).

In Development and Testing, the OpenAPI document and Scalar UI are mapped before this pipeline is built (`MapCrowdFundingOpenApi`).
