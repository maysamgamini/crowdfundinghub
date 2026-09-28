# Outbox and Inbox Architecture

This document describes how an application event travels from a business transaction to its consumers: write, claim, publish, then delete, retry or dead-letter. It also covers the wake-up signal, lease recovery, event identity, the inbox on the consumer side, tracing and test support, and when the design should be replaced by change data capture (CDC).

The code lives in [`BuildingBlocks.Infrastructure/Outbox`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Outbox/ModuleOutboxProcessor.cs), [`Persistence`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Persistence/OutboxClaimQuery.cs) and [`Inbox`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Inbox/EfIdempotentConsumer.cs).

## Why an outbox

Committing a database change and publishing a message are two writes to two systems. If the process dies between them, the systems disagree for good. The transactional outbox writes the event as a row in the same transaction as the business change: both commit or neither does. A background worker publishes the row afterwards and retries independently of the original request.

## Who owns what

Three modules produce events: Campaigns, Contributions and Moderation. Each owns:

| Piece | Campaigns | Contributions | Moderation |
| :--- | :--- | :--- | :--- |
| Outbox table | `campaigns_outbox_messages` | `contributions_outbox_messages` | `moderation_outbox_messages` |
| Dead-letter table | `campaigns_dead_letter_events` | `contributions_dead_letter_events` | `moderation_dead_letter_events` |
| Worker | `CampaignsOutboxBackgroundService` | `ContributionsOutboxBackgroundService` | `ModerationOutboxBackgroundService` |
| Signal | `OutboxSignal<CampaignsDbContext>` | `OutboxSignal<ContributionsDbContext>` | `OutboxSignal<ModerationDbContext>` |

Each worker derives from `ModuleOutboxProcessor<TDbContext>` and is registered by its module's Infrastructure DI extension. The API host knows no outbox table or DbContext. The three workers run independently, so a slow or failing module cannot starve the others, and a module that is extracted later takes its outbox engine with it.

## 1. Write

Each module's transaction executor (`CampaignTransactionExecutor`, `ContributionTransactionExecutor`, `ModerationTransactionExecutor`) runs the handler's action inside a transaction. Before commit it collects the domain events raised by tracked aggregates, maps each to a public application event from the module's Contracts project, and adds one `OutboxMessage` per event. A domain event without a mapping throws, so a new event cannot be dropped silently.

[`OutboxMessage.Create`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Persistence/OutboxMessage.cs) stores:

- `Id`: the application event's own `EventId` (see [Event identity](#event-identity)).
- `EventType` and `Version`: a stable discriminator from `EventTypeRegistry`, not a CLR assembly-qualified name, so renaming or moving a class does not orphan stored rows.
- `Payload`: the event as web-style (camelCase) JSON.
- `Headers`: the W3C `traceparent` and `tracestate` of `Activity.Current` at write time.
- `Status = Pending`, `ScheduledAtUtc = OccurredOnUtc`, `Attempts = 0`.

## 2. Wake-up signal

Polling alone puts a floor under latency: an event written just after a poll waits a full interval. Instead, each worker waits on an [`OutboxSignal<TDbContext>`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Outbox/OutboxSignal.cs), a bounded channel of capacity one with `DropWrite`. Any number of notifications collapse into one pending wake-up, and a notification sent while the worker is busy is not lost: the worker finds it on its next wait.

[`OutboxCommitSignalInterceptor<TDbContext>`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Outbox/OutboxCommitSignalInterceptor.cs) is an EF Core `ISaveChangesInterceptor` and `IDbTransactionInterceptor` added to the module's DbContext (`AddOutboxSignal<TDbContext>()` plus `AddOutboxSignalInterceptor<TDbContext>()` in [`OutboxSignalRegistrationExtensions`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Outbox/OutboxSignalRegistrationExtensions.cs)):

- `SavingChanges`: if the context has `Added` outbox rows, remember that context.
- `SavedChanges`: if there is no explicit transaction, the rows are already committed; notify.
- `TransactionCommitted`: notify if the context was remembered.
- `TransactionRolledBack`: forget the context; nothing becomes visible, so there is nothing to wake for.

The worker is therefore woken only after the rows are visible to its claim query. `WaitAsync(pollInterval)` also returns when the interval (5 seconds) elapses, which makes the timer a fallback sweep. The sweep is still needed for rows written by another process (the signal is in-process), rows rescheduled for a retry, and expired leases.

## 3. Claim

[`OutboxClaimQuery.ClaimPendingBatchAsync`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Persistence/OutboxClaimQuery.cs) claims up to 20 rows in one statement:

