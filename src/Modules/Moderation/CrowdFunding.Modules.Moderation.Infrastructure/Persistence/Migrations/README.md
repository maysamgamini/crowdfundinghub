# Migrations

## Purpose
Contains the EF Core migration history for the Moderation module. These files are generated snapshots of schema evolution.

## Files
- `20260407035246_InitialModerationSchema.Designer.cs`: Generated model metadata for the `InitialModerationSchema` migration.
- `20260407035246_InitialModerationSchema.cs`: Migration that applies: initial moderation schema.
- `20260408044528_AddOutboxMessages.Designer.cs`: Generated model metadata for the `AddOutboxMessages` migration.
- `20260408044528_AddOutboxMessages.cs`: Migration that applies: add outbox messages.
- `20260908191535_UpgradeOutboxAndAddDeadLetter.Designer.cs`: Generated model metadata for the `UpgradeOutboxAndAddDeadLetter` migration.
- `20260908191535_UpgradeOutboxAndAddDeadLetter.cs`: Migration that applies: upgrade outbox and add dead letter.
- `20260909001056_AddOutboxHeaders.Designer.cs`: Generated model metadata for the `AddOutboxHeaders` migration.
- `20260909001056_AddOutboxHeaders.cs`: Migration that applies: add outbox headers.
- `20260909025308_AddCampaignReviewAutomatedAnalysisScores.Designer.cs`: Generated model metadata for the `AddCampaignReviewAutomatedAnalysisScores` migration.
- `20260909025308_AddCampaignReviewAutomatedAnalysisScores.cs`: Migration that applies: add campaign review automated analysis scores.
- `ModerationDbContextModelSnapshot.cs`: EF Core model snapshot of the current `ModerationDbContext` schema, used to compute the next migration.

## Child Folders
- None.

## Notes
Avoid hand-editing migration designer files unless you are intentionally repairing generated output.
