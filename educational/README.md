# The Architect's Guide: Designing Modular Monoliths for Frictionless Microservices Decomposition

> **Curriculum Level:** Principal Engineer, Software Architect, Tech Lead  
> **Reference Implementation:** `CrowdFundingHub` (.NET 10 / C# 14 / PostgreSQL 16)  
> **Core Thesis:** *Build a Monolith first, but design it with the strict modular discipline, data boundaries, and cryptographic decoupling required to slice off microservices without rewriting business logic.*

---

## 1. Executive Summary & Curriculum Vision

Most microservices migrations fail not because distributed systems are inherently flawed, but because **the monolith was never architected for decomposition**. When organizations attempt to break apart a typical legacy monolith, they encounter:
1. **Shared Database Tables & Cross-Schema SQL Joins**: Tangling domain models so tightly that separating databases causes massive operational regressions.
2. **Synchronous In-Process Coupling**: Hundreds of hidden, synchronous in-memory calls that turn into cascading HTTP timeouts and distributed deadlocks when converted to network hops.
3. **Dual-Write Inconsistencies**: Naive in-memory events (`IMediator.Publish`) that fail to guarantee atomicity between local database writes and distributed message brokers.
4. **Symmetric Secret Sprawl**: Symmetric HMAC JWT tokens requiring every extracted service to share the same master encryption key.

This educational curriculum and reference codebase provide the **definitive, production-tested blueprint** for designing an in-process **Modular Monolith** that is physically prepared for microservices decomposition from Day 1.

```mermaid
graph TD
    subgraph Phase1["Phase 1: In-Process Modular Monolith"]
        MonolithHost["API Host (CrowdFunding.API)"]
        subgraph ModLayer["Isolated In-Process Modules"]
            IdM["Identity Module"]
            CampM["Campaigns Module"]
            ContM["Contributions Module"]
            ModM["Moderation Module"]
            NotM["Notifications Module"]
            UpdM["CampaignUpdates Module"]
        end
        PGShared[("One database per module in the local stack<br/>each with its own schema")]
    end

    subgraph Phase2["Phase 2: Frictionless Microservice Extraction"]
        IdSvc["Identity Service (Container)"]
        CampSvc["Campaigns Service (Container)"]
        ContSvc["Contributions Service (Container)"]
        ModSvc["Moderation Service (Container)"]
        Broker["Distributed Message Bus (Kafka / RabbitMQ)"]
        PG1[("Identity DB")]
        PG2[("Campaigns DB")]
        PG3[("Contributions DB")]
    end

    IdM -. "1. Move to New Host" .-> IdSvc
    CampM -. "2. Point DbContext to New DB" .-> CampSvc
    ContM -. "3. Swap In-Process Poller for Debezium CDC" .-> ContSvc
    ModM -. "4. Route Traffic via YARP Gateway" .-> ModSvc
```

---

## 2. Curriculum Directory & Specification Index

This directory is organized into four thematic areas serving both as **teaching materials for architects** and as a **concrete engineering development specification**:

### [01. Architecture Paradigms](./01-architecture-paradigms/)
- **[`poly-pattern-architecture.md`](./01-architecture-paradigms/poly-pattern-architecture.md)**:  
  *Architectural Pragmatism vs. Dogmatism.* Why homogeneous architecture is an anti-pattern. Demonstrates how to balance 3 tiers in the same application (Tier 1 single endpoint on the module's `DbContext`, Tier 2 command and handler on the `DbContext`, Tier 3 rich DDD aggregates with repository, outbox, locks and tokens).
- **[`monolith-to-microservices-blueprint.md`](./01-architecture-paradigms/monolith-to-microservices-blueprint.md)**:  
  The 6 non-negotiable architectural prerequisites for zero-rewrite microservice extraction: Contract Assemblies, Schema Isolation, Zero Cross-Schema Foreign Keys, Transactional Outbox, Asymmetric JWKS Edge Verification, and assembly-reference architecture tests (the NetArchTest library is not used).
- **[`justifying-the-outbox-and-complex-crud.md`](./01-architecture-paradigms/justifying-the-outbox-and-complex-crud.md)**:  
  *Justifying the Outbox Pattern & Complex CRUD.* Why in-memory same-database writes don't need outboxes, but external I/O integrations (SendGrid email receipts, AWS Lambda / Cloud Function AI moderation, Stripe payment intents, and reward perk allocation) mathematically require the Outbox and rich aggregate invariants.

### [02. Gap Analysis & Architectural Trade-offs](./02-gap-analysis-and-tradeoffs/)
- **[`over-engineering-audit.md`](./02-gap-analysis-and-tradeoffs/over-engineering-audit.md)**:  
  *Where Did We Go Too Far?* An unsparing critique of accidental complexity: 30 projects, the "14-file pipeline" for simple inserts, triple-mapping ceremony, and hand-rolled cryptographic key stores.
- **[`under-engineering-audit.md`](./02-gap-analysis-and-tradeoffs/under-engineering-audit.md)**:  
  *Where Didn't We Go Far Enough?* The hidden microservice decomposition roadblocks: synchronous cross-module query coupling (the removed `ICampaignContributionAvailabilityReader`, since replaced by a replicated read model), in-memory outbox poller coupling, physical database connection pool contention, and incomplete crowdfunding lifecycle rules (missing refunds and expiration workers).

### [03. Engineering Specifications & Roadmap](./03-engineering-spec-and-roadmap/)
- **[`transformation-spec.md`](./03-engineering-spec-and-roadmap/transformation-spec.md)**:  
  Concrete, sprint-ready engineering specification to evolve this codebase into the ultimate educational benchmark (Rosetta Stone comparative slices, pluggable `IMessageBus`, replicated read models, and containerization).
- **[`deconstruction-walkthrough.md`](./03-engineering-spec-and-roadmap/deconstruction-walkthrough.md)**:  
  Step-by-step engineering runbook showing how to extract the `Moderation` module into a standalone Dockerized microservice with zero lines of business logic rewritten.

### [04. Architect's Gold Notes](./04-architect-gold-notes/)
- **[`pedagogical-benefits-and-learning-matrix.md`](./04-architect-gold-notes/pedagogical-benefits-and-learning-matrix.md)**:  
  *The Pedagogical Value Catalog.* Comprehensive pedagogical analysis breaking down the 10 landmark educational lessons of the Modular Monolith curriculum, solving the "Distributed Systems Learning Paradox", and providing role-based learning outcomes for students, tech leads, and principal architects.
- **[`key-mental-models-for-students.md`](./04-architect-gold-notes/key-mental-models-for-students.md)**:  
  High-impact mental models, diagrams, and timeless maxims worth teaching to principal engineers and students (e.g., *"Coupling is the enemy, not the monolith"*, *"Databases are not queues: MVCC write amplification"*, *"The Stateless JWT Revocation Dilemma"*, *"Pre-commit cache eviction races"*).

---

## 3. The 3 Core Tenets of the Curriculum

```mermaid
flowchart TB
    T1["1. Pragmatism over dogmatism<br/>Right-size each slice to its transactional risk, concurrency and blast radius"]
    T2["2. Physical boundaries before physical processes<br/>Isolate modules with schemas, contract assemblies and assembly-reference tests before paying the network tax"]
    T3["3. Asynchronous event-carried state over synchronous RPC<br/>Replicate read models through events so each module stays autonomously available"]
    T1 --> T2 --> T3
```

Evidence for each tenet in the code: [`docs/POLY_PATTERN_TIER_ASSIGNMENT.md`](../docs/POLY_PATTERN_TIER_ASSIGNMENT.md) (tenet 1), [`docs/TESTS_THAT_TEACH.md`](../docs/TESTS_THAT_TEACH.md) and `ModuleSchemaRulesTests` (tenet 2), and the replicated read-model tests (tenet 3).

---

## 4. Where the running code differs from these documents

```mermaid
flowchart LR
    Docs["These documents"] --> Check{"Checked against code"}
    Check --> Map["design-to-code-mapping-round1.md<br/>what is built, simpler, or not built"]
    Check --> Tests["docs/TESTS_THAT_TEACH.md<br/>which tests prove which rule"]
    Check --> Tiers["docs/POLY_PATTERN_TIER_ASSIGNMENT.md<br/>3 tiers in the code and in these documents"]
```

The code, `docs/POLY_PATTERN_TIER_ASSIGNMENT.md`, `poly-pattern-architecture.md` and the Rosetta Stone sample agree on three tiers and the same two deciding questions. Reads are not a tier.
