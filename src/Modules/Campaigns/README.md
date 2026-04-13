# Campaigns

## Purpose
Contains source files related to Campaigns.

## Files
- No direct files live in this folder; it primarily organizes child folders.

## Child Folders
- `CrowdFunding.Modules.Campaigns.Application`: Contains the application-layer orchestration for the Campaigns module, including handlers, validators, and service abstractions.
- `CrowdFunding.Modules.Campaigns.Contracts`: Contains integration-facing contracts for the Campaigns module, such as cross-module commands, queries, and events.
- `CrowdFunding.Modules.Campaigns.Domain`: Contains the core domain model for the Campaigns module, including invariants, events, and enums.
- `CrowdFunding.Modules.Campaigns.Infrastructure`: Contains infrastructure implementations for the Campaigns module, including EF Core persistence, services, and transaction executors.
