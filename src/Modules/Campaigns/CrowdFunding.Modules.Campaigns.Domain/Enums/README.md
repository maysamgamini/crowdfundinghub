# Enums

## Purpose
Contains enum values that describe state transitions and classifications in the Campaigns module.

## Files
- `CampaignLedgerEntryKind.cs`: `Pledge` (positive), `Refund` (negative), `OpeningBalance`; a database CHECK ties each kind to the sign of its amount.
- `CampaignStatus.cs`: `Draft`, `Published`, `Successful`, `Failed`, `Cancelled`.

## Child Folders
- None.
