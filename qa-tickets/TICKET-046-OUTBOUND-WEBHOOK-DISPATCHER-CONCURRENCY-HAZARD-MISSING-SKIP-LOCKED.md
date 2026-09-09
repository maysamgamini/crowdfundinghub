# QA Ticket: TICKET-046

**Title:** Outbound Webhook Dispatcher Concurrency Hazard: Missing `SKIP LOCKED` Enables Duplicate Webhook Dispatches Under Multi-Replica Deployments  
**Severity:** 🟠 P1 (High - Concurrency, Outbound API Duplicate Deliveries & Multi-Replica Scale)  
**QA Focus Area:** Concurrency, Multi-Instance Topologies & Background Worker Reliability  
**Found By:** `qa-concurrency-audit` & `qa-resilience-outbox`  
**Status:** Open  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

In TICKET-035, the Outbound Creator Webhook Engine was introduced to dispatch event notifications (e.g. `pledge.confirmed`) to creators' external CRM and Zapier endpoints via [`WebhookDispatcherBackgroundService`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/CampaignUpdates/CrowdFunding.Modules.CampaignUpdates.Infrastructure/Services/WebhookDispatcherBackgroundService.cs).

Every 5 seconds, the service calls `_taskRepository.ClaimDueBatchAsync(BatchSize, nowUtc, cancellationToken)`.

### The Concurrency Hazard in `ClaimDueBatchAsync`
Inspecting [`WebhookDeliveryTaskRepository.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/CampaignUpdates/CrowdFunding.Modules.CampaignUpdates.Infrastructure/Persistence/Repositories/WebhookDeliveryTaskRepository.cs#L23-L30):
```csharp
public async Task<IReadOnlyList<WebhookDeliveryTask>> ClaimDueBatchAsync(int batchSize, DateTime nowUtc, CancellationToken cancellationToken)
{
    return await _dbContext.WebhookDeliveryTasks
        .Where(x => x.Status == WebhookDeliveryStatus.Pending && x.ScheduledAtUtc <= nowUtc)
        .OrderBy(x => x.ScheduledAtUtc)
        .Take(batchSize)
        .ToListAsync(cancellationToken);
}
```

Notice:
1. It executes a basic LINQ query **without row-level locking (`FOR UPDATE SKIP LOCKED`)**.
2. It **does NOT transition the status from `Pending` to `Processing`** within an atomic SQL claim.
3. The rows remain in `Pending` status while the HTTP POST requests are being made across the public internet!

---

## 2. Blast Radius & Multi-Replica Duplicate Dispatches

In any production deployment with $\ge 2$ replicas (Kubernetes, AWS ECS):
1. Both Instance 1 and Instance 2 wake up at the 5-second tick.
2. Both execute `ClaimDueBatchAsync` and receive the **exact same 20 `WebhookDeliveryTask` IDs**.
3. Both instances execute parallel `HttpClient.SendAsync()` calls to the creator's endpoint.
4. **Duplicate Webhook Delivery:** The creator's external system receives duplicate webhook calls for every pledge event! If the creator connects the webhook to an external inventory order system or Zapier, double fulfillment or duplicate accounting entries occur.
5. Both instances attempt to call `taskRepository.UpdateAsync(task)` upon completion, triggering race conditions and potential concurrency conflicts on task state updates.

Contrast this with the Transactional Outbox processor (`OutboxClaimQuery.cs`), which uses raw SQL `FOR UPDATE SKIP LOCKED` precisely so multi-instance workers never race or double-dispatch.

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Teach architects why **Any Polling Work-Queue in a Multi-Replica Environment Requires PostgreSQL `FOR UPDATE SKIP LOCKED` or an Atomic State Lock**. An asynchronous task table cannot be treated as a simple entity repository. If multiple background worker instances poll the table, they must atomically claim non-overlapping batches to guarantee at-most-once or orderly at-least-once dispatching without duplicate parallel execution.

### Monolith First, Microservices Ready
Even before extracting CampaignUpdates into an independent microservice, running multiple instances of the Modular Monolith behind a load balancer is the standard deployment topology for high availability. Ensuring background services do not double-process tasks is mandatory for horizontal scalability.

---

## 4. Affected Files & Modules

- [`src/Modules/CampaignUpdates/CrowdFunding.Modules.CampaignUpdates.Infrastructure/Persistence/Repositories/WebhookDeliveryTaskRepository.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/CampaignUpdates/CrowdFunding.Modules.CampaignUpdates.Infrastructure/Persistence/Repositories/WebhookDeliveryTaskRepository.cs)
- [`src/Modules/CampaignUpdates/CrowdFunding.Modules.CampaignUpdates.Infrastructure/Services/WebhookDispatcherBackgroundService.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/CampaignUpdates/CrowdFunding.Modules.CampaignUpdates.Infrastructure/Services/WebhookDispatcherBackgroundService.cs)
- [`src/Modules/CampaignUpdates/CrowdFunding.Modules.CampaignUpdates.Domain/Aggregates/WebhookDeliveryTask.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/CampaignUpdates/CrowdFunding.Modules.CampaignUpdates.Domain/Aggregates/WebhookDeliveryTask.cs)

---

## 5. Greenfield Remediation Guidance

1. Implement an atomic SQL claim query on `WebhookDeliveryTaskRepository` utilizing PostgreSQL's `FOR UPDATE SKIP LOCKED`:
   ```sql
   WITH cte AS (
       SELECT id FROM campaign_updates.webhook_delivery_tasks
       WHERE status = 'Pending' AND scheduled_at_utc <= @nowUtc
       ORDER BY scheduled_at_utc
       LIMIT @batchSize
       FOR UPDATE SKIP LOCKED
   )
   UPDATE campaign_updates.webhook_delivery_tasks t
   SET status = 'Processing', locked_until_utc = @lockedUntilUtc, worker_id = @workerId
   FROM cte
   WHERE t.id = cte.id
   RETURNING t.*;
   ```
2. In `WebhookDispatcherBackgroundService`, ensure tasks claimed in `Processing` state are marked `Delivered` on success, or scheduled with exponential backoff on failure.

---

## 6. Verification & Acceptance Criteria

1. **Atomic Batch Claiming:** `ClaimDueBatchAsync` uses `SKIP LOCKED` to atomically claim and lock due tasks.
2. **Multi-Replica Test:** An integration test simulating two concurrent worker loops processing the same batch of 20 tasks dispatches each task exactly once across the workers with zero duplicates.
