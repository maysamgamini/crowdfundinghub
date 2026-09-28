# Crowd Funding Modules Campaign Updates Infrastructure

## Purpose
Contains infrastructure implementations for the Campaign Updates module, including EF Core persistence, services, and transaction executors.

## Files
- `CrowdFunding.Modules.CampaignUpdates.Infrastructure.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `DependencyInjection`: Registers repositories, the idempotent consumer, the SSRF-safe webhook HTTP client and the webhook dispatcher.
- `Inbox`: `CampaignUpdatesIdempotentConsumer`, the module's `IIdempotentConsumer` over `campaign_updates_processed_events`. The pledge-confirmed webhook fan-out runs through it so a redelivered event never enqueues duplicate deliveries.
- `Persistence`: EF Core DbContext, configurations, repositories and migrations (webhook subscriptions, delivery tasks, the replicated campaign-owner cache, processed events).
- `Security`: `WebhookPayloadSigner`, HMAC-SHA256 signing of outbound webhook payloads.
- `Services`: `WebhookDispatcherBackgroundService` (claims due delivery tasks with SKIP LOCKED and POSTs them), the SSRF-safe handler factory and the date-time provider.
