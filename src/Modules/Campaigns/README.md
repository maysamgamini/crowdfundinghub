# Campaigns Module

## Purpose
The Campaigns module manages the core crowdfunding campaign lifecycle: draft creation, story and goal definition, publication, cancellation, and funding total tracking.

## Capabilities & Workflows
- **Draft & Create**: Creators submit campaign proposals with title, description, funding target (`Money`), deadline, and category. Emits `CampaignCreatedApplicationEvent` which enqueues a moderation review.
- **Review & Publish**: Once approved by moderation, the campaign owner can transition the campaign to `Active` status (`PublishCampaignCommand`), emitting `CampaignPublishedApplicationEvent`.
- **Cancel Campaign**: Campaigns may be cancelled by creators or administrators before expiration, emitting `CampaignCancelledApplicationEvent`.
- **Contribution Ledger**: When contributions are confirmed, `ApplyConfirmedContribution` updates the raised amount idempotently and notifies connected clients in real-time via SignalR.
- **Distributed Caching**: Details for hot campaigns are cached using `CachedCampaignReadService` with active invalidation upon every balance modification.

## Projects & Layers
- `CrowdFunding.Modules.Campaigns.Domain`: Contains the `Campaign` aggregate root, `CampaignStatus` enum, and domain events (`CampaignCreatedDomainEvent`, `CampaignPublishedDomainEvent`, `CampaignCancelledDomainEvent`).
- `CrowdFunding.Modules.Campaigns.Application`: Implements CQRS command handlers (`CreateCampaign`, `PublishCampaign`, `CancelCampaign`, `AddContributionToCampaign`), query handlers (`GetCampaignById`, `ListCampaigns`, `GetCampaignContributionAvailability`), and event handlers (`ContributionPaymentConfirmedApplicationEventHandler`).
- `CrowdFunding.Modules.Campaigns.Infrastructure`: EF Core `CampaignsDbContext`, configurations, repositories, caching decorators, and transactional outbox.
- `CrowdFunding.Modules.Campaigns.Contracts`: Integration commands, queries, and application events shared with external modules.
