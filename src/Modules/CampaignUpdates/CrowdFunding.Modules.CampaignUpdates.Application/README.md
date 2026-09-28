# Crowd Funding Modules Campaign Updates Application

## Purpose
Contains the application-layer orchestration for the Campaign Updates module: webhook subscription use cases, the event handlers that replicate campaign owners and fan out pledge webhooks, and the ports Infrastructure implements.

## Files
- `CrowdFunding.Modules.CampaignUpdates.Application.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `Abstractions`: Ports implemented by Infrastructure: `Inbox/ICampaignUpdatesIdempotentConsumer` (the module's `IIdempotentConsumer`), `Persistence/` (`IWebhookSubscriptionRepository`, `IWebhookDeliveryTaskRepository` with its `FOR UPDATE SKIP LOCKED` claim, `ICampaignOwnerCacheRepository`), and `Services/ICampaignUpdatesDateTimeProvider`.
- `DependencyInjection`: Registers Campaign Updates handlers and validators with the dependency injection container.
- `Events`: Application event handlers for campaign and contribution events.
- `Features`: `WebhookSubscriptions/Commands` (`RegisterWebhookSubscription`, `DeleteWebhookSubscription`) and `WebhookSubscriptions/Queries` (`GetWebhookSubscriptionById`, `ListWebhookSubscriptionsByCampaign`). Query results never include the signing secret.
- `Security`: `UrlSecurityValidator`, which rejects webhook targets that are not public HTTPS URLs (loopback, private ranges, link-local, cloud metadata).
