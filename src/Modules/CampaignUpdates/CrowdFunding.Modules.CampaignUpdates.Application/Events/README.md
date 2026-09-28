# Events

## Purpose
Contains the Campaign Updates module's handlers for application events published by other modules.

## Files
- `CampaignActivityEventHandlers.cs`: `CampaignCreatedActivityHandler` replicates the campaign owner into `campaign_owner_cache`; `ContributionPaymentConfirmedActivityHandler` enqueues one `WebhookDeliveryTask` per active subscription, exactly once per event through `ICampaignUpdatesIdempotentConsumer`. The published, cancelled, review-approved, and review-rejected handlers are no-ops.

## Child Folders
- None.
