# Persistence

## Purpose
Contains shared persistence helpers such as outbox storage and EF Core model configuration extensions.

## Files
- `DeadLetterEvent.cs`: Durable record of an outbox message that could not be processed (unknown event type or version, or retries exhausted), moved out of the active outbox table.
- `DomainEventAccessor.cs`: Reads and clears uncommitted domain events from the aggregates tracked by a `DbContext`.
- `ModelBuilderExtensions.cs`: `ConfigureOutbox`, `ConfigureDeadLetter`, and `ConfigureInbox` map the outbox, dead-letter, and processed-event tables uniformly in every module `DbContext`.
- `OutboxClaimQuery.cs`: `ClaimPendingBatchAsync` claims a batch of due rows with `FOR UPDATE SKIP LOCKED`, so concurrent processors take disjoint batches.
- `OutboxMessage.cs`: Persistent outbox record used to safely publish application events after database commits (payload, type, version, attempts, lease, next attempt time).
- `OutboxMessageStatus.cs`: Row lifecycle: `Pending` -> `Processing` -> deleted on success, back to `Pending` for a retry, or `DeadLetter`.

## Child Folders
- None.
