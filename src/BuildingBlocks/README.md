# Building Blocks Layer

## Purpose
Contains foundational, shared, cross-cutting primitives, abstractions, value objects, and infrastructure patterns utilized across all functional modules in the modular monolith.

## Architecture & Boundaries
To maintain strict clean architecture and domain isolation:
- **Zero Module Dependencies**: Building block libraries never reference any business module (`Campaigns`, `Contributions`, `Identity`, etc.).
- **Strict Inward Dependency Flow**:
  - `CrowdFunding.BuildingBlocks.Domain` has zero external dependencies.
  - `CrowdFunding.BuildingBlocks.Application` references only `BuildingBlocks.Domain`.
  - `CrowdFunding.BuildingBlocks.Infrastructure` implements application and domain contracts using concrete frameworks (EF Core, Npgsql, OpenMeter, etc.).

## Projects
- `CrowdFunding.BuildingBlocks.Domain`: Core entity bases (`BaseEntity`), domain events (`BaseEvent`), common value objects (`Money`), and concurrency locking primitives (`AdvisoryLockKey`).
- `CrowdFunding.BuildingBlocks.Application`: In-process CQRS messaging (`ICommand`, `IQuery`, `ICommandHandler`, `IQueryHandler`), event bus abstractions (`IEventPublisher`, `IEventHandler`), CloudEvents usage metering interfaces (`IUsageMeteringClient`), pagination contracts (`PageRequest`, `PagedResult`), technology-agnostic exceptions (`ConcurrencyConflictException`), and security context contracts (`ICurrentUser`).
- `CrowdFunding.BuildingBlocks.Infrastructure`: Transactional outbox persistence (`OutboxMessage`, `ModelBuilderExtensions`), domain event dispatchers (`ServiceProviderEventPublisher`), and OpenMeter HTTP metering integrations (`OpenMeterClient`).

## How the pieces fit

```mermaid
flowchart TB
    subgraph Domain["BuildingBlocks.Domain"]
        BaseEntity["BaseEntity / BaseEvent"]
        Money["Money"]
        Lock["AdvisoryLockKey"]
    end
    subgraph App["BuildingBlocks.Application"]
        Cqrs["ICommand / IQuery / dispatchers"]
        Pub["IEventPublisher / IEventHandler"]
        Inbox["IInbox / InboxMessageContext"]
    end
    subgraph Infra["BuildingBlocks.Infrastructure"]
        Outbox["OutboxMessage, OutboxClaimQuery, ModuleOutboxProcessor"]
        EfInbox["EfInbox, InboxRegistration"]
        Bus["ServiceProviderEventPublisher, message bus"]
    end
    App --> Domain
    Infra --> App
    Infra --> Domain
```

## Outbox to inbox, in one picture

```mermaid
sequenceDiagram
    participant P as ModuleOutboxProcessor
    participant Db as module schema
    participant Pub as ServiceProviderEventPublisher
    participant H as IEventHandler
    Note over P: sleeps on IOutboxSignal, woken by a commit, 5 s safety-net timer
    P->>Db: claim Pending rows (SKIP LOCKED), mark Processing
    P->>Pub: publish with InboxMessageContext.MessageId
    Pub->>Db: already in inbox for this handler? skip
    Pub->>H: Handle
    Pub->>Db: record (MessageId, handler) in inbox, ON CONFLICT DO NOTHING
    P->>Db: delete the row on success, or schedule a retry 10 s later, or dead-letter after 5 attempts
```

How the worker is woken, and the alternatives (LISTEN/NOTIFY, change data capture), are compared in [`docs/EVENT_DELIVERY_APPROACHES.md`](../../docs/EVENT_DELIVERY_APPROACHES.md).

Known limits: the inbox record is written after the handler, so a crash in between runs the handler once more; the RabbitMQ path does not set a message id, so it skips the inbox; inbox rows are never pruned; an outbox row left in `Processing` by a crashed worker is reclaimed after its 30-second lease and dead-lettered after five unfinished claims.
