# Crowd Funding Modules Contributions Infrastructure

## Purpose
Contains infrastructure implementations for the Contributions module, including EF Core persistence, services, and transaction executors.

## Files
- `CrowdFunding.Modules.Contributions.Infrastructure.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `DependencyInjection`: Registers Contributions services, handlers, validators, and infrastructure components with the dependency injection container.
- `Persistence`: Groups persistence concerns for the Contributions module, including EF Core configuration, db contexts, repositories, and migrations.
- `Services`: Contains infrastructure service implementations used by the Contributions module.
- `Transactions`: Contains transaction executor implementations that coordinate persistence and event publication for the Contributions module.
