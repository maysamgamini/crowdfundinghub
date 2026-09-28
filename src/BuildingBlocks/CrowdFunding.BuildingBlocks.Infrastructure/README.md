# Building Blocks: Infrastructure Layer

## Purpose
Provides shared technical implementations for persistence, transactional outbox management, domain event dispatching, and usage metering services.

## Subdirectories
- `Inbox`:
  - `ProcessedEvent.cs`: One row per (EventId, ConsumerName) a module has applied (ported from apps/backend). `EventId` is a string so external ids such as Stripe `evt_…` share the table.
  - `EfIdempotentConsumer.cs`: EF Core base for each module's `IIdempotentConsumer`. Claims with `INSERT … ON CONFLICT DO NOTHING` inside the effect's transaction, so a concurrent duplicate waits on the first claim and then skips; a failing effect rolls the claim back.
- `ErrorHandling`:
  - `ExceptionClassifier.cs`: Maps an exception to an HTTP status, title and client-safe detail for every host (monolith API and extracted Moderation service). Domain and application exception types become 4xx with their own message; database overload (Npgsql pool exhaustion, `53300`, `57P03`) becomes 503; everything else becomes a generic 500 so framework and database messages never reach the client.
- `Events`:
  - `ServiceProviderEventPublisher.cs`: Implements `IEventPublisher` by discovering and invoking all registered `IEventHandler<TNotification>` implementations from the ASP.NET Core `IServiceProvider`.
- `Metering`:
  - `OpenMeterClient.cs`: Implements `IUsageMeteringClient` over resilient HTTP (`Microsoft.Extensions.Http.Resilience`) targeting OpenMeter Cloud.
  - `OpenMeterOptions.cs`: Binds to configuration settings for OpenMeter endpoints and tokens.
  - `MeteringDependencyInjection.cs`: Extension methods wiring OpenMeter clients and resilient HTTP pipelines.
- `Outbox`:
  - `ModuleOutboxProcessor.cs`: Abstract per-module background worker. Claims due rows with `FOR UPDATE SKIP LOCKED` (and reclaims rows a crashed worker left in Processing once their lease expires, counting that as an attempt), publishes through `IMessageBus`, deletes on success, backs off and dead-letters after `MaxAttempts`. Wakes on `OutboxSignal` and sweeps every poll interval as a fallback; a failed pass is logged, never fatal to the host.
  - `OutboxSignal.cs`: Single-slot, drop-write channel per module DbContext (ported from apps/backend's `ChannelOutboxSignal`) that coalesces commits into one wake-up.
  - `OutboxCommitSignalInterceptor.cs`: EF Core save-changes + transaction interceptor that notifies the signal only after rows added to the outbox are committed (never on rollback).
  - `OutboxSignalRegistrationExtensions.cs`: `AddOutboxSignal<TDbContext>()` and `AddOutboxSignalInterceptor<TDbContext>(sp)` used by each module's DI.
  - `IOutboxDispatcher.cs`: `ProcessBatchAsync` (one pass) and `DrainAsync` (until nothing is due or in flight) for diagnostics and deterministic tests.
- `Persistence`:
  - `OutboxMessage.cs`: Persistence model representing outbox records, storing serialized JSON payloads, event discriminator, occurrence time, processed time, and error trace.
  - `ModelBuilderExtensions.cs`: Fluent EF Core configuration extensions (`ConfigureOutbox`, `ConfigureDeadLetter`, `ConfigureInbox`) for mapping outbox and DLQ tables uniformly across modular DbContexts.
  - `DomainEventAccessor.cs`: Utility for extracting and clearing uncommitted `BaseEvent` instances from tracked aggregate roots in EF Core `ChangeTracker`.
