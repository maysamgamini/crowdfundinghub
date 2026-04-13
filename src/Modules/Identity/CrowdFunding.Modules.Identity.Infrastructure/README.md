# Crowd Funding Modules Identity Infrastructure

## Purpose
Contains infrastructure implementations for the Identity module, including EF Core persistence, services, and transaction executors.

## Files
- `CrowdFunding.Modules.Identity.Infrastructure.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `DependencyInjection`: Registers Identity services, handlers, validators, and infrastructure components with the dependency injection container.
- `Persistence`: Groups persistence concerns for the Identity module, including EF Core configuration, db contexts, repositories, and migrations.
- `Services`: Contains infrastructure service implementations used by the Identity module.
