# Building Blocks: Infrastructure Layer

## Purpose
Provides shared technical implementations for persistence, transactional outbox management, domain event dispatching, and usage metering services.

## Subdirectories
- `ErrorHandling`:
  - `ExceptionClassifier.cs`: Maps an exception to an HTTP status, title and client-safe detail for every host (monolith API and extracted Moderation service). Domain and application exception types become 4xx with their own message; database overload (Npgsql pool exhaustion, `53300`, `57P03`) becomes 503; everything else becomes a generic 500 so framework and database messages never reach the client.
- `Events`:
  - `ServiceProviderEventPublisher.cs`: Implements `IEventPublisher` by discovering and invoking all registered `IEventHandler<TNotification>` implementations from the ASP.NET Core `IServiceProvider`.
- `Metering`:
  - `OpenMeterClient.cs`: Implements `IUsageMeteringClient` over resilient HTTP (`Microsoft.Extensions.Http.Resilience`) targeting OpenMeter Cloud.
  - `OpenMeterOptions.cs`: Binds to configuration settings for OpenMeter endpoints and tokens.
  - `MeteringDependencyInjection.cs`: Extension methods wiring OpenMeter clients and resilient HTTP pipelines.
- `Persistence`:
  - `OutboxMessage.cs`: Persistence model representing outbox records, storing serialized JSON payloads, event discriminator, occurrence time, processed time, and error trace.
  - `ModelBuilderExtensions.cs`: Fluent EF Core configuration extensions (`ConfigureOutbox`, `ConfigureDeadLetter`) for mapping outbox and DLQ tables uniformly across modular DbContexts.
  - `DomainEventAccessor.cs`: Utility for extracting and clearing uncommitted `BaseEvent` instances from tracked aggregate roots in EF Core `ChangeTracker`.
