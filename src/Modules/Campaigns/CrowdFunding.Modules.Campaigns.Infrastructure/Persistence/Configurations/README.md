# Configurations

## Purpose
Contains EF Core entity configuration classes for the Campaigns module.

## Files
- `CampaignConfiguration.cs`: Maps `Campaign` to the `campaigns` table (with the `xmin` concurrency token).
- `CampaignLedgerEntryConfiguration.cs`: Maps the append-only `campaign_ledger_entries` table: unique (ContributionId, Kind) key and the kind/sign CHECK constraint.
- `RewardTierConfiguration.cs`: Maps `RewardTier`.
- `RewardTierReservationConfiguration.cs`: Maps `RewardTierReservation`.

## Child Folders
- None.
