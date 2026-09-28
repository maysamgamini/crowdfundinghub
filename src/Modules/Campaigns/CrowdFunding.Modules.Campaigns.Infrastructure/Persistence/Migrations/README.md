# Migrations

## Purpose
Contains the EF Core migration history for the Campaigns module. These files are generated snapshots of schema evolution.

## Files
- `20260402165758_InitialCampaignsSchema.Designer.cs`: Generated model metadata for the `InitialCampaignsSchema` migration.
- `20260402165758_InitialCampaignsSchema.cs`: Migration that applies: initial campaigns schema.
- `20260408044457_AddOutboxMessages.Designer.cs`: Generated model metadata for the `AddOutboxMessages` migration.
- `20260408044457_AddOutboxMessages.cs`: Migration that applies: add outbox messages.
- `20260408050504_RenameCampaignOutboxTable.Designer.cs`: Generated model metadata for the `RenameCampaignOutboxTable` migration.
- `20260408050504_RenameCampaignOutboxTable.cs`: Migration that applies: rename campaign outbox table.
- `20260908183629_AddCampaignConcurrencyAndLedger.Designer.cs`: Generated model metadata for the `AddCampaignConcurrencyAndLedger` migration.
- `20260908183629_AddCampaignConcurrencyAndLedger.cs`: Migration that applies: add campaign concurrency and ledger.
- `20260908191506_UpgradeOutboxAndAddDeadLetter.Designer.cs`: Generated model metadata for the `UpgradeOutboxAndAddDeadLetter` migration.
- `20260908191506_UpgradeOutboxAndAddDeadLetter.cs`: Migration that applies: upgrade outbox and add dead letter.
- `20260909001040_AddOutboxHeaders.Designer.cs`: Generated model metadata for the `AddOutboxHeaders` migration.
- `20260909001040_AddOutboxHeaders.cs`: Migration that applies: add outbox headers.
- `20260909032436_AddRewardTiers.Designer.cs`: Generated model metadata for the `AddRewardTiers` migration.
- `20260909032436_AddRewardTiers.cs`: Migration that applies: add reward tiers.
- `20260909154404_AddRewardTierReservations.Designer.cs`: Generated model metadata for the `AddRewardTierReservations` migration.
- `20260909154404_AddRewardTierReservations.cs`: Migration that applies: add reward tier reservations.
- `20260928174424_AppendOnlyCampaignLedger.Designer.cs`: Generated model metadata for the `AppendOnlyCampaignLedger` migration.
- `20260928174424_AppendOnlyCampaignLedger.cs`: Migration that applies: append only campaign ledger.
- `CampaignsDbContextModelSnapshot.cs`: EF Core model snapshot of the current `CampaignsDbContext` schema, used to compute the next migration.

## Child Folders
- None.

## Notes
Avoid hand-editing migration designer files unless you are intentionally repairing generated output.
