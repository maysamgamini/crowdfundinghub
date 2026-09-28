# Crowd Funding Modules Contributions Infrastructure

## Purpose
Contains infrastructure implementations for the Contributions module, including EF Core persistence, the inbox consumer, the outbox worker, services, and transaction executors.

## Files
- `CrowdFunding.Modules.Contributions.Infrastructure.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `Inbox`: `ContributionsIdempotentConsumer`, the module's `IIdempotentConsumer` over `contributions_processed_events`. The payment-gateway webhook reconciliation claims each gateway event id through it, in the same transaction as the contribution's state change.
- `DependencyInjection`: Registers Contributions services, handlers, validators, and infrastructure components with the dependency injection container.
- `Outbox`: `ContributionsOutboxBackgroundService`, the module's outbox worker (a `ModuleOutboxProcessor` over `ContributionsDbContext`).
- `Persistence`: Groups persistence concerns for the Contributions module, including EF Core configuration, db contexts, read models, repositories, and migrations.
- `Services`: Contains infrastructure service implementations used by the Contributions module.
- `Transactions`: Contains transaction executor implementations that coordinate persistence and event publication for the Contributions module.
