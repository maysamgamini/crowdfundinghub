# Transactions

## Purpose
Contains transaction executor implementations that coordinate persistence and event publication for the Contributions module.

## Files
- `ContributionTransactionExecutor.cs`: Implements `IContributionTransactionExecutor`: runs a write inside a transaction on `ContributionsDbContext`, so the aggregate changes and the outbox rows commit together.

## Child Folders
- None.
