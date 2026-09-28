---
name: qa-resilience-outbox
description: >-
  Use this skill to audit asynchronous messaging, transactional outbox resilience,
  poison message handling, serialization safety, dead-letter recovery, and background worker reliability.
---

# Async Messaging & Outbox Resilience QA Runbook

## Purpose
This skill guides the QA engineer/agent in reviewing the transactional outbox pipeline, event publisher mechanics, background worker resilience, serialization stability, and failure recovery.

## Resilience Audit Checklist

### 1. Transactional Outbox Pipeline
- **Outbox Persistence Boundary**:
  - Verify that domain event extraction and outbox table persistence happen within the exact same database transaction as the aggregate state change.
  - Verify that if the transaction rolls back, no outbox messages are saved.
- **Batch Claiming & Concurrency**:
  - Verify row-level lock claiming using PostgreSQL `FOR UPDATE SKIP LOCKED` so multiple instances do not process duplicate batches.
  - Check lease handling: a row left in `Processing` by a crashed worker must be reclaimed once its lease (`LockedUntilUtc`) expires, and the reclaim must count as an attempt.
  - Check the wake-up path: `OutboxCommitSignalInterceptor` notifies `OutboxSignal` only after commit (never on rollback); other replicas rely on the 5 s fallback sweep.

### 2. Poison Pill Handling & Dead-Letter Queues (DLQ)
- **Head-of-Line Blocking**:
  - Verify that a single failing message does NOT block the rest of the batch (no `break;` inside loops).
  - Verify that unresolvable event types or malformed payloads are immediately moved to the module's dead-letter table instead of retrying infinitely.
  - Verify the actual retry policy of `ModuleOutboxProcessor`: a fixed 10 s delay (`RetryDelay`) between attempts and `MaxAttempts = 5` before dead-lettering. There is no exponential back-off or jitter; assess whether that is adequate rather than assuming it exists.

### 3. Serialization Stability & Schema Evolution
- **Decoupled Event Envelopes**:
  - Ensure events are deserialized using a discriminator-based `EventTypeRegistry` rather than fragile CLR `Type.GetType(AssemblyQualifiedName)`.
  - Check event versioning attributes (`[EventVersion]`) for forward and backward schema tolerance.
- **Unmapped Event Detection**:
  - Verify that transaction executors throw or log critical errors if an aggregate emits a domain event that has no corresponding application event mapping (preventing silent event drops).

### 4. Downstream Handler Idempotency
- Verify that every event handler invoked by the outbox processor is idempotent: effects that must happen once should run through the inbox (`IIdempotentConsumer.ExecuteOnceAsync`, claimed with `INSERT ... ON CONFLICT DO NOTHING` in the effect's transaction), or rely on a unique key (e.g. the campaign ledger's `(ContributionId, Kind)`).
- Check what happens if `dbContext.SaveChangesAsync()` fails after dispatching events to handlers.

## Issue Reporting
When a resilience or outbox defect is discovered:
1. Record it in your final report to the caller (file path and line, severity, violated rule). Do not create ticket files in the repository.
2. Detail failure scenarios, data loss risks, or queue starvation conditions.
3. Recommend resilient architectural fixes.
