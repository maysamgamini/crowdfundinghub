# Aggregates

## Purpose
Contains the aggregate roots that protect the core business rules for the Campaigns module.

## Files
- `Campaign.cs`: Campaign aggregate root: creation rules, lifecycle transitions (Draft -> Published -> Successful / Failed / Cancelled), and `RecordPledge` / `RecordRefund`, which produce ledger entries.
- `CampaignLedgerEntry.cs`: One immutable line of the append-only funding ledger (pledge, refund, or opening balance).
- `RewardTier.cs`: Limited-quantity reward perk; protects `ClaimedCount + ReservedCount <= TotalCapacity`.
- `RewardTierReservation.cs`: A time-limited hold on one reward-tier slot during checkout, later confirmed or released.

## Child Folders
- None.
