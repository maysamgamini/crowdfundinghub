# Architect's Gold Notes: Core Mental Models for Students & Tech Leads

> **Curriculum Artifact:** High-Impact Mental Models, Architectural Maxims, and Diagrammatic Thinking  
> **Target Audience:** Students, Tech Leads, Principal Architects  

---

## 1. "Coupling Is the Enemy, Not the Monolith"

Many engineering teams believe that decomposing an application into microservices will magically solve their architectural problems.

**The Golden Law:**  
> *"If you take a tangled monolith and distribute it across network boundaries, you do not get microservices. You get a Distributed Monolith—which combines all the complexity of distributed systems with all the brittleness of tight coupling."*

```
     Tangled In-Process Monolith
                 │
      [Split across network without fixing boundaries]
                 ▼
     Fragile Distributed Monolith
     (Cascading timeouts, partial failures, distributed deadlocks)
```

The goal of architecture is **low coupling and high cohesion**. You can achieve low coupling inside a single executable.

---

## 2. "Database Foreign Keys Are Concrete Chains"

When engineers design relational databases in a monolith, their instinct is to link everything with SQL foreign keys:
`contributions.campaign_id` $\rightarrow$ `campaigns.campaigns.id`.

```mermaid
graph LR
    subgraph BadDesign["Anti-Pattern: Cross-Schema Foreign Key"]
        T1["contributions table"] -->|"SQL FK Constraint (Concrete Chain)"| T2["campaigns table"]
    end
    subgraph GoodDesign["Pattern: Primitive Identifier Decoupling"]
        T3["contributions table<br/>(campaign_id UUID)"] -. "Scalar Value Reference (Zero SQL Chain)" .-> T4["campaigns table<br/>(id UUID)"]
    end
```

**Why This Is a Golden Rule:**
A SQL foreign key constraint requires both tables to live in the **same database engine**. It permanently locks your data models together.
- In a decomposition-ready architecture, **never write a cross-boundary foreign key**. Store the target entity's ID as a primitive scalar (`Guid CampaignId`).
- Let domain logic and application events enforce consistency across boundaries.

---

## 3. "The Dual-Write Fallacy & The Outbox/CDC Shield"

Every software engineer eventually tries to write this code:
```csharp
// THE DUAL-WRITE DISASTER
await dbContext.SaveChangesAsync(); // Step 1: Write to Database
await kafkaProducer.PublishAsync(event); // Step 2: Publish to Broker
```

### Why This Fails in Production:
1. **Network Crash:** Step 1 commits, but the network to Kafka drops. The database changed, but the rest of the world never hears about it.
2. **App Crash:** The machine loses power between Step 1 and Step 2.
3. **Database Rollback:** Step 2 fires, but Step 1 rolls back due to a transaction conflict. The event was published for an action that never happened!

```mermaid
sequenceDiagram
    autonumber
    participant App as Application
    participant DB as PostgreSQL
    participant Kafka as Kafka / RabbitMQ

    rect rgb(255, 240, 240)
        Note over App,Kafka: The Dual-Write Bug
        App->>DB: SaveChangesAsync() (Committed!)
        App--xKafka: PublishAsync() (CRASH / TIMEOUT!)
        Note over App,Kafka: System in Corrupt Inconsistent State
    end

    rect rgb(240, 255, 240)
        Note over App,Kafka: The Outbox / CDC Solution
        App->>DB: Save Entity + Insert Outbox Row (Atomic Single DB Commit)
        DB-->>App: OK!
        Note over DB,Kafka: Debezium / Background Poller reads DB and streams to Kafka
        DB->>Kafka: Stream Outbox Event (Guaranteed At-Least-Once!)
    end
```

**The Golden Law:**  
> *"You cannot achieve atomic consistency across two independent storage systems without Two-Phase Commit (which you do not want) or the Transactional Outbox Pattern / Change Data Capture (which you do want)."*