```sql
WITH claimable AS (
    SELECT "Id" FROM <module>_outbox_messages
    WHERE ("Status" = Pending    AND "ScheduledAtUtc" <= now())
       OR ("Status" = Processing AND "LockedUntilUtc" <  now())
    ORDER BY "ScheduledAtUtc", "Id"
    FOR UPDATE SKIP LOCKED
    LIMIT 20
)
UPDATE <module>_outbox_messages m
SET "Attempts" = m."Attempts" + CASE WHEN m."Status" = Processing THEN 1 ELSE 0 END,
    "Status" = Processing,
    "LockedBy" = <machine:pid:DbContext>,
    "LockedUntilUtc" = now() + 30 seconds
FROM claimable WHERE m."Id" = claimable."Id"
RETURNING m.*;
```

- `FOR UPDATE SKIP LOCKED` lets several workers (other replicas, or a test drain) claim concurrently: each gets a disjoint batch and nobody blocks.
- Selecting and marking happen in one statement, so there is no gap between "found" and "claimed".
- The claim commits on its own, before publishing. The row is now leased to this worker for 30 seconds.
- A partial index `ix_<table>_pending` on `(ScheduledAtUtc, Id) WHERE Status = Pending` keeps the common path cheap.

### Lease recovery

If a worker crashes after claiming, its rows stay `Processing`. Once `LockedUntilUtc` has passed, the same claim query picks them up again and counts the abandoned pass as an attempt. A message that kills the process every time therefore still reaches the attempt limit and the dead-letter table instead of looping forever. Reclaiming means a message can be published twice; consumers handle that (see [Inbox](#inbox-the-consumer-side)).

## 4. Publish and resolve

For each claimed row, `ModuleOutboxProcessor` runs:

1. **Resolve.** `TryResolve` looks up `(EventType, Version)` in `EventTypeRegistry` and deserializes the payload. If either fails, the row goes straight to the dead-letter table with the reason: retrying cannot fix it. The rest of the batch continues.
2. **Publish.** Start a tracing activity (see [Tracing](#tracing-across-the-outbox)) and call `IMessageBus.PublishAsync`.
3. **Success:** delete the row. The outbox table holds only pending and in-flight work, which keeps the table and its index small and avoids keeping dead tuples of processed rows.
4. **Failure:** `MarkFailed` increments `Attempts`, records the error, clears the lease and sets the row back to `Pending` with `ScheduledAtUtc = now + 10 s` (a fixed retry delay). At 5 attempts the row is copied to the dead-letter table with the last error and deleted from the outbox.

One `SaveChanges` at the end persists the deletes, retries and dead-letter rows of the batch. A failure of one message never stops the others.

The worker loop catches and logs any exception from a pass (for example, the database being down) and tries again after the fallback interval, so an outage does not stop the host. When a pass claims a full batch, the worker skips the wait and claims again at once, so a backlog drains at database speed rather than 20 rows per interval.

### What "publish" means today

`IMessageBus` has two implementations, selected by `Messaging:Provider`:

- `InProcessMessageBus` (default): calls `IEventPublisher`, which runs every `IEventHandler<T>` for the event in the same process, one after another. This is the working mode.
- `RabbitMqMessageBus`: wraps the event in a CloudEvents envelope and publishes it to a durable topic exchange, routed by event type. There is no RabbitMQ consumer in this codebase, so cross-process delivery is not implemented on this branch.

In-process handlers share one outbox row. If the second of three handlers throws, the whole message is retried and the first handler runs again. Every handler must therefore be idempotent, which is what the inbox, unique keys and upserts below provide.

## Event identity

Every application event derives from [`BaseApplicationEvent`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Application/Events/BaseApplicationEvent.cs), whose `EventId` is created once and serialized with the payload. `OutboxMessage.Create` uses that `EventId` as the row id, so the outbox row id, the deserialized event's `EventId`, the CloudEvents `id` (and RabbitMQ `MessageId`), and the consumers' processed-event keys all name the same message. Every redelivery carries the same id.

## Inbox: the consumer side

At-least-once delivery means consumers see duplicates: after a lease expiry, after a partial handler failure, or when an external provider retries a webhook. [`IIdempotentConsumer`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Application/Inbox/IIdempotentConsumer.cs) makes a side effect run once per `(eventId, consumerName)`:

```csharp
await idempotentConsumer.ExecuteOnceAsync(
    notification.EventId.ToString(),
    nameof(ContributionPaymentConfirmedActivityHandler),
    ct => EnqueueDeliveriesAsync(notification, ct),
    cancellationToken);
```

[`EfIdempotentConsumer<TDbContext>`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Inbox/EfIdempotentConsumer.cs) opens a transaction (or joins the current one), runs `INSERT ... ON CONFLICT DO NOTHING` on the module's processed-events table (primary key `(EventId, ConsumerName)`), and only runs the effect if the insert added a row. The claim and the effect's writes commit together: if the effect fails, the claim rolls back and a retry runs the effect again; if a concurrent delivery holds the claim, the second one waits on the key and then skips. Consumer names are separate keys, so two handlers of the same event do not block each other.

Current users:

| Consumer | Event id | Table |
| :--- | :--- | :--- |
| `ReconcilePaymentWebhookCommandHandler` (Contributions) | Stripe event id | `contributions_processed_events` |
| `ContributionPaymentConfirmedActivityHandler` (CampaignUpdates, pledge-confirmed webhook fan-out) | `EventId` | `campaign_updates_processed_events` |

Other consumers are idempotent by construction instead: the campaign ledger's unique `(ContributionId, Kind)` key (see [append-only-ledger.md](append-only-ledger.md)), and upserts into replicated read models such as Contributions' active-campaign cache. E-mail and metering side effects rely on the provider's idempotency key, not the inbox, so exactly-once is not guaranteed across a crash between the external call and the commit.

## Tracing across the outbox

Publishing happens seconds later on a background thread, where `Activity.Current` is empty. To keep one trace from request to consumer:

1. `OutboxMessage.Create` saves the current `traceparent`/`tracestate` in `Headers`.
2. The worker parses them and starts an `Outbox.Process <EventType>` activity of kind `Consumer` from the `CrowdFunding.Outbox` `ActivitySource` ([`CrowdFundingTelemetry`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Observability/CrowdFundingTelemetry.cs)), with the stored context as parent. Missing or malformed headers produce a new root activity rather than a failure.
3. Handler work (EF Core, Npgsql, HttpClient spans) nests under that activity, and any outbox rows those handlers write capture it as their parent in turn.

[`OpenTelemetryConfiguration`](../src/API/CrowdFunding.API/Observability/OpenTelemetryConfiguration.cs) registers the `CrowdFunding.Outbox` source with the ASP.NET Core, HttpClient, EF Core and Npgsql instrumentation and exports over OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. With `docker compose up`, the Aspire dashboard at http://localhost:18888 shows the HTTP request and its outbox dispatch in one trace.

## Test and diagnostic support

`ModuleOutboxProcessor` implements [`IOutboxDispatcher`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Outbox/IOutboxDispatcher.cs):

- `ProcessBatchAsync`: one claim-and-publish pass.
- `DrainAsync(timeout)`: runs passes until no row is due and none is `Processing` in another worker, or the timeout elapses. Rows rescheduled for a later retry are not waited for.

Because a commit wakes the background worker, that worker may already hold the rows a test just wrote. A single pass would then find nothing and return early; `DrainAsync` also waits for in-flight rows. The integration fixture's [`ProcessOutboxMessagesAsync`](../tests/IntegrationTests/CrowdFunding.IntegrationTests/CrowdFundingApiFactory.cs) finds every `IOutboxDispatcher` among the hosted services and drains each, so tests observe cross-module effects deterministically without knowing which modules exist.

## Polling outbox vs. CDC

The alternative to polling is change data capture: enable logical replication (`wal_level=logical`) and let Debezium stream committed outbox inserts to Kafka or Redpanda.

| Dimension | SKIP LOCKED outbox (this repo) | Debezium CDC |
| :--- | :--- | :--- |
| Infrastructure | PostgreSQL only | Kafka/Redpanda, Kafka Connect, usually a schema registry |
| Latency | Near-immediate for in-process commits (signal); up to 5 s for other replicas and retries | Sub-second after commit |
| Database writes per event | INSERT, claim UPDATE, DELETE (plus UPDATEs on retry) | INSERT only; rows purged in bulk |
| Idle load | One claim query per module every 5 s | None; WAL is read sequentially |
| Consumers | One worker pool per module | Many independent consumer groups; replay by offset |
| Failure mode | Lease expiry and retry; dead-letter table | A stalled connector makes PostgreSQL retain WAL until the disk fills |

Choose CDC when sustained event volume reaches thousands per second, when several independent systems need to consume and replay the same stream, or when outbox write amplification measurably affects the primary workload. None of these holds today. The outbox rows, `EventId` identity and inbox would carry over unchanged; only the claim-and-publish worker would be replaced by the connector.
