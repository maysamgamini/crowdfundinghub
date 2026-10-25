# Pledges: Application Layer

## Purpose
Orchestrates use cases and business workflows for the Pledges module, including pledge creation, payment confirmation/failure, cancellation, and CloudEvent usage metering.

## Subdirectories
- `Abstractions`: Persistence repository abstractions (`IPledgeRepository`), transaction executor contracts (`IPledgesTransactionExecutor`), and time provider interfaces (`IPledgesDateTimeProvider`).
- `DependencyInjection`: Extension methods (`AddPledgesApplication`) registering command and query handlers.
- `Features`:
  - `Pledges/Commands`: Handlers for `MakePledge`, `ConfirmPayment`, `FailPayment`, and `CancelPledge`.
  - `Pledges/Queries`: Handlers for `GetPledgeById`.
  - `Metering`: Handlers like `PledgeConfirmedMeteringEventHandler` translating confirmed pledges into OpenMeter CloudEvents.
