# Identity

## Purpose
Contains source files related to Identity.

## Files
- No direct files live in this folder; it primarily organizes child folders.

## Child Folders
- `CrowdFunding.Modules.Identity.Application`: Contains the application-layer orchestration for the Identity module, including handlers, validators, and service abstractions.
- `CrowdFunding.Modules.Identity.Contracts`: Contains integration-facing contracts for the Identity module, such as cross-module commands, queries, and events.
- `CrowdFunding.Modules.Identity.Domain`: Contains the core domain model for the Identity module, including invariants, events, and enums.
- `CrowdFunding.Modules.Identity.Infrastructure`: Contains infrastructure implementations for the Identity module, including EF Core persistence, services, and transaction executors.
