# QA Ticket: TICKET-055

**Title:** Unbounded Batch Loading in Campaign Refund Saga: Single-Transaction OOM, Lock Contention & Timeout Hazards  
**Severity:** 🟠 P1 (High - Scalability, Database Lock Saturation & Memory Exhaustion)  
**QA Focus Area:** Performance, Persistence Scalability & Distributed Sagas  
**Found By:** `qa-performance-persistence` & `qa-resilience-outbox`  
**Status:** Open  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

In [TICKET-027](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/qa-tickets/TICKET-027-DISTRIBUTED-CROWDFUNDING-LIFECYCLE-REFUND-SAGA.md), the event-driven campaign cancellation and expiration refund saga was introduced. When a campaign fails or is cancelled, [`CampaignTerminationRefundHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/Contributions/Events/CampaignTerminationRefundHandler.cs#L45-L62) consumes the event:

```csharp
private async Task RefundAllSucceededContributionsAsync(Guid campaignId, CancellationToken cancellationToken)
{
    var succeededContributions = await _contributionRepository.GetSucceededByCampaignIdAsync(campaignId, cancellationToken);

    if (succeededContributions.Count == 0)
    {
        return;
    }

    await _transactionExecutor.ExecuteAsync(async ct =>
    {
        foreach (var contribution in succeededContributions)
        {
            contribution.Refund(_dateTimeProvider.UtcNow);
            await _contributionRepository.UpdateAsync(contribution, ct);
        }
    }, cancellationToken);
}
```

### The Architectural Defect

The current refund saga design is completely unbounded:
1. **Unbounded In-Memory Change Tracking:** `GetSucceededByCampaignIdAsync` executes a raw SQL `SELECT` without a `LIMIT` or cursor pagination clause. For successful campaigns with 10,000 to 50,000 backers that are subsequently cancelled (e.g. fraudulent creator discovered post-campaign), EF Core materializes all 50,000 `Contribution` aggregates into memory simultaneously.
2. **Gigantic Single-Transaction Write Amplification:**
   - 50,000 `Contribution` aggregates are modified to `Status = Refunded`.
   - Each aggregate adds a `ContributionRefundedDomainEvent`.
   - `ContributionTransactionExecutor` collects all 50,000 domain events and constructs 50,000 `OutboxMessage` records.
   - `SaveChangesAsync` generates a single transaction containing **100,000 SQL statements** (50,000 UPDATEs + 50,000 INSERTs).
3. **Database Lock Saturation & Timeouts:** Holding row-level locks on 50,000 rows inside an active PostgreSQL transaction for tens of seconds saturates connection pools, triggers lock table exhaustion (`max_locks_per_transaction`), causes Npgsql command timeout exceptions (`TimeoutException: 30 seconds`), and starves concurrent checkout operations.

---

## 2. Blast Radius & Impact Analysis

- **Out Of Memory (OOM) Crashes:** On Kubernetes or container hosts with 512MB–1GB RAM limits, materializing 50,000 tracked aggregates and 50,000 outbox entities causes the .NET GC to exhaust container memory and trigger OOMKills.
- **Poison Event & Outbox Worker Deadlock:** If the handler times out after 30 seconds, Npgsql aborts the transaction. The outbox message remains unacknowledged, retries, and repeatedly times out or crashes, permanently blocking the `Contributions` module outbox processor.
- **System-Wide Read-Write Lock Starvation:** Long-running write transactions holding row locks on the `contributions` table block analytical queries and administrative back-office operations.

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Teach architects the **Bounded Chunking Principle in Batch Sagas**:
> *Never process an unbounded collection of domain aggregates in a single database transaction. Sagas operating over large cardinality collections must execute in bounded, idempotent chunks (micro-batches).*

By chunking batch updates into sizes of 100–250 records per transaction, memory consumption remains strictly $O(1)$ regardless of whether a campaign has 10 backers or 1,000,000 backers.

---

## 4. Affected Files & Modules

- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/Contributions/Events/CampaignTerminationRefundHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/Contributions/Events/CampaignTerminationRefundHandler.cs)
- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Abstractions/Persistence/IContributionRepository.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Abstractions/Persistence/IContributionRepository.cs)
- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/Persistence/Repositories/ContributionRepository.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/Persistence/Repositories/ContributionRepository.cs)

---

## 5. Greenfield Remediation Guidance

### Step 1: Add Keyset / Bounded Batch Query to `IContributionRepository`

```csharp
Task<IReadOnlyList<Contribution>> GetSucceededBatchByCampaignIdAsync(
    Guid campaignId,
    int batchSize,
    CancellationToken cancellationToken);
```

In `ContributionRepository.cs`:
```csharp
public async Task<IReadOnlyList<Contribution>> GetSucceededBatchByCampaignIdAsync(
    Guid campaignId,
    int batchSize,
    CancellationToken cancellationToken)
{
    return await _dbContext.Contributions
        .Where(c => c.CampaignId == campaignId && c.Status == ContributionStatus.Succeeded)
        .OrderBy(c => c.CreatedAtUtc)
        .Take(batchSize)
        .ToListAsync(cancellationToken);
}
```

### Step 2: Implement Chunked Processing in `CampaignTerminationRefundHandler`

```csharp
private async Task RefundAllSucceededContributionsAsync(Guid campaignId, CancellationToken cancellationToken)
{
    const int batchSize = 200;
    while (!cancellationToken.IsCancellationRequested)
    {
        var batch = await _contributionRepository.GetSucceededBatchByCampaignIdAsync(campaignId, batchSize, cancellationToken);
        if (batch.Count == 0)
        {
            break;
        }

        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            foreach (var contribution in batch)
            {
                contribution.Refund(_dateTimeProvider.UtcNow);
                await _contributionRepository.UpdateAsync(contribution, ct);
            }
        }, cancellationToken);
    }
}
```

Because `.Where(c => c.Status == ContributionStatus.Succeeded)` naturally excludes contributions as soon as they are transitioned to `Refunded` and committed, each subsequent iteration fetches the next batch of 200 items in $O(1)$ memory, committing transactions incrementally.

---

## 6. Verification & Acceptance Criteria

1. **Bounded Batch Execution:** The refund saga processes contributions in chunks of 200, committing each chunk in an independent transaction.
2. **Memory Stability:** Processing a campaign with 10,000+ contributions maintains flat memory consumption without spiking EF Core change tracking state.
3. **Resumability on Disruption:** If a container is restarted midway through refunding, the next execution picks up exactly where the previous batch left off without re-refunding processed contributions.
