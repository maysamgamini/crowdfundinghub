# Contributions Module

## Purpose
The Contributions module manages the financial lifecycle of backer pledges, payment verification, transaction settlement, and revenue usage metering.

## Capabilities & Workflows
- **Pledge Creation**: Backers create pledges for active campaigns (`MakeContributionCommand`). Invariants verify campaign funding headroom and active status.
- **Payment Processing**:
  - `ConfirmPaymentCommand`: Marks the contribution as `Confirmed`, records payment reference, and raises `ContributionPaymentConfirmedApplicationEvent`.
  - `FailPaymentCommand`: Transitions the contribution to `Failed` upon payment provider rejection.
  - `CancelContributionCommand`: Cancels a pending pledge prior to settlement.
- **Optimistic Concurrency & Advisory Locks**: Prevents double-spend and race conditions using aggregate concurrency tokens (`xmin`) and PostgreSQL transaction-level advisory locks.
- **Monetization & Metering**: Confirmed pledges trigger `PledgeConfirmedMeteringEventHandler` which calculates platform fees and emits normalized CloudEvents to OpenMeter.

## Projects & Layers
- `CrowdFunding.Modules.Contributions.Domain`: Contains `Contribution` aggregate root, `ContributionStatus` enum, and domain events.
- `CrowdFunding.Modules.Contributions.Application`: Orchestrates contribution commands, query handlers, and event handlers.
- `CrowdFunding.Modules.Contributions.Infrastructure`: EF Core `ContributionsDbContext`, persistence configurations, repositories, and outbox tables.
- `CrowdFunding.Modules.Contributions.Contracts`: Cross-module contracts and events (`ContributionPaymentConfirmedApplicationEvent`).
