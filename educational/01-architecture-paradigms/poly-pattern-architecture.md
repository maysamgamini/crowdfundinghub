# Poly-Pattern Architecture: Pragmatism vs. Dogmatism in Enterprise Systems

> **Key Architectural Maxim:**  
> *"Symmetric architecture is an illusion. The mark of a Principal Architect is not the ability to enforce one pattern everywhere, but the wisdom to apply the simplest pattern that safely satisfies each feature's operational invariants."*

---

## 1. The Fallacy of Homogeneous Architecture

In software engineering pedagogy, courses often present a single architectural pattern—whether it is Clean Architecture, CQRS, or Domain-Driven Design (DDD)—and mandate that **every single feature in the system must strictly follow it**.

This dogmatic uniformity leads to catastrophic friction in real-world systems:
- A simple `POST /api/campaigns/categories` lookup requires 12 files, 3 DTO mappings, and an outbox queue.
- Developers spend 80% of their velocity maintaining empty abstractions for trivial CRUD logic.
- The engineering team becomes frustrated, declaring that *"Clean Architecture is slow and over-engineered"*, when in reality, **the misuse of the pattern was the failure.**

### The Reality of Modern Enterprise Systems
High-scale platforms (e.g., Shopify, Amazon, Stripe) are **intentionally heterogeneous**. They recognize that in any business application:
- **80% of the codebase** consists of low-risk, low-concurrency CRUD, lookups, and reporting queries.
- **20% of the codebase** represents the high-concurrency, mission-critical financial core where data corruption, lost updates, or double-spends would destroy the business.

Treating the 80% with the heavy ceremony of the 20% is **accidental complexity**.  
Treating the 20% with the naive simplicity of the 80% is **negligence**.

---

## 2. The 3-Tier Architectural Pattern Matrix

In this decomposition-ready Modular Monolith, three patterns live side-by-side. The matrix matches the code and [`docs/POLY_PATTERN_TIER_ASSIGNMENT.md`](../../docs/POLY_PATTERN_TIER_ASSIGNMENT.md):

```mermaid
quadrantChart
    title The Pattern Selection Matrix
    x-axis Low Transactional Risk --> High Financial / Concurrency Risk
    y-axis Low System Blast Radius --> Multi-System Orchestration
    quadrant-1 "Tier 3: Aggregate + Locks + Outbox"
    quadrant-2 "Tier 3 when another module must react"
    quadrant-3 "Tier 1 or Tier 2: Direct DbContext"
    quadrant-4 "Tier 3 when concurrent writes can corrupt"
    "Category Lookups": [0.05, 0.05]
    "Draft Campaign Creation": [0.15, 0.20]
    "Notification Preferences": [0.25, 0.30]
    "Campaign Published Fan-out": [0.30, 0.85]
    "Confirm Pledge & Balance": [0.95, 0.95]
    "Campaign Deadline & Refund": [0.85, 0.90]
```

### Detailed Breakdown of the 3 Tiers

| Pattern Tier | Architectural Style | Core Pieces | When to Use (Business Scenarios) | Implementation Cost |
|---|---|---|---|:---:|
| **Tier 1: Pragmatic Vertical Slice** | One endpoint | Endpoint + the module's `DbContext` | Simple lookups and single-row edits that no other module cares about. | Minimal (1 file) |
| **Tier 2: Command + Handler on the DbContext** | Command and handler, no aggregate, no repository | Command + handler in the module's Infrastructure, using its own `DbContext` directly. Real example: Notifications preferences (`Notifications.Infrastructure/Features/Preferences`) | Writes with no invariants worth a domain model, no events, and no concurrent-write risk. | Low (2-3 files) |
| **Tier 3: Rich DDD Transactional Core** | Aggregate Root + repository | Aggregate, repository, command dispatcher, transactional outbox, `pg_advisory_xact_lock`, `xmin` tokens | Pledges, campaign funding balance, double-spend prevention, anything another module must react to reliably. | High (10-12 files) |

The outbox belongs to Tier 3: only aggregates that raise events write to it. Cross-module reactions run from the outbox through the message bus (`InProcess` or `RabbitMQ`; log-based CDC such as Debezium is a possible evolution, not something this repository implements).

