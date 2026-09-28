# CrowdFunding API Host

## Purpose
The primary entry-point ASP.NET Core Web API host for the modular monolith. It orchestrates cross-module dependency injection, HTTP transport routing, authentication and authorization, rate limiting, CORS and forwarded headers, correlation and OpenTelemetry tracing, OpenAPI documentation, SignalR real-time hubs, and database migration lifecycles.

## Architecture & Responsibilities
Following the repository's architectural principles in `DEV_GUIDELINES.md`:
- **Thin Transport Layer**: Minimal API endpoints (and the one comparison controller, `ModerationController`) only bind HTTP requests, build a command or query, dispatch it, and map the result to an API contract with explicit code (no mapping library). Validation runs in the command pipeline (`ValidationPipelineBehavior`), not in endpoints.
- **Decoupled Modules**: Direct module-to-domain references are forbidden; communication across boundaries occurs exclusively via contract projects (`Contracts`) and application events delivered through each module's transactional outbox.
- **Asymmetric Authentication (ES256)**: Validates incoming JWT tokens using ECDSA public keys published via the standard `/.well-known/jwks.json` endpoint. Inbound claim mapping is off (`MapInboundClaims = false`); the caller's identity comes from the `sub` claim.
- **Centralized Exception Handling**: Unhandled exceptions are converted to RFC 9457 `application/problem+json` responses (with `traceId`) by `GlobalExceptionHandler`, using `ExceptionClassifier` from BuildingBlocks.Infrastructure. Unknown exceptions become a generic 500 that never exposes the framework message.
- **Health Probes**: Provides `/health/live` (no checks) and `/health/ready` (every module `DbContext` checked).

## Files
- `Program.cs`: Bootstraps configuration, Serilog logging, OpenTelemetry, authentication and authorization policies, rate limiting, CORS, forwarded headers, module service registration, the request pipeline, endpoint and SignalR hub mapping, health checks, and the `migrate` / `seed-admin` CLI commands.
- `appsettings.json`: Base configuration containing connection strings, JWT validation parameters, and rate limiting options.
- `appsettings.Development.json`: Environment-specific overrides for local development (for example the OTLP exporter endpoint `http://localhost:4317`).
- `CrowdFunding.API.http`: HTTP scratchpad for exercising API routes with VS Code / Visual Studio REST client tools.
- `Dockerfile`: Multi-stage build that publishes the API and runs it on the ASP.NET Core 10 runtime image (port 8080).

## Subdirectories
- `Contracts`: Strongly typed request and response DTOs exposed over HTTP, organized by module (`Campaigns`, `RewardTiers`, `Contributions`, `Identity`, `Moderation`, `Notifications`, `CampaignUpdates`, `Webhooks`, `Common`).
- `Controllers`: `ModerationController`, the one MVC controller kept on purpose to compare with the Minimal APIs.
- `Documentation`: OpenAPI document generation (`OpenApiConfiguration`) served at `/openapi/v1.json` with the Scalar UI at `/scalar/v1`, in Development and Testing only.
- `Endpoints`: Minimal API endpoint groups for every module, registered by `EndpointRegistration`.
- `Middleware`: Intentionally empty apart from its README; see that README for where each pipeline component lives.
- `Migrations`: Database migration execution (`MigrationRunner`) and out-of-band operator admin account seeding (`AdminSeeder`).
- `Observability`: Structured logging (`LoggingConfiguration`), OpenTelemetry tracing and metrics (`OpenTelemetryConfiguration`), correlation id propagation (`CorrelationIdMiddleware`), health checks (`DbContextHealthCheck`, `HealthCheckResponseWriter`), and RFC 9457 error handling (`GlobalExceptionHandler`).
- `Properties`: Local environment launch settings (`launchSettings.json`, `http://localhost:5076`).
- `RateLimiting`: The global limiter and the named `auth-strict`, `payment-strict`, and `webhook-strict` policies, partitioned by `sub` or client IP (`RateLimitingConfiguration`).
- `RealTime`: SignalR hub (`CampaignHub`, mapped at `/hubs/campaigns`) and real-time push notification service (`SignalRCampaignRealtimeNotifier`).
- `Security`: Bridges the authenticated HTTP user into `ICurrentUser`, exposes the JWKS endpoint (`JwksEndpoint`), configures CORS and forwarded headers (`EdgeSecurityConfiguration`), and verifies inbound webhook signatures (Stripe and the media-analysis Cloud Function).

Background work (outbox processors, campaign expiration, reward-tier reservation scavenging, webhook dispatch, signing-key refresh) is registered by each module's Infrastructure `DependencyInjection` code, not by this project.

## Running & Operating
- **Run the API** (Development profile, `http://localhost:5076`; the OpenAPI UI is at `/scalar/v1`):
  ```bash
  dotnet run --project src/API/CrowdFunding.API
  ```
- **Apply Database Migrations (CLI)**:
  ```bash
  dotnet run --project src/API/CrowdFunding.API -- migrate
  ```
- **Seed System Administrator (CLI)**:
  ```bash
  dotnet run --project src/API/CrowdFunding.API -- seed-admin admin@crowdfunding.local "Password123!" "System Administrator"
  ```
