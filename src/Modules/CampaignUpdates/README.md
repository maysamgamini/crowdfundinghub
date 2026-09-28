# Campaign Updates Module (Outbound Webhooks)

## Purpose
The Campaign Updates module lets campaign creators register outbound webhook subscriptions (their CRM, Zapier, an accounting system) and delivers a signed `pledge.confirmed` webhook to each active subscription when a pledge on their campaign is confirmed. It is not an activity feed: it stores no timeline and publishes no application events.

## Capabilities & Event Subscriptions
Subscribes to system events via the handlers in `Events/CampaignActivityEventHandlers.cs`:
- `CampaignCreatedApplicationEvent`: Replicates the campaign's owner into the module's own `campaign_owner_cache` (event-carried state transfer), so subscription registration can check ownership without calling Campaigns.
- `ContributionPaymentConfirmedApplicationEvent`: Fans the pledge out into one durable `WebhookDeliveryTask` per active subscription. The fan-out runs through the inbox (`ICampaignUpdatesIdempotentConsumer`, table `campaign_updates_processed_events`, keyed by the event's `EventId`), so a redelivered event never enqueues duplicate deliveries.
- `CampaignPublishedApplicationEvent`, `CampaignCancelledApplicationEvent`, `CampaignReviewApprovedApplicationEvent`, `CampaignReviewRejectedApplicationEvent`: No-op handlers; nothing is recorded for them.

## Webhook Delivery
- **Registration** (`POST /api/campaigns/{campaignId}/webhook-subscriptions`): owner-only; the target must be a public HTTPS URL (`UrlSecurityValidator` rejects loopback, private, link-local, and cloud-metadata addresses). The signing secret is returned once, in the registration response.
- **Dispatch**: `WebhookDispatcherBackgroundService` claims due delivery tasks with `FOR UPDATE SKIP LOCKED` (with a lease, so a crashed dispatcher's rows are reclaimed) and POSTs them through an SSRF-safe HTTP handler that re-checks the resolved IP at connect time.
- **Signature**: every delivery carries `X-CrowdFunding-Signature: t={unixSeconds},v1={hex HMAC-SHA256 of "{t}.{body}"}` (see `CrowdFunding.Modules.CampaignUpdates.Contracts`).
- **Failures**: a failed delivery is retried on a fixed schedule (1 min, 5 min, 15 min, 1 h) and then marked dead; a subscription that keeps failing disables itself. Delivery is at-least-once, so receivers must deduplicate.

## Projects & Layers
- `CrowdFunding.Modules.CampaignUpdates.Domain`: The `WebhookSubscription` and `WebhookDeliveryTask` aggregates.
- `CrowdFunding.Modules.CampaignUpdates.Application`: Subscription commands and queries, the event handlers, SSRF URL validation, and ports.
- `CrowdFunding.Modules.CampaignUpdates.Infrastructure`: EF Core `CampaignUpdatesDbContext` (tables in the `public` schema, migration history in the `campaignupdates` schema), repositories, the inbox, the payload signer, and the dispatcher.
- `CrowdFunding.Modules.CampaignUpdates.Contracts`: The outbound webhook contract (`WebhookEventTypes`, `PledgeConfirmedWebhookPayload`, `WebhookSignature`).
