# Building Blocks: Application Layer

## Purpose
Provides shared application-layer orchestration primitives, CQRS dispatchers, event registry and bus contracts, pagination models, security abstractions, domain-agnostic exceptions, and usage metering interfaces.

## Subdirectories
- `Events`: Event handling primitives including `IEventHandler<TEvent>`, `IEventPublisher`, and dynamic discriminator/version resolver `EventTypeRegistry`.
- `Exceptions`: Technology-agnostic application exceptions such as `ConcurrencyConflictException`, enabling optimistic concurrency detection without coupling application code to EF Core or database drivers.
- `Messaging`: In-process CQRS abstractions (`ICommand<TResult>`, `IQuery<TResult>`, `ICommandHandler<TCommand, TResult>`, `IQueryHandler<TQuery, TResult>`) along with `CommandDispatcher`, `QueryDispatcher`, and assembly scanning extension methods.
- `Metering`: Monetization and telemetry interfaces including `IUsageMeteringClient`, CloudEvents v1.0 schema records (`CloudEvent`), and fee calculation models (`OpenMeterFeeOptions`).
- `Pagination`: Reusable pagination models including `PageRequest` and generic `PagedResult<TItem>`.
- `Security`: Contextual authentication and authorization contracts including `ICurrentUser`.
