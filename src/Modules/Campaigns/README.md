# Campaigns Module

## Purpose
The Campaigns module manages the core crowdfunding campaign lifecycle: draft creation, story and goal definition, publication, cancellation, and funding total tracking.

## Capabilities & Workflows
- **Draft & Create**: Creators submit campaign proposals with title, description, funding target (`Money`), deadline, and category. Emits `CampaignCreatedApplicationEvent` which enqueues a moderation review.
- **Review & Publish**: Once approved by moderation (checked through Moderation's `ICampaignReviewStatusReader` contract), the campaign owner can move the campaign from `Draft` to `Published` (`PublishCampaignCommand`), emitting `CampaignPublishedApplicationEvent`.
- **Cancel Campaign**: Campaigns may be cancelled by creators or administrators before expiration, emitting `CampaignCancelledApplicationEvent`.
- **Deadline Resolution**: `CampaignExpirationBackgroundService` moves a published campaign past its deadline to `Successful` (`CampaignSucceededApplicationEvent`) or `Failed` (`CampaignFailedApplicationEvent`, which starts the refund saga in Contributions).
- **Reward Tiers**: Limited-quantity perks with time-limited slot reservations; `RewardTierReservationScavengerBackgroundService` releases abandoned reservations, and failed or refunded payments release their slot.
- **Append-only funding ledger**: A confirmed contribution appends a pledge entry (`Campaign.RecordPledge`), a refund appends a compensating negative entry (`Campaign.RecordRefund`). `RaisedAmount` is a snapshot the database recomputes as the sum of the entries; PostgreSQL rejects duplicate entries, UPDATE/DELETE of entries, and any `raised_amount` that differs from the sum. Connected clients are notified via SignalR. See [`docs/append-only-ledger.md`](../../../docs/append-only-ledger.md).
- **Distributed Caching**: Details for hot campaigns are cached using `CachedCampaignReadService`, evicted after the commit of every write (including ledger entries).

## Projects & Layers
- `CrowdFunding.Modules.Campaigns.Domain`: Contains the `Campaign` aggregate root, `CampaignStatus` enum, `CampaignLedgerEntry`, `RewardTier`, `RewardTierReservation`, and domain events (created, published, cancelled, succeeded, failed).
- `CrowdFunding.Modules.Campaigns.Application`: Implements CQRS command handlers (`CreateCampaign`, `PublishCampaign`, `CancelCampaign`, `CompleteCampaign`, `FailCampaign`, `AddContributionToCampaign`, `RecordContributionRefund`, and the reward-tier commands), query handlers (`GetCampaignById`, `ListCampaigns`, reward-tier queries), and event handlers (`ContributionPaymentConfirmedApplicationEventHandler`, `ContributionRefundedLedgerHandler`, reward-tier release handlers).
- `CrowdFunding.Modules.Campaigns.Infrastructure`: EF Core `CampaignsDbContext`, configurations, repositories, the ledger, caching decorators, background services, and the transactional outbox processor.
- `CrowdFunding.Modules.Campaigns.Contracts`: Integration commands (`AddContributionToCampaignCommand`), application events, and `CampaignStatusContract` shared with other modules.