> **Note on read slices.** Reads are not a tier. Query handlers already project straight from EF Core with `AsNoTracking()` and bypass the write repositories; no Dapper or cache layer is involved (Dapper is not a package in this repo).

### Lessons from applying the tiers

Two questions decide the tier, instead of dogma:

1. **Does another module need to react reliably?** If yes, the write must raise an event through the outbox, which is Tier 3.
2. **Can concurrent writes corrupt this data?** Answer it by looking at **every writer of the table**, not only the feature's own handlers.

Webhook subscriptions show why. They look like plain CRUD, so they were tried as Tier 2 and stayed Tier 3: `WebhookSubscriptionConcurrencyTests` reproduced a lost update. The dispatcher reads a subscription, makes an HTTP call of up to 5 seconds, then saves its stale copy, which re-activated a subscription the owner had just deleted. An `xmin` concurrency token fixed it.

---

## 3. Side-by-Side Code Contrast: Tier 1 vs. Tier 3

To teach students the difference between pragmatic simplicity and justified complexity, examine the exact same business entity under two different operations:

### Tier 1: Creating a Draft Campaign (Pragmatic Vertical Slice)
Creating a draft campaign has **zero concurrency competition**, **no money is moving**, and **no other service cares yet**. 

Using a 14-file pipeline here is architectural excess. A single-file Minimal API is the superior engineering choice:

```csharp
// src/Features/Campaigns/CreateDraftCampaignEndpoint.cs
namespace CrowdFunding.Features.Campaigns;

public static class CreateDraftCampaignEndpoint
{
    public record Request(string Title, string Story, string Category, decimal GoalAmount, string Currency, DateTime DeadlineUtc);
    public record Response(Guid Id, string Title, string Status);

    public class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Story).NotEmpty().MinimumLength(20).MaximumLength(5000);
            RuleFor(x => x.GoalAmount).GreaterThan(0);
            RuleFor(x => x.DeadlineUtc).GreaterThan(DateTime.UtcNow.AddDays(1));
        }
    }

    public static void Map(WebApplication app)
    {
        app.MapPost("/api/campaigns/drafts", async (
            Request req, 
            CampaignsDbContext db, 
            IValidator<Request> validator, 
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(req, ct);
            if (!validation.IsValid) 
                return Results.ValidationProblem(validation.ToDictionary());

            var campaign = new Campaign(req.Title, req.Story, req.Category, req.GoalAmount, req.Currency, req.DeadlineUtc);
            db.Campaigns.Add(campaign);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/campaigns/{campaign.Id}", new Response(campaign.Id, campaign.Title, campaign.Status.ToString()));
        })
        .RequireAuthorization("campaigns:create")
        .WithTags("Campaigns");
    }
}
```
- **Files Touched:** 1 file.
- **Cognitive Overhead:** Immediate comprehension.
- **Performance:** Direct EF Core pipeline without dispatcher overhead.

---

### Tier 3: Confirming a Pledge Payment (Rich DDD + Financial Invariants)
Conversely, when a backer's payment is confirmed by Stripe/Adyen:
1. Pledged funds must be credited to the campaign balance.
2. The campaign's raised amount cannot overflow or desync.
3. Concurrent payments to a viral campaign must be serialized to prevent lost updates.
4. An outbox event must be committed atomically to notify downstream services.

Here, **the 14-file pipeline is 100% justified**:

```csharp
// Inside Campaigns Application, crediting a confirmed pledge (AddPledgeToCampaignCommandHandler).
// Pledges has already confirmed the payment under its own xmin token and published the event through its outbox.
await _transactionExecutor.ExecuteAsync(advisoryLockKey, async ct =>
{
    // 1. The executor first takes the PostgreSQL advisory lock on the campaign ID: SELECT pg_advisory_xact_lock(key)

    // 2. Load the Aggregate Root inside the lock & enforce its invariants
    var campaign = await _campaignRepository.GetByIdAsync(campaignId, ct);

    // 3. Ledger row first (unique per pledge), so a redelivered event is a no-op
    if (await _pledgeLedger.TryRecordAsync(campaignId, pledgeId, amount, currency, ct))
    {
        campaign.ApplyConfirmedPledge(new Money(amount, currency));
        await _campaignRepository.UpdateAsync(campaign, ct); // optimistic concurrency token checked
    }

    // 4. Domain events are captured and committed to the outbox table in the SAME transaction!
}, cancellationToken);
```
- **Files Touched:** Domain Aggregate, Repository, AdvisoryLockKey, OutboxMessage, Event Handler.
- **Why It's Justified:** Prevents financial loss, eliminates race conditions, and guarantees reliable downstream event delivery.

