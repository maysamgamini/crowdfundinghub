# Crowd Funding Building Blocks Infrastructure

## Purpose
Provides reusable infrastructure helpers for persistence and event publishing.

## Files
- `CrowdFunding.BuildingBlocks.Infrastructure.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `Events`: Contains infrastructure implementations that resolve and invoke application event handlers.
- `Persistence`: Contains shared persistence helpers such as outbox storage and EF Core model configuration extensions.
