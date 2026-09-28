# Abstractions

## Purpose
Contains the ports (interfaces) the Contributions application layer depends on; Infrastructure implements them.

## Files
- No direct files live in this folder; it primarily organizes child folders.

## Child Folders
- `Inbox`: `IContributionsIdempotentConsumer.cs`, the module's `IIdempotentConsumer`, backed by `contributions_processed_events` so the processed-event record commits in the same transaction as the state change it guards.
- `Persistence`: Defines persistence ports consumed by the Contributions application layer so handlers stay independent from EF Core details.
- `Services`: Defines service contracts that the Contributions application layer relies on for time, reads, or externalized behavior.
- `Transactions`: Defines transaction-boundary abstractions for commands in the Contributions module.
