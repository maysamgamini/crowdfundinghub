# CrowdFunding Integration Tests

## Purpose
Database-backed integration test suite executing against containerized PostgreSQL instances (`Testcontainers`), validating high-concurrency race conditions, advisory locking, optimistic concurrency tokens, and outbox event persistence.

## Test Suites & Fixtures
- `CampaignContributionConcurrencyTests.cs`:
  - `ConcurrentPledges_ShouldNotLoseUpdates`: Fires concurrent pledge confirmations in parallel against a live campaign to verify that total raised balance and contribution count never suffer lost updates.
  - `RedeliveredContribution_ShouldBeAppliedOnlyOnce`: Simulates at-least-once outbox message redelivery, verifying that duplicate event processing is discarded idempotently via the contribution ledger.
- `ContributionConcurrencyTests.cs`:
  - `ConfirmAndFail_ShouldNotBothSucceed_WhenRacingOnTheSamePendingContribution`: Fires simultaneous confirmation and failure commands against the same pending contribution to prove PostgreSQL `xmin` concurrency tokens and transaction advisory locks prevent conflicting terminal state transitions.
- `CampaignsPostgresFixture.cs`:
  - Manages a lifecycle-bound `PostgreSqlContainer` running PostgreSQL 16.
  - Automatically runs `CampaignsDbContext` EF Core migrations upon test startup.
  - Provides isolated `CampaignsDbContext` factory instances for test assertions.
- `ContributionsPostgresFixture.cs`:
  - Manages a dedicated `PostgreSqlContainer` running PostgreSQL 16.
  - Automatically runs `ContributionsDbContext` EF Core migrations upon test startup.
  - Provides isolated `ContributionsDbContext` factory instances for test assertions.

## Prerequisites
Integration tests require a running Docker daemon on the host machine to spin up `testcontainers/postgresql`.
