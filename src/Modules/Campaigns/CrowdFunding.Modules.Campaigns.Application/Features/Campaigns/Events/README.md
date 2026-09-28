# Events

## Purpose
Contains application event handlers that react to cross-module events relevant to the Campaigns module.

## Files
- `ContributionPaymentConfirmedApplicationEventHandler.cs`: Reacts to a confirmed payment from Contributions by dispatching `AddContributionToCampaignCommand`.
- `ContributionRefundedLedgerHandler.cs`: Reacts to a refunded contribution by dispatching `RecordContributionRefundCommand`; idempotent through the ledger's unique (ContributionId, Kind) key.

## Child Folders
- None.
