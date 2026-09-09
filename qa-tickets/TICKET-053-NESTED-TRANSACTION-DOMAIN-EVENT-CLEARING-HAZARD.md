# QA Ticket: TICKET-053

**Title:** Nested Transaction Domain Event Clearing Hazard in `CampaignTransactionExecutor`: Inner Call Flushes and Clears Outer Entity Domain Events  
**Severity:** 🔴 P1 (High - Lost Outbox Messages & Silent Domain Event Dropping)  
**QA Focus Area:** Clean Architecture Infrastructure, Transaction Management & Outbox Reliability  
**Found By:** `qa-resilience-outbox` & `qa-concurrency-audit`  
**Status:** Open  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

In [`CampaignTransactionExecutor.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Infrastructure/Transactions/CampaignTransactionExecutor.cs#L62-L108), the executor correctly recognizes whether the current execution owns the database transaction:

```csharp
var ownsTransaction = _dbContext.Database.CurrentTransaction is null;
IDbContextTransaction? transaction = null;

if (ownsTransaction)
{
    transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
}

try
{
    if (advisoryLockKey is not null)
    {
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({advisoryLockKey.Value})",
            cancellationToken);
    }

    var result = await action(cancellationToken);
    var domainEvents = DomainEventAccessor.GetDomainEvents(_dbContext);
    var outboxMessages = domainEvents.Select(MapApplicationEvent).ToArray();

    if (outboxMessages.Length > 0)
    {
        await _dbContext.OutboxMessages.AddRangeAsync(outboxMessages, cancellationToken);
    }

    await _dbContext.SaveChangesAsync(cancellationToken);
    DomainEventAccessor.ClearDomainEvents(_dbContext);

    if (transaction is not null)
    {
        await transaction.CommitAsync(cancellationToken);
        await FlushPendingCacheInvalidationsAsync(cancellationToken);
    }

    return result;
}
```

### The Architectural Defect

While lines 65–68 and 96–106 guard `BeginTransactionAsync`, `CommitAsync`, and cache invalidation behind `ownsTransaction` (`if (transaction is not null)`), lines 85–94 **run unconditionally on every invocation, including nested calls**:

```csharp
var domainEvents = DomainEventAccessor.GetDomainEvents(_dbContext);
var outboxMessages = domainEvents.Select(MapApplicationEvent).ToArray();

if (outboxMessages.Length > 0)
{
    await _dbContext.OutboxMessages.AddRangeAsync(outboxMessages, cancellationToken);
}

await _dbContext.SaveChangesAsync(cancellationToken);
DomainEventAccessor.ClearDomainEvents(_dbContext);
```

When a business operation consists of composed steps (for instance, an outer command handler modifying a Campaign aggregate, which then delegates to a domain service or helper that executes another operation via `_transactionExecutor.ExecuteAsync`):

1. **Premature Global Domain Event Harvesting:** `DomainEventAccessor.GetDomainEvents(_dbContext)` inspects the `ChangeTracker` for *all* tracked entities across the entire DbContext. It extracts domain events from the outer aggregate before the outer unit of work has completed.
2. **Intermediate Persistence & premature `ClearDomainEvents`:** The inner execution maps those events to Outbox messages, writes them to `_dbContext.OutboxMessages`, runs `SaveChangesAsync`, and executes `DomainEventAccessor.ClearDomainEvents(_dbContext)`.
3. **Outer Domain Event Disappearance:** When control returns to the outer caller, all domain events have been wiped from its tracked entities. If the outer caller raises further events, or if an outer unit of work handler expects to inspect domain events, state is fragmented.
4. **Premature Flush Hazard:** If the outer caller fails or throws an exception after the inner call returns, `SaveChangesAsync` was already called on the shared DbContext. While the outer transaction rollback aborts the database transaction, the EF Core `ChangeTracker` remains in a dirty, inconsistent state (`EntityState.Unchanged` for prematurely saved entities), preventing clean retry behaviors.

---

## 2. Blast Radius & Impact Analysis

- **Transactional Outbox Fragmentation:** Outbox events from an outer operation are prematurely mapped and saved halfway through execution, instead of atomically at the completion of the business transaction.
- **EF Core ChangeTracker Corruption:** Flushed entities cannot be reliably rolled back in-memory if subsequent business rules throw domain exceptions.
- **Violates Unit of Work Atomicity:** An inner helper must not decide when the DbContext persists or when domain events are cleared; only the root transaction coordinator possesses that authority.

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Teach architects the **Root Unit of Work Boundary Principle**:
> *In nested transaction and Unit of Work execution patterns, only the root coordinator (the invocation that started the transaction) may harvest domain events, commit changes, and clear entity event collections.*

Child/nested execution scopes must participate in the ambient transaction without prematurely flushing the change tracker or altering the entity event queue.

---

## 4. Affected Files & Modules

- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Infrastructure/Transactions/CampaignTransactionExecutor.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Infrastructure/Transactions/CampaignTransactionExecutor.cs)
- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/Transactions/ContributionTransactionExecutor.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/Transactions/ContributionTransactionExecutor.cs)
- [`src/Modules/Identity/CrowdFunding.Modules.Identity.Infrastructure/Transactions/IdentityTransactionExecutor.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Identity/CrowdFunding.Modules.Identity.Infrastructure/Transactions/IdentityTransactionExecutor.cs)
- [`src/Modules/Moderation/CrowdFunding.Modules.Moderation.Infrastructure/Transactions/ModerationTransactionExecutor.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Moderation/CrowdFunding.Modules.Moderation.Infrastructure/Transactions/ModerationTransactionExecutor.cs)

---

## 5. Greenfield Remediation Guidance

Guard domain event collection, outbox generation, `SaveChangesAsync`, and `ClearDomainEvents` to execute only when `ownsTransaction` is true:

```csharp
var result = await action(cancellationToken);

if (ownsTransaction)
{
    var domainEvents = DomainEventAccessor.GetDomainEvents(_dbContext);
    var outboxMessages = domainEvents.Select(MapApplicationEvent).ToArray();

    if (outboxMessages.Length > 0)
    {
        await _dbContext.OutboxMessages.AddRangeAsync(outboxMessages, cancellationToken);
    }

    await _dbContext.SaveChangesAsync(cancellationToken);
    DomainEventAccessor.ClearDomainEvents(_dbContext);

    if (transaction is not null)
    {
        await transaction.CommitAsync(cancellationToken);
        await FlushPendingCacheInvalidationsAsync(cancellationToken);
    }
}

return result;
```

If an inner call requires its queries to see preceding changes within the same transaction, explicit flushing should only be invoked if strictly requested, but outbox mapping and domain event clearing must remain strictly anchored to the root transaction owner.

---

## 6. Verification & Acceptance Criteria

1. **Nested Invocation Atomicity:** When `ExecuteAsync` is called inside an existing `ExecuteAsync` block, the inner block does not harvest or clear domain events on tracked entities.
2. **Domain Event Preservation:** Domain events raised prior to an inner call remain attached to tracked entities and are all captured together by the root transaction outbox commit.
3. **Zero Leaked Changes on Outer Abort:** If an outer operation throws an exception after an inner call finishes, no outbox messages are emitted and the transaction rolls back cleanly.
