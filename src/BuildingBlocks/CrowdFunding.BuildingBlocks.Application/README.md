# Building Blocks: Application Layer

## Purpose
Provides shared application-layer orchestration primitives, CQRS dispatchers, event registry and bus contracts, pagination models, security abstractions, domain-agnostic exceptions, and usage metering interfaces.

## Subdirectories
- `Events`: Event handling primitives including `IEventHandler<TEvent>`, `IEventPublisher`, and dynamic discriminator/version resolver `EventTypeRegistry`. `BaseApplicationEvent.EventId` gives every integration event a stable identity that survives serialization; the outbox row id and the CloudEvents `id` reuse it.
- `Exceptions`: Technology-agnostic application exceptions: `NotFoundException` (404), `ResourceConflictException` (409), `RequestValidationException` (400 with per-property errors) and `ConcurrencyConflictException` (409, lets handlers detect optimistic-concurrency losses without depending on EF Core). Handlers throw `NotFoundException` rather than the BCL `KeyNotFoundException`, which any dictionary lookup also throws.
- `Messaging`: In-process CQRS abstractions (`ICommand<TResult>`, `IQuery<TResult>`, `ICommandHandler<TCommand, TResult>`, `IQueryHandler<TQuery, TResult>`) along with `CommandDispatcher`, `QueryDispatcher`, and assembly scanning extension methods.
- `Inbox`: `IIdempotentConsumer`, the consumer side of the outbox: `ExecuteOnceAsync(eventId, consumerName, effect)` applies an effect at most once per consumer however often a message is redelivered. Each module exposes its own derived interface backed by its own DbContext.
- `Metering`: Monetization and telemetry interfaces including `IUsageMeteringClient`, CloudEvents v1.0 schema records (`CloudEvent`), and fee calculation models (`OpenMeterFeeOptions`).
- `Pagination`: Reusable pagination models including `PageRequest` and generic `PagedResult<TItem>`.
- `Validation`: `ValidationPipelineBehavior<TCommand,TResult>` runs every registered FluentValidation validator for a command before its handler and throws `RequestValidationException` (mapped to an RFC 9457 400 with an `errors` member). `AddCommandValidation()` registers it; hosts call it before any other behavior so it runs outermost. Endpoints never validate by hand.
- `Security`: Contextual authentication and authorization contracts including `ICurrentUser`, plus `UnauthenticatedException` (401) and `ForbiddenAccessException` (403). Handlers throw `UnauthenticatedException` rather than the BCL `UnauthorizedAccessException`, which the runtime throws for file-system permission failures.
