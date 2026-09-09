# Notifications HTTP API Contracts

## Purpose
Contains request and response Data Transfer Objects (DTOs) for the Notifications module exposed over HTTP via `NotificationsController`.

## Contracts
- `NotificationPreferenceResponse`: DTO representing a user's notification settings (`CampaignUpdatesEnabled`, `MarketingAnnouncementsEnabled`, `UpdatedAtUtc`).
- `UpdateNotificationPreferencesRequest`: Request payload for modifying notification preferences.
- `UnsubscribeResponse`: Confirmation DTO returned by one-click unsubscribe actions.
