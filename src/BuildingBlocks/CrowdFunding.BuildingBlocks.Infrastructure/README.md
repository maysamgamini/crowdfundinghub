# Building Blocks: Infrastructure Layer

## Purpose
Provides shared technical implementations for persistence, transactional outbox and inbox, message buses, exception classification, auditing, distributed cache invalidation, domain event dispatching, and usage metering services.

## Subdirectories
- `Audit`:
  - `AuditDbContext.cs`: Cross-cutting DbContext for the audit trail, owned by no module; its table `audit_records` and migration history live in the `system` schema.
  - `AuditRecordConfiguration.cs`: Maps `AuditRecord` to `system.audit_records`.
  - `EfAuditStore.cs`: Implements `IAuditStore` with a single insert.
  - `AuditDependencyInjection.cs`: `AddAuditInfrastructure` registers the audit-logging behavior and its store.
  - `Migrations`: EF Core migrations for `AuditDbContext`.
- `Caching`:
  - `IDistributedCacheInvalidationPublisher.cs` / `RedisDistributedCacheInvalidationPublisher.cs`: Publish a fire-and-forget Redis Pub/Sub notification so other instances evict their cached copies; a failed publish is logged, not surfaced.
  - `CacheInvalidationSubscription.cs` / `DistributedCacheInvalidationSubscriber.cs`: Modules declare channel handlers; one hosted service per instance subscribes to them.
  - `DistributedCacheInvalidationDependencyInjection.cs`: `AddDistributedCacheInvalidation` and `AddCacheInvalidationSubscription`.
- `Configuration`:
  - `ConfigurationExtensions.cs`: `GetRequiredModuleConnectionString` resolves `ConnectionStrings:{Module}Db`, falling back to `DefaultConnection`, so a module can move to its own database by configuration.
- `Inbox`:
  - `ProcessedEvent.cs`: One row per (EventId, ConsumerName) a module has applied (ported from apps/backend). `EventId` is a string so external ids such as Stripe `evt_…` share the table.
  - `EfIdempotentConsumer.cs`: EF Core base for each module's `IIdempotentConsumer`. Claims with `INSERT … ON CONFLICT DO NOTHING` inside the effect's transaction, so a concurrent duplicate waits on the first claim and then skips; a failing effect rolls the claim back.
- `ErrorHandling`:
  - `ExceptionClassifier.cs`: Maps an exception to an HTTP status, title and client-safe detail for every host that uses it, so all return identical RFC 9457 responses for the same failure. Domain and application exception types become 4xx with their own message; database overload (Npgsql pool exhaustion, `53300`, `57P03`) becomes 503; everything else becomes a generic 500 so framework and database messages never reach the client.
- `Events`:
  - `ServiceProviderEventPublisher.cs`: Implements `IEventPublisher` by discovering and invoking all registered `IEventHandler<TNotification>` implementations from the ASP.NET Core `IServiceProvider`.
- `Messaging`:
  - `InProcessMessageBus.cs`: The default `IMessageBus` (`Messaging:Provider=InProcess`): hands events to in-process handlers through `IEventPublisher`.
  - `RabbitMqMessageBus.cs` / `RabbitMqOptions.cs`: Optional publish-only bus (`Messaging:Provider=RabbitMQ`) that sends CloudEvents envelopes to a topic exchange. No RabbitMQ consumer exists in this codebase, so cross-process delivery is not implemented; the in-process bus is the working mode.
  - `CloudEventEnvelope.cs`: CloudEvents v1.0 envelope used only at the process boundary.
  - `MessagingDependencyInjection.cs`: `AddCrowdFundingMessaging` selects the bus from configuration.
- `Metering`:
  - `OpenMeterClient.cs`: Implements `IUsageMeteringClient` over resilient HTTP (`Microsoft.Extensions.Http.Resilience`) targeting OpenMeter Cloud.
  - `OpenMeterOptions.cs`: Binds to configuration settings for OpenMeter endpoints and tokens.
  - `MeteringDependencyInjection.cs`: Extension methods wiring OpenMeter clients and resilient HTTP pipelines.
- `Observability`:
  - `CrowdFundingTelemetry.cs`: Names of this codebase's `ActivitySource`s (for example the outbox source), which hosts pass to the OpenTelemetry SDK.
- `Outbox`:
  - `ModuleOutboxProcessor.cs`: Abstract per-module background worker. Claims due rows with `FOR UPDATE SKIP LOCKED` (and reclaims rows a crashed worker left in Processing once their lease expires, counting that as an attempt), publishes through `IMessageBus`, deletes on success, retries after a fixed 10 s delay, and dead-letters after `MaxAttempts` (5). Wakes on `OutboxSignal` and sweeps every poll interval as a fallback; a failed pass is logged, never fatal to the host.
  - `OutboxSignal.cs`: Single-slot, drop-write channel per module DbContext (ported from apps/backend's `ChannelOutboxSignal`) that coalesces commits into one wake-up.
  - `OutboxCommitSignalInterceptor.cs`: EF Core save-changes + transaction interceptor that notifies the signal only after rows added to the outbox are committed (never on rollback).
  - `OutboxSignalRegistrationExtensions.cs`: `AddOutboxSignal<TDbContext>()` and `AddOutboxSignalInterceptor<TDbContext>(sp)` used by each module's DI.
  - `IOutboxDispatcher.cs`: `ProcessBatchAsync` (one pass) and `DrainAsync` (until nothing is due or in flight) for diagnostics and deterministic tests.
- `Persistence`:
  - `OutboxMessage.cs`: Persistence model representing outbox records, storing serialized JSON payloads, event type and version, attempts, lease (`LockedUntilUtc`), scheduled time, and last error.
  - `ModelBuilderExtensions.cs`: Fluent EF Core configuration extensions (`ConfigureOutbox`, `ConfigureDeadLetter`, `ConfigureInbox`) for mapping outbox and DLQ tables uniformly across modular DbContexts.
  - `DomainEventAccessor.cs`: Utility for extracting and clearing uncommitted `BaseEvent` instances from tracked aggregate roots in EF Core `ChangeTracker`.
  - `OutboxClaimQuery.cs`: The `FOR UPDATE SKIP LOCKED` batch claim used by the outbox processor.
  - `DeadLetterEvent.cs`: Dead-letter record for messages that are unparseable or out of retries.
  - `OutboxMessageStatus.cs`: `Pending`, `Processing`, `Processed`, `DeadLetter`.
