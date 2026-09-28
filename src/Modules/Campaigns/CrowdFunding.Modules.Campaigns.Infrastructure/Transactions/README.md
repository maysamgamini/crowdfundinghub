# Transactions

## Purpose
Contains transaction executor implementations that coordinate persistence and event publication for the Campaigns module.

## Files
- `CampaignTransactionExecutor.cs`: Implements `ICampaignTransactionExecutor`: runs work in a transaction, harvests domain events into the outbox at the outermost level only, and evicts enqueued cache keys after commit.

## Child Folders
- None.
