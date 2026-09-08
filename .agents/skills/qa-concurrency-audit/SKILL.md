---
name: qa-concurrency-audit
description: >-
  Use this skill to audit, stress-test, and code-review concurrency hazards, race conditions,
  lost updates, TOCTOU vulnerabilities, distributed/advisory locking, and ledger idempotency.
---

# Concurrency, Race Conditions & Financial Consistency QA Runbook

## Purpose
This skill guides the QA engineer/agent in identifying and reproducing multi-threaded, multi-instance race conditions and financial anomalies across databases and application handlers.

## Concurrency Audit Checklist

### 1. Concurrency Tokens & Lost Updates
- **Entity Concurrency Tracking**:
  - Verify every entity with mutating counters/balances (e.g. `Campaign.RaisedAmount`, `Contribution.Status`) is protected by EF Core optimistic concurrency tokens (e.g. PostgreSQL `xmin` xid column).
  - Verify that `DbUpdateConcurrencyException` is caught, properly mapped or retried with jittered exponential backoff.
  - Check whether concurrent status updates (e.g. concurrent `ConfirmPayment` vs `FailPayment` on `Contribution`) are protected.

### 2. Distributed & Advisory Locking
- **Advisory Lock Key Quality**:
  - Check how advisory lock keys are derived.
  - **CRITICAL**: Never use 32-bit `Guid.GetHashCode()` cast to `long`. In 64-bit systems, `GetHashCode()` has frequent hash collisions (birthday paradox across ~70k entries) and can be process-randomized, causing false locking contentions across unrelated aggregates. Use deterministic 64-bit byte hashing (e.g. MurmurHash3 or `BitConverter.ToInt64(guid.ToByteArray(), 0) ^ BitConverter.ToInt64(guid.ToByteArray(), 8)`).
- **Lock Acquisition Ordering**:
  - Verify that entity retrieval occurs *inside* the transaction after acquiring `pg_advisory_xact_lock`, never before.
  - Verify that ALL competing mutation operations acquire the SAME lock key (e.g., if `AddContributionToCampaign` acquires a lock on `CampaignId`, `CancelCampaign` and `PublishCampaign` must also acquire that lock).

### 3. TOCTOU (Time-of-Check to Time-of-Use) Vulnerabilities
- Check for state checks performed outside transaction boundaries.
  - Example: Querying campaign availability or moderation review status before opening the transaction, leaving a race window for another process to cancel or reject the entity before the modification commits.
- Verify read re-validation inside the atomic transaction block.

### 4. Idempotency & Ledger Uniqueness
- Verify that every at-least-once message delivery or payment webhook has a durable uniqueness constraint (e.g., `campaign_contributions_ledger` unique index on `(campaign_id, contribution_id)`).
- Verify that redelivery of identical messages results in safe no-ops without double-crediting balances or emitting duplicate domain events.

## Issue Reporting
When a concurrency issue is found:
1. Log a ticket in `qa-tickets/` named `TICKET-XXX-<SLUG>.md`.
2. Document the interleaved sequence of operations between Thread A and Thread B.
3. Include repro steps or concurrency test scenarios.
