# Crowd Funding Modules Notifications Domain

## Purpose
Contains the core domain model for the Notifications module, the `NotificationPreference` aggregate.

## Files
- `CrowdFunding.Modules.Notifications.Domain.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `Aggregates`: `NotificationPreference.cs` (derives from `BaseEntity`), a user's opt-ins for campaign update emails and marketing announcements, with marketing off by default (GDPR consent, CAN-SPAM opt-out). Transactional email is not governed by it.
