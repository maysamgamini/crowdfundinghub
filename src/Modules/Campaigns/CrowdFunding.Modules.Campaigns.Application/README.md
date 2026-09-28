# Crowd Funding Modules Campaigns Application

## Purpose
Contains the application-layer orchestration for the Campaigns module, including handlers, validators, and service abstractions.

## Files
- `CrowdFunding.Modules.Campaigns.Application.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `Abstractions`: Ports (persistence, services, transactions) implemented by Infrastructure.
- `DependencyInjection`: Registers Campaigns services, handlers, validators, and infrastructure components with the dependency injection container.
- `Features`: Commands, queries, and event handlers for campaigns and reward tiers.
