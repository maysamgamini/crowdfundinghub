# Crowd Funding Modules Notifications Domain

## Purpose
Contains the core domain model for the Notifications module, including invariants, events, and enums.

## Files
- `CrowdFunding.Modules.Notifications.Domain.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `Aggregates`: Contains domain aggregates such as `NotificationPreference.cs`, enforcing GDPR consent rules, default opt-out for commercial marketing, and opt-in/opt-out status for campaign progress updates (TICKET-056).
