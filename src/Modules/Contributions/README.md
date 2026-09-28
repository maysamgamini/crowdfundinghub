# Contributions Module

## Purpose
The Contributions module manages the financial lifecycle of backer pledges: pledge creation, payment confirmation or failure (directly or through a signed payment-gateway webhook), refunds when a campaign fails or is cancelled, and revenue usage metering.

## Capabilities & Workflows
- **Pledge Creation**: Backers create pledges (`MakeContributionCommand`), optionally holding a reward-tier reservation. The handler validates the campaign against the module's own replicated read model (`active_campaigns_cache`, filled by `ReplicatedCampaignEventHandlers` from Campaigns' application events), never by a synchronous call into Campaigns: the campaign must exist, be published, be before its deadline, use the same currency, and not be owned by the backer.
- **Payment Processing**:
  - `ConfirmContributionPaymentCommand`: Moves the contribution from `Pending` to `Succeeded`, records the payment reference, and raises `ContributionPaymentConfirmedApplicationEvent` (Campaigns appends the pledge to its funding ledger).
  - `FailContributionPaymentCommand`: Moves the contribution to `Failed` with a reason and raises `ContributionPaymentFailedApplicationEvent` (the reward-tier reservation is released).
  - `ReconcilePaymentWebhookCommand`: Applies a signature-verified payment-gateway event (`payment_intent.succeeded` / `payment_intent.payment_failed`) to the matching contribution. Each gateway event id is claimed once through the inbox (`IContributionsIdempotentConsumer`, table `contributions_processed_events`) in the same transaction as the state change, so redeliveries are ignored.
- **Refund Saga**: `CampaignTerminationRefundHandler` reacts to `CampaignFailedApplicationEvent` and `CampaignCancelledApplicationEvent` by refunding every succeeded contribution in batches, raising `ContributionRefundedApplicationEvent` for each (Campaigns appends a compensating ledger entry; Notifications sends the backer an alert).
- **Optimistic Concurrency**: The `xmin` concurrency token on `contributions` makes a confirm and a fail racing on the same pending contribution fail detectably instead of overwriting each other.
- **Monetization & Metering**: Confirmed pledges trigger `PledgeConfirmedMeteringEventHandler`, which calculates platform fees and emits normalized CloudEvents to OpenMeter.

## Persistence
One `ContributionsDbContext` owning the tables `contributions`, `active_campaigns_cache`, `contributions_outbox_messages`, `contributions_dead_letter_events`, and `contributions_processed_events`. The tables and the EF migrations history live in the `contributions` schema. There are no foreign keys to other modules' tables, and `ConnectionStrings:ContributionsDb` (falling back to `DefaultConnection`) lets the module move to its own database.

## Projects & Layers
- `CrowdFunding.Modules.Contributions.Domain`: Contains the `Contribution` aggregate root, `ContributionStatus` enum (`Pending`, `Succeeded`, `Failed`, `Refunded`), and domain events.
- `CrowdFunding.Modules.Contributions.Application`: Orchestrates contribution commands, query handlers, the refund saga, campaign replication, and metering event handlers.
- `CrowdFunding.Modules.Contributions.Infrastructure`: EF Core `ContributionsDbContext`, persistence configurations, repositories, the inbox consumer, and the outbox worker.
- `CrowdFunding.Modules.Contributions.Contracts`: Cross-module application events (`ContributionPaymentConfirmedApplicationEvent`, `ContributionPaymentFailedApplicationEvent`, `ContributionRefundedApplicationEvent`).
