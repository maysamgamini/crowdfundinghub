# Contributions: Application Layer

## Purpose
Orchestrates use cases and business workflows for the Contributions module, including contribution creation, payment confirmation/failure, payment-webhook reconciliation, the refund saga, replication of campaign state, and CloudEvent usage metering.

## Subdirectories
- `Abstractions`: Persistence repository abstractions (`IContributionRepository`, `IActiveCampaignCacheRepository`), the inbox (`IContributionsIdempotentConsumer`), the transaction executor contract (`IContributionTransactionExecutor`), the read service (`IContributionReadService`), and the time provider interface (`IContributionDateTimeProvider`).
- `DependencyInjection`: Extension methods registering command and query handlers and validators.
- `Features`:
  - `ActiveCampaigns/Events`: `ReplicatedCampaignEventHandlers` keeps the local `active_campaigns_cache` replica in step with Campaigns' application events.
  - `Contributions/Commands`: Handlers for `MakeContribution`, `ConfirmContributionPayment`, `FailContributionPayment`, and `ReconcilePaymentWebhook`.
  - `Contributions/Queries`: Handlers for `GetContributionById` and `ListContributionsByCampaign`.
  - `Contributions/Events`: `CampaignTerminationRefundHandler`, the refund saga.
  - `Metering`: `PledgeConfirmedMeteringEventHandler` translating confirmed pledges into OpenMeter CloudEvents.
