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
  - Check lock timeouts and retry intervals to avoid orphaned in-progress messages if a worker node crashes mid-batch.

### 2. Poison Pill Handling & Dead-Letter Queues (DLQ)
- **Head-of-Line Blocking**:
  - Verify that a single failing message does NOT block the rest of the batch (no `break;` inside loops).
  - Verify that unresolvable event types or malformed payloads are immediately moved to a `dead_letter_events` table instead of retrying infinitely.
  - Verify retry limits (e.g. `MaxAttempts = 5`) with exponential backoff and jitter.

### 3. Serialization Stability & Schema Evolution
- **Decoupled Event Envelopes**:
  - Ensure events are deserialized using a discriminator-based `EventTypeRegistry` rather than fragile CLR `Type.GetType(AssemblyQualifiedName)`.
  - Check event versioning attributes (`[EventVersion]`) for forward and backward schema tolerance.
- **Unmapped Event Detection**:
  - Verify that transaction executors throw or log critical errors if an aggregate emits a domain event that has no corresponding application event mapping (preventing silent event drops).

### 4. Downstream Handler Idempotency
- Verify that every event handler invoked by the outbox processor is completely idempotent.
- Check what happens if `dbContext.SaveChangesAsync()` fails after dispatching events to handlers.

## Issue Reporting
When a resilience or outbox defect is discovered:
1. Log a ticket in `qa-tickets/` named `TICKET-XXX-<SLUG>.md`.
2. Detail failure scenarios, data loss risks, or queue starvation conditions.
3. Recommend resilient architectural fixes.
