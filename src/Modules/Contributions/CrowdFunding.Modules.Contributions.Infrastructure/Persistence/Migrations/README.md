# Migrations

## Purpose
Contains the EF Core migration history for the Contributions module. These files are generated snapshots of schema evolution.

## Files
- `20260407024123_InitialContributionsSchema.Designer.cs`: Generated model snapshot for the migration above.
- `20260407024123_InitialContributionsSchema.cs`: Migration: Initial Contributions Schema.
- `20260407181607_AddContributionPaymentWorkflow.Designer.cs`: Generated model snapshot for the migration above.
- `20260407181607_AddContributionPaymentWorkflow.cs`: Migration: Add Contribution Payment Workflow.
- `20260408044512_AddOutboxMessages.Designer.cs`: Generated model snapshot for the migration above.
- `20260408044512_AddOutboxMessages.cs`: Migration: Add Outbox Messages.
- `20260908191531_UpgradeOutboxAndAddDeadLetter.Designer.cs`: Generated model snapshot for the migration above.
- `20260908191531_UpgradeOutboxAndAddDeadLetter.cs`: Migration: Upgrade Outbox And Add Dead Letter.
- `20260908215419_AddContributionConcurrencyToken.Designer.cs`: Generated model snapshot for the migration above.
- `20260908215419_AddContributionConcurrencyToken.cs`: Migration: Add Contribution Concurrency Token.
- `20260908222900_AddContributionIndexes.Designer.cs`: Generated model snapshot for the migration above.
- `20260908222900_AddContributionIndexes.cs`: Migration: Add Contribution Indexes.
- `20260909001053_AddOutboxHeaders.Designer.cs`: Generated model snapshot for the migration above.
- `20260909001053_AddOutboxHeaders.cs`: Migration: Add Outbox Headers.
- `20260909002643_AddActiveCampaignsCache.Designer.cs`: Generated model snapshot for the migration above.
- `20260909002643_AddActiveCampaignsCache.cs`: Migration: Add Active Campaigns Cache.
- `20260909023950_AddPaymentGatewayReconciliation.Designer.cs`: Generated model snapshot for the migration above.
- `20260909023950_AddPaymentGatewayReconciliation.cs`: Migration: Add Payment Gateway Reconciliation.
- `20260909154621_AddContributionRewardTierReservationId.Designer.cs`: Generated model snapshot for the migration above.
- `20260909154621_AddContributionRewardTierReservationId.cs`: Migration: Add Contribution Reward Tier Reservation Id.
- `20260909180000_AddActiveCampaignCacheOwnerId.Designer.cs`: Generated model snapshot for the migration above.
- `20260909180000_AddActiveCampaignCacheOwnerId.cs`: Migration: Add Active Campaign Cache Owner Id.
- `20260928164036_AddInboxProcessedEvents.Designer.cs`: Generated model snapshot for the migration above.
- `20260928164036_AddInboxProcessedEvents.cs`: Migration: Add Inbox Processed Events.
- `ContributionsDbContextModelSnapshot.cs`: Generated snapshot of the current model, used to compute the next migration.

## Child Folders
- None.

## Notes
Avoid hand-editing migration designer files unless you are intentionally repairing generated output.