---

## 4. Debezium CDC vs. Polling Outbox: The Strategic Evolution

> **Not built.** This repository ships the polling outbox only (`ModuleOutboxProcessor`). The Debezium and Kafka path below is a design for a later extraction, shown to explain why the outbox table exists.

In an educational repository, the relationship between the **Transactional Outbox Table** and **Debezium Change Data Capture** must be clearly communicated:

```mermaid
sequenceDiagram
    autonumber
    actor Client as Backer Client
    participant App as Monolith Application
    participant DB as PostgreSQL (campaigns schema)
    participant WAL as PostgreSQL WAL (pgoutput)
    participant Deb as Debezium Connector
    participant Kafka as Apache Kafka
    participant Downstream as Extracted Microservices

    Client->>App: Confirm Pledge Payment
    rect rgb(240, 248, 255)
        Note over App,DB: Local ACID Transaction (Same Database)
        App->>DB: UPDATE pledges SET status = 'Succeeded'
        App->>DB: INSERT INTO pledges_outbox_messages (payload_json)
        DB-->>App: Commit Transaction
    end
    App-->>Client: 200 OK (Payment Confirmed)

    rect rgb(255, 245, 238)
        Note over DB,Kafka: Zero Application Overhead (Log-Based CDC)
        DB->>WAL: Emit Transaction WAL Records
        WAL->>Deb: Stream Binary Change Stream
        Deb->>Kafka: Publish to 'pledges.payment-confirmed' topic
        Kafka->>Downstream: Consume & Process Async
    end
```

### Why Debezium Changes the Educational Narrative
When students look at an `outbox_messages` table, their first question is: *"Why are we writing messages to a table instead of publishing directly to Kafka?"*

The answer is the **Dual-Write Problem**: If you save to the database and then call `await _kafkaProducer.ProduceAsync()`, the network can fail, the app can crash, or the database transaction can roll back after the message is sent.

By writing to the outbox table in the **same database transaction**:
1. In local development or early-stage monoliths, an **in-process poller** reads the table using `FOR UPDATE SKIP LOCKED`.
2. At scale, **Debezium** attaches to PostgreSQL's `pgoutput` logical replication slot, reading the exact same table directly from the Write-Ahead Log (WAL) and streaming to Kafka with **zero application query load and zero table updates**.

---

## 5. The Golden Rules of Poly-Pattern Monoliths

1. **Do not use a DDD Aggregate if there are no business invariants to protect.** An entity that only has getters and setters is an anemic data model—and that is completely fine for CRUD!
2. **Do not route queries through write repositories.** Queries should project directly from EF Core `AsNoTracking()` to the response DTO.
3. **Only write events to the Outbox table if another bounded context or external microservice genuinely needs to react to them.**
4. **Enforce boundaries through namespaces and project references, not through layers of empty classes.**

---

## 6. This Isn't Hypothetical — Run It

The Tier 1 vs. Tier 3 contrast in Section 3 above is illustrative pseudocode. The real thing runs
in this repository: [`src/Samples/RosettaStone/`](../../src/Samples/RosettaStone/) implements
**the same business requirement — create a campaign with a title, story, and target amount —
three times**, as Tier 1 (Minimal API), Tier 2 (Pragmatic CQRS), and a pointer to the real Tier 3
(`POST /api/campaigns`), all mapped side by side under the `RosettaStone` Swagger tag when you
`dotnet run --project src/API/CrowdFunding.API`. Its `README.md` carries the honest,
`wc -l`-measured scorecard — files, lines, assemblies, and the two questions ("does another
module need to react reliably?", "can concurrent writes corrupt this row?") that decide which
tier a real feature belongs in, instead of dogma deciding it.