---

## 4. "Symmetric Architecture Is an Illusion (The 80/20 Rule)"

Never force an entire codebase into a single rigid mold.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ THE 80/20 RULE OF ENTERPRISE ARCHITECTURE:                                  │
│                                                                             │
│ [80% Low-Risk CRUD & Queries]            [20% High-Risk Transactional Core] │
│ - Draft creation, user bio, lookups      - Pledges, ledger balances, refunds│
│ - Pattern: Minimal API / Fast Slices     - Pattern: Rich DDD + Locks + CDC  │
│ - Focus: Velocity & Low Indirection      - Focus: Financial Invariants      │
└─────────────────────────────────────────────────────────────────────────────┘
```

A Principal Architect knows that **forcing a 14-file DDD pipeline onto a lookup table is just as incompetent as using raw inline SQL for a high-concurrency ledger balance update.**

---

## 5. "Asymmetric Cryptography: Sign Centrally, Verify Locally"

In microservices and modular monoliths, authentication must scale horizontally without bottlenecking on the identity database:

```mermaid
graph TD
    Client["Client / User"] -->|"1. POST /login (Credentials)"| IdSvc["Identity Service"]
    IdSvc -->|"2. Sign JWT with Private ECDSA Key"| Client
    Client -->|"3. HTTP Bearer Token"| EdgeSvc["Extracted Campaigns Service"]
    EdgeSvc -->|"4. Verify Locally using Public Key (JWKS)"| LocalCache["Cached JWKS Keys"]
    EdgeSvc -- "Zero Network Calls to Identity!" --> EdgeSvc
