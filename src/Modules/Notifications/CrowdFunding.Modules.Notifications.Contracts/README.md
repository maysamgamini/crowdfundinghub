# Notifications Contracts

## Purpose
The public contract of the Notifications module: what another module (or a future extracted service) may ask Notifications without reading its database.

## Files
- `Queries/GetNotificationPreferences/GetNotificationPreferencesQuery.cs`: `GetNotificationPreferencesQuery` and `GetNotificationPreferencesResult`, a user's opt-ins (defaults when nothing was saved). The handler lives in `Notifications.Application`; the API dispatches the query through `IQueryDispatcher`, and any module that must respect a user's opt-outs would do the same.

## Dependency rule
No project references beyond the BCL. Other modules may reference this project; never the module's Application, Domain or Infrastructure (enforced by `ModuleBoundaryConventionTests`).
