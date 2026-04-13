# Crowd Funding Modules Campaigns Infrastructure

## Purpose
Contains infrastructure implementations for the Campaigns module, including EF Core persistence, services, and transaction executors.

## Files
- `CrowdFunding.Modules.Campaigns.Infrastructure.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `DependencyInjection`: Registers Campaigns services, handlers, validators, and infrastructure components with the dependency injection container.
- `Persistence`: Groups persistence concerns for the Campaigns module, including EF Core configuration, db contexts, repositories, and migrations.
- `Services`: Contains infrastructure service implementations used by the Campaigns module.
- `Transactions`: Contains transaction executor implementations that coordinate persistence and event publication for the Campaigns module.