```

**The Golden Law:**  
> *"With symmetric keys (HS256), every service must hold the master secret. With asymmetric keys (ES256), the private key never leaves the Identity service. All other services verify tokens offline using public keys from `/.well-known/jwks.json`."*

---

## 6. "Event-Carried State Transfer vs. Synchronous RPC"

When Service B needs data from Service A:

| Approach | Mechanics | Coupling Level | Availability Impact |
|---|---|:---:|---|
| **Synchronous RPC (HTTP/gRPC)** | Service B calls `GET /api/serviceA/{id}` on every incoming request. | 🔴 **High (Temporal Coupling)** | If Service A is down, **Service B goes down too**. |
| **Event-Carried State Transfer** | Service A publishes `EntityUpdatedEvent`. Service B consumes it and maintains a local cache. | 🟢 **Zero (Autonomous)** | Service B handles requests locally with **100% uptime**, even if Service A is completely offline. |

---

---

## 7. "Databases Are Not Queues: MVCC Write Amplification & Autovacuum"

Many architects blindly implement the Transactional Outbox pattern by adding an `outbox` table and updating rows from `Status = Pending` to `Status = Processing` to `Status = Processed`.

**The Mechanical Sympathy Lesson:**
In PostgreSQL's Multi-Version Concurrency Control (MVCC), an `UPDATE` does not overwrite the row on disk. It writes a brand new row version (tuple) and marks the old one dead.
- Under 500 events/sec, mutating outbox row statuses generates **1,500 dead tuples per second**.
- PostgreSQL's `autovacuum` daemon falls behind, causing catastrophic table fragmentation, B-tree index bloat, and eviction of active domain data from `shared_buffers`.

```
Naive Outbox:   [Insert Row] -> [Update to Processing] -> [Update to Processed] = 3 Dead Tuples per Event!
Optimal Outbox: [Insert Row] -> [DELETE FROM outbox WHERE id = @id]            = Bounded Table Heap!
```

**The Golden Law:**
> *"If you use PostgreSQL as a transactional message outbox, delete messages immediately upon successful dispatch (`DELETE ON SUCCESS`). Archive failures to an append-only dead-letter table. Never let processed messages accumulate in an active poller table."*

---

## 8. "The Stateless Token Revocation Dilemma"

Architects choose asymmetric JWTs (ES256) so services can verify authentication offline against public JWKS (`/.well-known/jwks.json`) without hitting the Identity database.

**The Architectural Dilemma:**
Because validation is 100% offline and stateless, **a compromised token or deactivated user cannot be revoked until the token expires naturally**.

```
Pure Stateless JWT:    Fast offline verification, but ZERO immediate revocation capability.
Database Session Auth: Instant revocation capability, but CENTRALIZED DATABASE BOTTLENECK on every request.
```

**The Principal Solution (The Hybrid Tier):**
1. Issue **short-lived access tokens** (10–15 minutes).
2. Use **Refresh Token Rotation (RTR)** with automated reuse detection.
3. Maintain a low-latency **Redis Security Stamp blacklist**: downstream services perform local cryptographic verification for normal endpoints, and check Redis only when high-value administrative/financial actions occur.

---

## 9. "Cross-Cutting Governance: Pipeline Behaviors Over Handler Pollution"

When enforcing cross-cutting concerns (immutable audit logs, validation, metrics, distributed tracing):

**The Antipattern:**
Manually injecting `IAuditRepository` or `ILogger` into 40 distinct command handlers.
- Violates Single Responsibility.
- Pollutes domain orchestration with infrastructure plumbing.
- Guarantees that future engineers will forget to add audit logging to new endpoints.

**The Golden Law:**
> *"Cross-cutting governance belongs in the Dispatcher Pipeline Behavior (Decorator pattern), never in the Use Case Handler. Command handlers must remain 100% pure business logic."*

---

## 10. "Multi-Replica Scaling: Pre-Commit Cache Eviction Races"

Monoliths are never deployed as single instances in production; they run across 3, 5, or 10 container replicas behind an Application Load Balancer.

**The Race Condition:**
If Pod A updates an entity and evicts its Redis cache key *before* `SaveChangesAsync` commits the transaction to PostgreSQL, Pod B can concurrently handle a read request, query the database, read the **uncommitted old data**, and repopulate Redis with stale data for 30 minutes!

```mermaid
sequenceDiagram
    participant PodA as Pod A (Writer)
    participant Redis as Redis Cache
    participant DB as PostgreSQL
    participant PodB as Pod B (Reader)

    PodA->>Redis: 1. Evict Cache Key (BEFORE Commit!)
    PodB->>Redis: 2. Cache Miss!
    PodB->>DB: 3. Read Entity (Returns OLD DB State!)
    PodB->>Redis: 4. Populate Redis with OLD State (TTL 30s)
    PodA->>DB: 5. SaveChangesAsync() (NEW State Committed!)
    Note over PodA,PodB: Cache is now poisoned with stale data for 30 seconds!
```

**The Golden Law:**
> *"Never evict or invalidate caches inside the transactional write path before commit. Always bind cache evictions to post-commit hooks (`ITransactionExecutor.OnCommitted`) and broadcast invalidations across replicas via Redis Pub/Sub channels."*

---

## 11. The 10 Commandments of Decomposition-Ready Architecture

1. **Thou shalt not reference another module's domain assembly.**
2. **Thou shalt communicate across module boundaries exclusively through contract DTOs and integration events.**
3. **Thou shalt isolate database tables into schemas per module.**
4. **Thou shalt never declare a SQL foreign key across schema boundaries.**
5. **Thou shalt never dual-write to a database and a message broker in the same operation.**
6. **Thou shalt sign tokens with asymmetric cryptography and publish public keys via JWKS.**
7. **Thou shalt not route queries through write repositories; project directly from data to DTO.**
8. **Thou shalt right-size each vertical slice: keep simple CRUD simple, and save heavy DDD for mission-critical aggregates.**
9. **Thou shalt automate boundary enforcement in CI via architecture unit tests (`NetArchTest`).**
10. **Thou shalt design systems so that extracting a microservice requires zero lines of business logic rewritten.**

