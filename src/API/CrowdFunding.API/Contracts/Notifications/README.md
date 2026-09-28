# Notifications HTTP API Contracts

## Purpose
Contains request and response Data Transfer Objects (DTOs) for the Notifications module exposed over HTTP via `Endpoints/NotificationEndpoints.cs`.

## Files
- `NotificationContracts.cs`: Holds all four records below.

## Contracts
- `NotificationPreferencesResponse`: DTO representing a user's notification settings (`CampaignUpdatesEnabled`, `MarketingAnnouncementsEnabled`, `UpdatedAtUtc`).
- `UpdateNotificationPreferencesRequest`: Request payload for modifying notification preferences.
- `UnsubscribeRequest`: Request payload for the one-click unsubscribe action (optional `UserId`).
- `UnsubscribeResponse`: Confirmation DTO returned by the one-click unsubscribe action.
