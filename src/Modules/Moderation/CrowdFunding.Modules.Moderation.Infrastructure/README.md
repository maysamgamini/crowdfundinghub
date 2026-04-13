# Crowd Funding Modules Moderation Infrastructure

## Purpose
Contains infrastructure implementations for the Moderation module, including EF Core persistence, services, and transaction executors.

## Files
- `CrowdFunding.Modules.Moderation.Infrastructure.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `DependencyInjection`: Registers Moderation services, handlers, validators, and infrastructure components with the dependency injection container.
- `Persistence`: Groups persistence concerns for the Moderation module, including EF Core configuration, db contexts, repositories, and migrations.
- `Services`: Contains infrastructure service implementations used by the Moderation module.
- `Transactions`: Contains transaction executor implementations that coordinate persistence and event publication for the Moderation module.
