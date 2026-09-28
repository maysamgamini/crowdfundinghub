# Crowd Funding Modules Moderation Application

## Purpose
Contains the application-layer orchestration for the Moderation module, including handlers, validators, and service abstractions.

## Files
- `CrowdFunding.Modules.Moderation.Application.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `Abstractions`: Ports (persistence, services, transactions) implemented by Infrastructure.
- `DependencyInjection`: Registers Moderation services, handlers, validators, and infrastructure components with the dependency injection container.
- `Features`: Campaign review commands, queries, and event handlers.
