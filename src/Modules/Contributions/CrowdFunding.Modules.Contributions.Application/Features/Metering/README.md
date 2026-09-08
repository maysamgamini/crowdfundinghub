# Contributions Usage Metering

## Purpose
Translates confirmed contribution application events into normalized CloudEvents for ingestion into the OpenMeter monetization and usage tracking system.

## Files
- `PledgeConfirmedMeteringEventHandler.cs`: Implements `IEventHandler<ContributionPaymentConfirmedApplicationEvent>`. Calculates platform success fees, payment processing fee deductions (basis points and fixed cents), and publishes idempotent `crowdfunding.pledge.confirmed` events to `IUsageMeteringClient`.

## Idempotency & Resiliency
- **Deterministic ID**: The CloudEvent ID is deterministically computed as `pledge_evt_{ContributionId}`, guaranteeing that replayed outbox messages are treated idempotently by the metering platform.
- **Fail-Safe Processing**: Metering ingestion failures are swallowed within the HTTP client so billing reporting delays or outages never fail or roll back the core financial contribution pipeline.
