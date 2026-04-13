# Moderation

## Purpose
Contains source files related to Moderation.

## Files
- No direct files live in this folder; it primarily organizes child folders.

## Child Folders
- `CrowdFunding.Modules.Moderation.Application`: Contains the application-layer orchestration for the Moderation module, including handlers, validators, and service abstractions.
- `CrowdFunding.Modules.Moderation.Contracts`: Contains integration-facing contracts for the Moderation module, such as cross-module commands, queries, and events.
- `CrowdFunding.Modules.Moderation.Domain`: Contains the core domain model for the Moderation module, including invariants, events, and enums.
- `CrowdFunding.Modules.Moderation.Infrastructure`: Contains infrastructure implementations for the Moderation module, including EF Core persistence, services, and transaction executors.
