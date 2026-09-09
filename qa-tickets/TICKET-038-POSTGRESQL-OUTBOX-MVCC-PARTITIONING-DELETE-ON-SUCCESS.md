# QA Ticket: TICKET-038

**Title:** PostgreSQL Outbox MVCC Optimization: Table Partitioning & Immediate Delete-on-Success (Autovacuum Defense)  
**Severity:** 🟠 P1 (High - High-Throughput Database Bloat & Write Amplification)  
**QA Focus Area:** Database Engine Internals, PostgreSQL MVCC & Outbox Maintenance  
**Found By:** `qa-platform-shortcomings`  
**Status:** Fixed  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

In [`OutboxClaimQuery.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Persistence/OutboxClaimQuery.cs#L28-L45) and [`OutboxProcessorBackgroundService.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Background/OutboxProcessorBackgroundService.cs#L94), the outbox manages state transitions via mutable row updates:
- Step 1: `INSERT` row with `Status = 1 (Pending)`.
- Step 2: Background worker `UPDATE` row with `Status = 2 (Processing)`.
- Step 3: Completion `UPDATE` row with `Status = 3 (Processed)`.
- Retries: Transient errors execute additional `UPDATE` statements to increment `Attempts`.

### The PostgreSQL MVCC Write Amplification Trap
In PostgreSQL's Multi-Version Concurrency Control (MVCC) architecture, an `UPDATE` **does not overwrite disk blocks in place**. Instead:
1. It writes a brand new tuple (row version) to the page.
2. It sets the `xmax` of the old tuple to the current transaction ID, marking it as a dead tuple.
3. Every single event creates **3 to 5 dead tuples** in the table heap and associated indexes.
4. Processed rows are **never purged or deleted**, growing the table indefinitely.

Under sustained throughput (e.g. 500 events/sec), the database generates 1,500 dead tuples/sec. PostgreSQL `autovacuum` cannot keep pace, causing table pages to fragment, indexes to bloat, and `SELECT FOR UPDATE SKIP LOCKED` queries to evict active campaign and contribution data pages from PostgreSQL `shared_buffers`.

---

## 2. Blast Radius & Database Performance Impact

- **Autovacuum Starvation:** Outbox tables expand from megabytes to gigabytes within weeks, degrading query execution times across the entire database.
- **Index Fragmentation:** The B-tree index on `(Status, ScheduledAtUtc)` becomes severely bloated, increasing CPU cost per poll tick.

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Teach the **Database Engine Mechanics of Distributed System Patterns**. Too often, architects recommend the Transactional Outbox pattern without understanding the physical storage cost (PostgreSQL write amplification and dead tuple accumulation). A Principal Engineer must design patterns that survive production-scale database engine realities.

### Monolith First, Microservices Ready
The outcome of this project is a **Modular Monolith, NOT microservices**. Inside our monolith, all modules write outbox rows into their local schemas. By optimizing outbox lifecycle management today:
1. **Immediate Delete-on-Success:** Successfully processed outbox rows are deleted immediately in the claim loop (`DELETE FROM outbox WHERE "Id" = @id`), keeping the active table size bounded to only pending and in-flight messages (zero table bloat).
2. **Dead-Letter Archival:** Failed messages exceeding maximum retries are moved to an append-only `dead_letter_messages` table.
3. **Partitioning Readiness:** Outbox tables are structured with declarative date partitioning (`PARTITION BY RANGE (OccurredOnUtc)`).
When turning modules into microservices, the database persistence layer is **already hardened for enterprise-scale throughput**.

### What Breaks Tomorrow If Ignored Today?
If an architect deploys a naive outbox that accumulates processed messages forever, within months of high production traffic, the PostgreSQL database experiences severe disk expansion, query slowdowns, and eventual downtime due to autovacuum wraparound emergencies.

---

## 4. Affected Files & Modules

- [`src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Persistence/OutboxMessage.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Persistence/OutboxMessage.cs)
- [`src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Persistence/OutboxClaimQuery.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Persistence/OutboxClaimQuery.cs)
- All module outbox tables (`campaigns_outbox_messages`, `contributions_outbox_messages`, `moderation_outbox_messages`).

---

## 5. Implementation Specification & Greenfield Solution

### Strategy A: Immediate Delete-on-Success (Lean Outbox Table)
Modify the completion step in the outbox worker:
```csharp
// Instead of UPDATE SET Status = Processed:
await dbContext.Database.ExecuteSqlRawAsync(
    $"DELETE FROM {tableName} WHERE \"Id\" = @p0",
    message.Id,
    cancellationToken);
```
* **Result:** The outbox table contains only active, in-flight, or retrying messages. The table size stays virtually empty (< 100 rows) at all times, completely eliminating MVCC table bloat!

### Strategy B: Dead-Letter Archival for Diagnostics
If an outbox message reaches `Attempts >= MaxAttempts`:
```csharp
await dbContext.Database.ExecuteSqlRawAsync(
    $@"INSERT INTO dead_letter_messages (""Id"", ""EventType"", ""Payload"", ""Error"", ""FailedAtUtc"")
       SELECT ""Id"", ""EventType"", ""Payload"", @p1, now()
       FROM {tableName} WHERE ""Id"" = @p0;
       DELETE FROM {tableName} WHERE ""Id"" = @p0;",
    message.Id,
    lastException.ToString(),
    cancellationToken);
```

### Strategy C: Declarative Date Range Partitioning (Optional for Audit Retention)
If business regulations require keeping processed outbox history for 30 days:
```sql
CREATE TABLE campaigns.campaigns_outbox_messages (
    id UUID NOT NULL,
    occurred_on_utc TIMESTAMP WITH TIME ZONE NOT NULL,
    status INT NOT NULL,
    payload TEXT NOT NULL,
    PRIMARY KEY (id, occurred_on_utc)
) PARTITION BY RANGE (occurred_on_utc);

-- Daily partitions dropped via instant metadata operation:
DROP TABLE campaigns.campaigns_outbox_messages_2026_09_01;
```

---

## 6. Verification & Acceptance Criteria

1. **Zero Row Accumulation:** Running a load test producing 1,000 outbox messages results in an outbox table containing 0 rows once processing completes.
2. **Dead-Letter Isolation:** Forcing a message failure past retry limits removes the row from the active outbox and verifies its presence in `dead_letter_messages` with the full stack trace.
3. **PostgreSQL Autovacuum Metrics:** Database statistics (`pg_stat_user_tables`) confirm `n_dead_tup` remains minimal and does not trigger table vacuum lag.

---

## 7. Resolution

Implemented **Strategy A (delete-on-success)** and completed **Strategy B (dead-letter
archival)** in the one shared file every module's outbox already runs through —
[`ModuleOutboxProcessor.cs`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Outbox/ModuleOutboxProcessor.cs):

- **Before this ticket**: a successfully published message called `MarkProcessed(nowUtc)` —
  an `UPDATE` that left the row in the table forever with `Status = Processed`. A
  terminally-failed message already got *copied* into `DeadLetterEvent` (that half of Strategy B
  already existed), but the *original* outbox row was never deleted — it just sat there with
  `Status = DeadLetter`, permanently. Both paths accumulated rows without bound, exactly the
  MVCC dead-tuple trap this ticket describes.
- **Now**: on successful publish, the row is `Remove()`d instead of marked processed — no
  `UPDATE` at all, straight to `DELETE`. On dead-letter (both the "unresolvable event type" path
  and the "exceeded `MaxAttempts`" path), the row is still copied into `DeadLetterEvent` first,
  then also `Remove()`d from the active table. A row below `MaxAttempts` still goes back to
  `Pending` with a backed-off `ScheduledAtUtc` — it stays because it's still active work, not
  because anything is being retained after completion.
- **Strategy C (declarative partitioning)** was intentionally **not** implemented — the ticket
  itself marks it optional, and it would require restructuring the primary key to
  `(id, occurred_on_utc)` across every module's outbox table purely to support 30-day audit
  retention this codebase has no regulatory requirement for. With delete-on-success in place,
  the active table's steady-state size is bounded by in-flight message count (single digits to
  low hundreds under load), not by total lifetime message volume — partitioning an
  already-bounded table buys nothing here. If retention becomes a real requirement,
  `DeadLetterEvent` is already the append-only archive to partition instead.
- New tests: `tests/IntegrationTests/CrowdFunding.IntegrationTests/OutboxLeanTableE2ETests.cs` —
  (1) publishing a real campaign end-to-end and confirming zero `Processed`-status rows remain
  afterward, and (2) inserting a message for an event type deliberately never registered with
  `EventTypeRegistry`, confirming it's archived into `DeadLetterEvents` *and* removed from the
  active `OutboxMessages` table by the same `ProcessBatchAsync` pass — both against a real
  Postgres Testcontainer.
- Verified: full solution build clean; unit tests 203/203 (the existing `OutboxTests.cs` unit
  tests target `OutboxMessage`'s own `MarkProcessed`/`MarkFailed`/`MarkDeadLetter` methods
  directly and are unaffected — those methods still exist and behave identically; only what the
  *processor* does with the row afterward changed); integration tests 51/51 (49 prior + 2 new),
  against real Postgres/Redis/RabbitMQ Testcontainers — including every existing test across
  Campaigns/Contributions/Moderation that calls `ProcessOutboxMessagesAsync`, none of which
  asserted on rows surviving in the outbox table post-processing, so nothing needed updating
  beyond the processor itself.
