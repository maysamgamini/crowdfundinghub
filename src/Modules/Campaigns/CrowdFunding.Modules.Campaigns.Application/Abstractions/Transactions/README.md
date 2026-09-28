# Transactions

## Purpose
Defines transaction-boundary abstractions for commands in the Campaigns module.

## Files
- `ICampaignTransactionExecutor.cs`: Transaction boundary port for Campaigns writes; the implementation also collects cache keys to evict after commit (`EnqueueCacheInvalidation`).

## Child Folders
- None.
