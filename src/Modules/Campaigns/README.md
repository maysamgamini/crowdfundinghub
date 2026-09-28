# Campaigns Module

## Purpose
The Campaigns module manages the core crowdfunding campaign lifecycle: draft creation, story and goal definition, publication, cancellation, and funding total tracking.

## Capabilities & Workflows
- **Draft & Create**: Creators submit campaign proposals with title, description, funding target (`Money`), deadline, and category. Emits `CampaignCreatedApplicationEvent` which enqueues a moderation review.
- **Review & Publish**: Once approved by moderation, the campaign owner can transition the campaign to `Active` status (`PublishCampaignCommand`), emitting `CampaignPublishedApplicationEvent`.
- **Cancel Campaign**: Campaigns may be cancelled by creators or administrators before expiration, emitting `CampaignCancelledApplicationEvent`.
- **Append-only funding ledger**: A confirmed contribution appends a pledge entry (`Campaign.RecordPledge`), a refund appends a compensating negative entry (`Campaign.RecordRefund`). `RaisedAmount` is a snapshot the database recomputes as the sum of the entries; PostgreSQL rejects duplicate entries, UPDATE/DELETE of entries, and any `raised_amount` that differs from the sum. Connected clients are notified via SignalR. See [`docs/append-only-ledger.md`](../../../docs/append-only-ledger.md).
- **Distributed Caching**: Details for hot campaigns are cached using `CachedCampaignReadService` with active invalidation upon every balance modification.

## Projects & Layers
- `CrowdFunding.Modules.Campaigns.Domain`: Contains the `Campaign` aggregate root, `CampaignStatus` enum, and domain events (`CampaignCreatedDomainEvent`, `CampaignPublishedDomainEvent`, `CampaignCancelledDomainEvent`).
- `CrowdFunding.Modules.Campaigns.Application`: Implements CQRS command handlers (`CreateCampaign`, `PublishCampaign`, `CancelCampaign`, `AddContributionToCampaign`), query handlers (`GetCampaignById`, `ListCampaigns`, `GetCampaignContributionAvailability`), and event handlers (`ContributionPaymentConfirmedApplicationEventHandler`).
- `CrowdFunding.Modules.Campaigns.Infrastructure`: EF Core `CampaignsDbContext`, configurations, repositories, caching decorators, and transactional outbox.
- `CrowdFunding.Modules.Campaigns.Contracts`: Integration commands, queries, and application events shared with external modules.
