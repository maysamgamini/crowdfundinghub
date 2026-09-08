# API Background Workers

## Purpose
Hosts background `IHostedService` worker implementations responsible for executing out-of-band asynchronous processing, polling transactional outbox queues, and reliably delivering domain events to subscribers across module boundaries.

## Files
- `OutboxProcessorBackgroundService.cs`: Long-running `BackgroundService` that periodically polls module outbox tables (`campaigns_outbox`, `contributions_outbox`, `identity_outbox`, `moderation_outbox`). Fetches unprocessed messages in FIFO order, deserializes payloads via `IEventTypeRegistry`, publishes them through `IEventPublisher`, and marks processed records with UTC timestamps.

## Reliability & Outbox Architecture
- **At-Least-Once Delivery**: Events are written to the database in the exact same database transaction as the aggregate state mutation. The background processor guarantees that committed events will eventually be published.
- **Dead-Letter Queue (DLQ)**: Poison messages that fail repeatedly (or fail deserialization) are moved to module-specific dead-letter tables (`*_dead_letter`) to prevent queue starvation.
- **Graceful Shutdown**: Honors `CancellationToken` during process termination to ensure inflight event processing either completes cleanly or is rolled back safely for redelivery on restart.
