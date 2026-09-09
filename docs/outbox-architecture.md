# Outbox architecture: SKIP LOCKED vs. Debezium CDC

This document records the decision behind the outbox implementation in
`ModuleOutboxProcessor<TDbContext>` / `OutboxMessage`, and — just as importantly — the point at
which that decision should be revisited. Keep it up to date if the throughput assumptions below
change.

> **Update (TICKET-024/025/030):** the single, centralized `OutboxProcessorBackgroundService`
> that used to live in `CrowdFunding.API` and knew about all three module DbContexts by name has
> been replaced by `ModuleOutboxProcessor<TDbContext>` (`BuildingBlocks.Infrastructure.Outbox`),
> an abstract `BackgroundService` each module subclasses and registers in its own Infrastructure
> DI extension (`CampaignsOutboxBackgroundService`, `ContributionsOutboxBackgroundService`,
> `ModerationOutboxBackgroundService`). The API host no longer references any module's DbContext
> or outbox table name — three independent workers run on independent `PeriodicTimer`s inside the
> same process, so a slow or failing module's outbox can no longer starve the others, and
> extracting a module into its own service moves its outbox engine with it unchanged. Each
> processor publishes through `IMessageBus` (`InProcessMessageBus` by default; `RabbitMqMessageBus`
> when `Messaging:Provider = "RabbitMQ"`) instead of calling `IEventPublisher` directly, and
> restores the W3C `traceparent` captured in `OutboxMessage.Headers` at write time so the
> dispatch span stays a child of the originating HTTP request in a distributed trace. Everything
> below about the SKIP LOCKED claim query itself is unchanged — only who owns the polling loop.

## What problem the outbox solves

In distributed or event-driven systems, updating a database and publishing an event to a message
broker at the same time creates a "dual-write" dilemma: if the process crashes or the network
fails after the database commit but before the publish, the two systems fall out of sync forever.
The transactional outbox pattern solves this by writing the event payload to an outbox table
inside the *exact same database transaction* as the business-data change. If the transaction
commits, both the state change and the intent-to-publish are durable together; if it rolls back,
neither happened. A separate process then reads the outbox table and does the actual publish,
retrying independently of the original transaction.

Every module's `TransactionExecutor` (`CampaignTransactionExecutor`, `ContributionTransactionExecutor`,
`ModerationTransactionExecutor`) already does the "write" half of this correctly: domain events
raised during `action()` are mapped to `OutboxMessage` rows and saved in the same transaction as
the aggregate change, before commit. This document is about the "read" half — how a background
process claims and publishes those rows.

## The two options

### Option A — PostgreSQL-native, `FOR UPDATE SKIP LOCKED` (chosen)

The outbox table is polled directly by one or more instances of `OutboxProcessorBackgroundService`.
Naively, `SELECT ... WHERE status = 'Pending'` run by two replicas at once returns the *same* rows
to both, and both then publish and both then try to mark the rows processed — duplicate delivery.
PostgreSQL's `FOR UPDATE SKIP LOCKED` clause fixes this natively: a `SELECT ... FOR UPDATE SKIP
LOCKED` (here, structured as an atomic `UPDATE ... FROM (SELECT ... FOR UPDATE SKIP LOCKED) ...
RETURNING`) locks the rows it selects and instantly skips any row already locked by a concurrent
claimant, instead of blocking on it or returning it twice. Multiple worker instances can therefore
poll the same table concurrently and each walks away with a disjoint batch — no external
coordination needed.

**How it works here:**
1. Write: business transaction inserts an `OutboxMessage` row with `Status = Pending` (see
   `TransactionExecutor.ExecuteInternalAsync`).
2. Claim: `OutboxProcessorBackgroundService` runs a `WITH claimable AS (SELECT ... WHERE status =
   'Pending' AND scheduled_at_utc <= now() ORDER BY scheduled_at_utc, id FOR UPDATE SKIP LOCKED
   LIMIT @batchSize) UPDATE ... SET status = 'Processing' ... RETURNING *` — the claim and the
   status flip happen as one atomic statement, so there's no window where two workers could both
   see a row as claimable.
3. Process: each claimed message is deserialized (via `EventTypeRegistry`, not
   `Type.GetType(AssemblyQualifiedName)`) and published independently — one message throwing
   never stops the rest of the batch from being attempted (no `break`).
4. Resolve: on success the row becomes `Processed`; on failure it either goes back to `Pending`
   with a backed-off `ScheduledAtUtc` (if under the attempt limit) or to the terminal `DeadLetter`
   status (if not, or if the event type/payload couldn't be resolved at all) — see
   `DeadLetterEvent`.

**Advantages:**
- **Zero extra infrastructure.** No Kafka/Redpanda cluster, no Kafka Connect, no schema registry
  to run, monitor, and keep available. Everything lives in the database this project already
  depends on.
- **Zero race conditions across replicas.** `SKIP LOCKED` is a native row-locking primitive —
  correctness doesn't depend on anything this codebase has to get right about distributed
  coordination.
- **Guaranteed consistency.** If the originating business transaction rolls back, the outbox row
  was never committed either — nothing to reconcile.

**Scalability limits (why this isn't the answer for every scale):**
- **Table bloat / vacuuming pressure.** Every `UPDATE` on an outbox row (claim, then
  processed/dead-letter) creates a new MVCC tuple version; a busy table needs `VACUUM` to keep up
  or dead tuples accumulate and query planning degrades.
- **Index contention.** The claim query's `WHERE status = 'Pending' ORDER BY scheduled_at_utc`
  hits the same partial index from every worker on every poll; at high enough frequency this
  becomes a hot spot even though `SKIP LOCKED` prevents workers from blocking each other on the
  *rows*.
- **Polling overhead.** Workers hit the database on a fixed interval whether or not there's
  anything to do, consuming connection-pool slots and CPU even at zero load.

Net effect: this approach scales well into the **hundreds to low thousands of events/second**
range for a table like this, which comfortably covers this project's current and near-term
traffic. It is a deliberate trade of some ceiling headroom for a much smaller, easier-to-operate
system today.

### Option B — Debezium CDC (not chosen, revisit if traffic grows)

Instead of polling, enable PostgreSQL logical replication (`wal_level=logical`) and point a
Debezium connector at the outbox table. Debezium tails the write-ahead log directly and streams
committed inserts to Kafka/Redpanda — the database is never polled, and multiple independent
consumer groups can subscribe to the same stream without touching Postgres at all.

| Dimension | SKIP LOCKED (this repo) | Debezium CDC |
| :--- | :--- | :--- |
| Infrastructure complexity | None beyond Postgres | Kafka/Redpanda + Kafka Connect + schema registry |
| Throughput ceiling | Moderate (DB CPU / connection pool / index-write bound) | High (decoupled from the operational database) |
| Consumer flexibility | One logical worker pool consuming the table | Many independent consumer groups can replay the same stream |
| Database impact | Write amplification from claim/resolve updates, plus active polling | Low — sequential WAL reads, no polling |
| Operational cost | Low (nothing new to run) | Higher (a distributed streaming platform to operate) |

**When to switch:** move to Debezium CDC when event volume approaches the thousands-per-second
range sustained, when multiple independent downstream systems need to consume the same event
stream without going through this API, or when database write amplification/index contention from
the outbox table is measurably affecting the primary workload. None of those are true today, so
Option A stands — but if that changes, this is the section to update first.
