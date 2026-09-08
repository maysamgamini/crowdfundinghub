# Contributions: Application Layer

## Purpose
Orchestrates use cases and business workflows for the Contributions module, including contribution creation, payment confirmation/failure, cancellation, and CloudEvent usage metering.

## Subdirectories
- `Abstractions`: Persistence repository abstractions (`IContributionRepository`), transaction executor contracts (`IContributionsTransactionExecutor`), and time provider interfaces (`IContributionsDateTimeProvider`).
- `DependencyInjection`: Extension methods (`AddContributionsApplication`) registering command and query handlers.
- `Features`:
  - `Contributions/Commands`: Handlers for `MakeContribution`, `ConfirmPayment`, `FailPayment`, and `CancelContribution`.
  - `Contributions/Queries`: Handlers for `GetContributionById`.
  - `Metering`: Handlers like `PledgeConfirmedMeteringEventHandler` translating confirmed pledges into OpenMeter CloudEvents.
