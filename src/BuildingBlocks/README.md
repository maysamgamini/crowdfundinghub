# Building Blocks Layer

## Purpose
Contains foundational, shared, cross-cutting primitives, abstractions, value objects, and infrastructure patterns utilized across all functional modules in the modular monolith.

## Architecture & Boundaries
To maintain strict clean architecture and domain isolation:
- **Zero Module Dependencies**: Building block libraries never reference any business module (`Campaigns`, `Contributions`, `Identity`, etc.).
- **Strict Inward Dependency Flow**:
  - `CrowdFunding.BuildingBlocks.Domain` has zero external dependencies.
  - `CrowdFunding.BuildingBlocks.Application` references only `BuildingBlocks.Domain` (plus the FluentValidation and DI abstractions packages).
  - `CrowdFunding.BuildingBlocks.Infrastructure` implements application and domain contracts using concrete frameworks (EF Core, Npgsql, Redis, RabbitMQ, OpenMeter HTTP).

## Projects
- `CrowdFunding.BuildingBlocks.Domain`: Core entity bases (`BaseEntity`), domain events (`BaseEvent`), common value objects (`Money`), concurrency locking primitives (`AdvisoryLockKey`), and the domain exception hierarchy (`DomainException`, `BusinessRuleViolationException`, `DomainConflictException`).
- `CrowdFunding.BuildingBlocks.Application`: In-process CQRS messaging (`ICommandHandler`, `IQueryHandler`, the dispatchers and `ICommandPipelineBehavior`), the validation and audit pipeline behaviors, event abstractions (`IEventPublisher`, `IEventHandler`, `IMessageBus`, `EventTypeRegistry`), the inbox contract (`IIdempotentConsumer`), CloudEvents usage metering interfaces (`IUsageMeteringClient`), pagination contracts (`PageRequest`, `PagedResult`), technology-agnostic exceptions (`NotFoundException`, `ResourceConflictException`, `ConcurrencyConflictException`, `RequestValidationException`), and security context contracts (`ICurrentUser`, `IRequestContext`).
- `CrowdFunding.BuildingBlocks.Infrastructure`: Transactional outbox persistence and processing (`OutboxMessage`, `ModuleOutboxProcessor`, `OutboxSignal`), the inbox (`EfIdempotentConsumer`), message buses (`InProcessMessageBus`, publish-only `RabbitMqMessageBus`), exception classification (`ExceptionClassifier`), the audit store (`AuditDbContext`), Redis cache invalidation, per-module connection strings (`ConfigurationExtensions`), domain event dispatchers (`ServiceProviderEventPublisher`), and OpenMeter HTTP metering integrations (`OpenMeterClient`).
