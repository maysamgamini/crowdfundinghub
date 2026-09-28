# Add Contribution To Campaign

## Purpose
Credits a confirmed contribution to its campaign by appending a pledge entry to the append-only ledger (the command and result live in `Campaigns.Contracts`).

## Files
- `AddContributionToCampaignCommandHandler.cs`: Takes the per-campaign advisory lock, lets `Campaign.RecordPledge` produce the entry, appends it through `ICampaignLedger`, and notifies connected clients. A cancelled or completed campaign credits nothing (`Credited = false`).

## Child Folders
- None.
