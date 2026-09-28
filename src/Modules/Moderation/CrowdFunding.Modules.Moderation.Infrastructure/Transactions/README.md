# Transactions

## Purpose
Contains transaction executor implementations that coordinate persistence and event publication for the Moderation module.

## Files
- `ModerationTransactionExecutor.cs`: Implements `IModerationTransactionExecutor`: runs work in a transaction and harvests domain events into the outbox.

## Child Folders
- None.
