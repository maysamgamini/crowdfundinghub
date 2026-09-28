# Educational Evaluation: Clean Architecture Modular Monolith

> **Document type:** pedagogical and architectural evaluation
> **Audience:** course authors, tech leads and senior engineers using this repository as a reference
> **System under review:** CrowdFunding Hub (.NET 10, C# 14, PostgreSQL 16, EF Core 10)
> **First evaluated:** 2026-09-08. **Revised:** 2026-09-28 to describe the code as it is now. Sections that describe earlier states say so explicitly.

---

## Table of Contents

1. [Summary and verdict](#1-summary-and-verdict)
2. [Architectural identity](#2-architectural-identity)
3. [Where the design goes further than a course needs](#3-where-the-design-goes-further-than-a-course-needs)
4. [Gaps found in the first evaluation and how they were closed](#4-gaps-found-in-the-first-evaluation-and-how-they-were-closed)
5. [The request journey for POST /api/campaigns](#5-the-request-journey-for-post-apicampaigns)
6. [Scorecard](#6-scorecard)
7. [Remaining gaps](#7-remaining-gaps)
8. [Evaluation against the stated intent: a monolith designed for decomposition](#8-evaluation-against-the-stated-intent-a-monolith-designed-for-decomposition)

---

## 1. Summary and verdict

CrowdFunding Hub is a modular monolith: one ASP.NET Core host, six business modules (Identity, Campaigns, Contributions, Moderation, CampaignUpdates, Notifications), each split into Contracts, Domain, Application and Infrastructure projects, plus shared BuildingBlocks. It is the reference codebase for a Clean Architecture course aimed at senior engineers.

**Verdict.** As a reference for senior engineers learning how to build a monolith that keeps the option to split into services, the codebase is strong: boundaries are enforced by tests, integration runs through a transactional outbox and inbox, and the core crowdfunding lifecycle is complete. As an introduction to Clean Architecture for less experienced developers it is heavy, because the infrastructure (JWKS signing keys, advisory locks, outbox leasing, an append-only ledger) is production grade. The course addresses this with the Rosetta Stone sample, which shows one use case at three levels of ceremony.

### Strengths for teaching

1. **Boundaries enforced by tests.** The [architecture tests](../tests/ArchitectureTests/CrowdFunding.ArchitectureTests/) inspect compiled assembly references (plain reflection, not NetArchTest): every module has the four layers, cross-module references go only to `Contracts`, `Domain` references only `BuildingBlocks.Domain`, `Contracts` reference no module internals, and the API host references no sample and contains exactly one controller.
2. **Real database tests.** Integration tests run against PostgreSQL and Redis in Testcontainers, so indexes, triggers, `FOR UPDATE SKIP LOCKED`, advisory locks and `ON CONFLICT` behave as in production.
3. **A readable outbox and inbox.** Each module that publishes events has its own outbox table and `ModuleOutboxProcessor<TDbContext>`; consumers that must run once use `IIdempotentConsumer`. See [outbox-architecture.md](outbox-architecture.md).
4. **Contrast in code.** [`src/Samples/RosettaStone`](../src/Samples/RosettaStone/README.md) implements "create a campaign" as a Minimal API with direct `DbContext` access, as pragmatic CQRS, and points to the full rich-domain pipeline. The controller-versus-Minimal-API comparison is documented in [minimal-apis-vs-controllers.md](minimal-apis-vs-controllers.md).

### Costs for teaching

1. **Project count.** 28 production projects (API host, three BuildingBlocks projects, six modules with four projects each), plus the Rosetta Stone host and the test projects.
2. **Infrastructure density.** A learner meets ES256 key management, advisory locks, outbox leases and a trigger-guarded ledger before the domain model. The course must sequence these topics deliberately.

---

## 2. Architectural identity

The first evaluation found that the project described itself as "vertical slice" while its structure is layered Clean Architecture inside each module. The README now describes it as a Clean Architecture modular monolith, which is accurate.

| Axis | Clean Architecture | Vertical Slice Architecture | CrowdFunding Hub |
|---|---|---|---|
| Primary organizing principle | Technical rings (Domain, Application, Infrastructure, Web) | One folder per use case | Modules by bounded context, Clean Architecture rings inside each module |
| Data access | Repository and unit-of-work abstractions | `DbContext` or SQL directly in the handler | Repositories plus a per-module transaction executor for writes; read services for queries |
| Co-location | A feature spans several projects | A feature lives in one folder or file | Feature folders inside each `Application` project (`Features/<Area>/Commands/<UseCase>`), with domain and infrastructure in their own projects |
| Where vertical slices appear | Not at all | Everywhere | Rosetta Stone tiers 1 and 2 only |

The honest label is "modular monolith with Clean Architecture per module and feature folders in the application layer". Pure vertical slices exist only in the Rosetta Stone sample, on purpose, for comparison.

---

## 3. Where the design goes further than a course needs

These are deliberate choices. Each is explained so the course can present it as a trade-off rather than a default.

### 3.1 Four projects per module

Physical projects make boundaries compile-time facts and let the architecture tests work on assembly references. The cost is navigation friction and a project reference decision for every new type. A single project per module with `internal` visibility would enforce most of the same rules with less structure, at the price of weaker guarantees about `Domain` staying free of infrastructure.

### 3.2 Hand-built ES256 signing and JWKS

Identity generates P-256 keys, stores them in PostgreSQL ([`EfSigningKeyStore.cs`](../src/Modules/Identity/CrowdFunding.Modules.Identity.Infrastructure/Services/EfSigningKeyStore.cs)), encrypts the private keys at rest with AES-256-GCM ([`SigningKeyProtector.cs`](../src/Modules/Identity/CrowdFunding.Modules.Identity.Infrastructure/Services/SigningKeyProtector.cs)), and publishes the public set at `/.well-known/jwks.json` ([`JwksEndpoint.cs`](../src/API/CrowdFunding.API/Security/JwksEndpoint.cs)). This is what lets another process validate tokens offline, which matters for extraction. In a real system a managed identity provider would own it. For a Clean Architecture lesson it is a detour; the course should treat it as its own topic.

### 3.3 Database concurrency primitives

[`AdvisoryLockKey.cs`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Domain/Common/AdvisoryLockKey.cs) folds a campaign `Guid` into a 64-bit key (XOR of its two halves) for `pg_advisory_xact_lock`, which serializes pledges, refunds and cancellation per campaign. [`OutboxClaimQuery.cs`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Persistence/OutboxClaimQuery.cs) claims due outbox rows with `FOR UPDATE SKIP LOCKED` and reclaims `Processing` rows whose `LockedUntilUtc` lease has expired. Both are correct and tested; both are systems programming that a learner must understand before the funding path makes sense. [append-only-ledger.md](append-only-ledger.md) explains the ledger that sits on top of the lock.

### 3.4 Request, command and result types

`CreateCampaignRequest` (API) and `CreateCampaignCommand` (Application) carry the same six fields. The aggregate is not a copy: `Campaign.Create` takes the owner, a `Money` goal and the current time and enforces invariants. Mapping is now explicit code in the endpoint (Mapster was removed), and validation runs once in `ValidationPipelineBehavior`, so the ceremony is two record types and one constructor call per use case. The duplication buys an API contract that can change independently of the application layer; for a simple create it is still ceremony, and the Rosetta Stone shows the alternative.

### 3.5 External metering

[`OpenMeterClient.cs`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Metering/OpenMeterClient.cs) sends CloudEvents to an external metering service. It runs in `PledgeConfirmedMeteringEventHandler`, an ordinary event handler after the pledge is confirmed, so it is off the request path and failures do not affect the pledge. It is still an external dependency that a pure architecture course does not need.

---

## 4. Gaps found in the first evaluation and how they were closed

| Gap in the first evaluation | Status | How it was closed |
|---|---|---|
| Crowdfunding lifecycle incomplete: no deadline processing, no refunds | Closed | `CampaignExpirationBackgroundService` (Campaigns.Infrastructure) scans published campaigns past their deadline every minute and dispatches `CompleteCampaignCommand` (goal met, status `Successful`) or `FailCampaignCommand` (status `Failed`). `CampaignTerminationRefundHandler` in Contributions handles `CampaignFailedApplicationEvent` and `CampaignCancelledApplicationEvent` and moves every `Succeeded` contribution to `Refunded` in batches. Refunds append compensating entries to the campaign ledger, so `RaisedAmount` falls as they land. |
| No vertical slice examples | Closed | Rosetta Stone tiers 1 and 2 in `src/Samples/RosettaStone`, a separate runnable host with its own `rosetta` schema and parity tests. |
| No contrast between styles | Closed | Rosetta Stone (three tiers of "create a campaign") and one MVC controller kept beside the Minimal APIs ([minimal-apis-vs-controllers.md](minimal-apis-vs-controllers.md)). |
| No Dockerfile or CI | Closed | [`src/API/CrowdFunding.API/Dockerfile`](../src/API/CrowdFunding.API/Dockerfile) (multi-stage) and [`.github/workflows/ci.yml`](../.github/workflows/ci.yml): format check, Release build with warnings as errors, then architecture, unit and integration tests. |
| Mapster mapping profiles | Removed | Explicit mapping in the endpoints. |
| Controllers plus Swashbuckle | Replaced | Minimal APIs in `src/API/CrowdFunding.API/Endpoints/` with `TypedResults` and named routes; built-in OpenAPI document at `/openapi/v1.json` with Scalar UI, Development and Testing only. One controller remains on purpose. |
| Validation called by hand | Replaced | `ValidationPipelineBehavior` runs all FluentValidation validators for a command before its handler. |
| In-memory cache would split across replicas | Closed | `CachedCampaignReadService` uses `IDistributedCache` backed by Redis. |

---

## 5. The request journey for POST /api/campaigns

```
[Client]
  -> API/Endpoints/CampaignEndpoints.cs              CreateAsync (Minimal API)
     API/Contracts/Campaigns/CreateCampaignRequest.cs
  -> BuildingBlocks.Application/Messaging/ICommandDispatcher.cs
     BuildingBlocks.Application/Validation/ValidationPipelineBehavior.cs
  -> Campaigns.Application/Features/Campaigns/Commands/CreateCampaign/
       CreateCampaignCommand.cs, CreateCampaignCommandValidator.cs,
       CreateCampaignCommandHandler.cs, CreateCampaignResult.cs
     Campaigns.Application/Abstractions/Persistence/ICampaignRepository.cs
  -> Campaigns.Domain/Aggregates/Campaign.cs          Campaign.Create
     Campaigns.Domain/Events/CampaignCreatedDomainEvent.cs
  -> Campaigns.Infrastructure/Persistence/Repositories/CampaignRepository.cs
     Campaigns.Infrastructure/Transactions/CampaignTransactionExecutor.cs
       (maps the domain event to CampaignCreatedApplicationEvent and writes the outbox row
        in the same transaction)
     Campaigns.Infrastructure/Persistence/DbContexts/CampaignsDbContext.cs
     BuildingBlocks.Infrastructure/Persistence/OutboxMessage.cs
  <- API/Contracts/Campaigns/CreateCampaignResponse.cs  (201 with Location from the named GET route)
```

About fourteen files across five projects. Several of them (the endpoint class, aggregate, repository, transaction executor) are shared by every Campaigns write, so the marginal cost of the next command is much lower than the first. After commit, the Campaigns outbox processor delivers `CampaignCreatedApplicationEvent` to four in-process consumers: Moderation (opens a review), Contributions and Notifications (replicated read models) and CampaignUpdates.

Tier 1 of the Rosetta Stone does the same create in one file of about fifty lines. Comparing the two is the core lesson: the heavy path buys invariants in the aggregate, atomic event publication and independent contracts; it is worth paying where those are needed.

---

## 6. Scorecard

| Dimension | Score | Notes |
|---|:---:|---|
| Boundary discipline | 9 / 10 | Enforced by architecture tests over assembly references and EF models; one schema and one database role per module, so PostgreSQL refuses cross-module queries. |
| Production realism | 9 / 10 | Outbox with leases and dead-lettering, inbox, append-only ledger, RFC 9457 errors, rate limiting, OpenTelemetry. |
| Test engineering | 9 / 10 | Unit, architecture and Testcontainers integration suites run in CI. |
| Domain completeness | 8 / 10 | Full lifecycle including expiry and refunds. Refunds are recorded in the domain; no outbound call to a payment provider is made. |
| Contrast and comparison material | 8 / 10 | Rosetta Stone and the controller comparison. |
| Accessibility for less experienced learners | 4 / 10 | High project count and infrastructure density; needs a guided path. |
| Onboarding | 7 / 10 | `docker compose up` for Postgres, Redis and the Aspire dashboard, then `dotnet run`; automatic migrations in Development. The compose file does not run the API. |

---

## 7. Remaining gaps

These are the genuine limitations of the current code. [PLATFORM_SHORTCOMINGS.md](PLATFORM_SHORTCOMINGS.md) and [TRADE_OFFS_AND_LIMITATIONS.md](TRADE_OFFS_AND_LIMITATIONS.md) cover them in more depth.

1. **Connection budget.** Each module owns its schema and connects as its own role ([data-ownership.md](data-ownership.md)), so each replica holds one pool per module (46 connections); a second replica needs PgBouncer or a larger `max_connections`.
2. **No broker consumer.** `RabbitMqMessageBus` publishes CloudEvents but nothing consumes them. Cross-process event delivery is not implemented; the in-process bus is the working mode.
3. **Per-instance mechanisms.** The outbox wake-up signal and the rate limiter are in memory. Other replicas rely on the 5-second outbox sweep; there is no distributed rate limiter.
4. **Unsubscribe link.** One-click unsubscribe trusts a bare user id; it needs a signed, expiring token.
5. **Key encryption key rotation.** Signing keys are encrypted with a single configured key; rotation is not implemented.
6. **Exactly-once side effects.** E-mail and metering rely on provider idempotency keys, not the inbox, so a crash between the external call and the commit can repeat them. Outbound webhooks are at-least-once; receivers must deduplicate.
7. **Query validation.** The validation pipeline runs for commands only.
8. **Logs outside OpenTelemetry.** Traces and metrics are exported over OTLP; logs stay on Serilog.
9. **No interactive client.** Scalar and the `.http` file cover API exploration; there is no UI that shows, for example, SignalR funding updates.

---

## 8. Evaluation against the stated intent: a monolith designed for decomposition

The course states its purpose as teaching how to design a monolith that can later be split into services. Judged by that goal, most of the structure in section 3 is scaffolding for extraction rather than accidental complexity.

### 8.1 Decomposition roadblocks and their status

| Roadblock (first evaluation) | Status now |
|---|---|
| Contributions called Campaigns synchronously (`ICampaignContributionAvailabilityReader`) to check a campaign before accepting a pledge | **Resolved.** The reader no longer exists. Contributions keeps a replicated read model (`active_campaigns_cache`, entity `ActiveCampaignCache`) updated by `ReplicatedCampaignEventHandlers` from the campaign created, published, cancelled, succeeded and failed events. `MakeContributionCommandHandler` checks its own table. `ReplicatedReadModelBoundaryTests` guard this. |
| One central outbox poller in the API host dispatched straight to DI (`OutboxProcessorBackgroundService`) | **Resolved.** That class no longer exists. Campaigns, Contributions and Moderation each run `ModuleOutboxProcessor<TDbContext>` and publish through `IMessageBus`, selected by `Messaging:Provider` (`InProcess` by default, or `RabbitMQ`). The broker side is publish-only (section 7). |
| Campaigns reads Moderation synchronously when publishing (`ICampaignReviewStatusReader`, defined in `Moderation.Contracts`) | **Open.** `PublishCampaignCommandHandler` checks that the review is approved inside the publish transaction. In process this is a method call; once Moderation is extracted it becomes a network call on the publish path. The fix is the same replicated read model technique, fed by `CampaignReviewApproved` and `CampaignReviewRejected` events. |
| All modules on one connection string and one pool | **Resolved in configuration.** Each module reads `ConnectionStrings:{Module}Db` (for example `CampaignsDb`) through `GetRequiredModuleConnectionString`, falling back to `DefaultConnection`. With the default configuration they still share one database and, because the strings are identical, one Npgsql pool. |
| Every module depends on shared BuildingBlocks | **Open by design.** BuildingBlocks holds generic mechanisms (dispatching, outbox, inbox, errors, value objects such as `Money`), not domain concepts. An extracted service would take them as a versioned package. |

### 8.2 Extraction readiness by module

| Module | Data coupling | Runtime dependencies | Readiness | Work left before extraction |
|---|---|---|---|---|
| Identity | Own tables | None; issues tokens, publishes JWKS | High | New host; other services already validate tokens through JWKS. |
| Moderation | Own tables | Consumes `CampaignCreated`; publishes review outcomes; is read synchronously by Campaigns on publish | Medium-high | A broker consumer for `CampaignCreated`, and removal of the synchronous review-status read in Campaigns. A proof of concept exists (8.3). |
| CampaignUpdates | Own tables, `CampaignOwnerCache` read model | Consumes campaign and contribution events | High | Broker consumers. |
| Notifications | Own tables, `CampaignTitleCache` read model | Consumes events | High | Broker consumers. |
| Campaigns | Own tables and ledger | Consumes contribution confirmed and refunded events to update the ledger; reads Moderation's review status on publish | Medium | Broker consumers; a local review-status read model; the ledger must stay idempotent under redelivery (it is keyed by contribution id and kind). |
| Contributions | Own tables, `active_campaigns_cache` | Consumes campaign events; receives payment webhooks | Medium | Broker consumers; webhook routing at the edge. |

### 8.3 Extraction is the next course step

On this branch Moderation is an in-process module. Microservice extraction is the upcoming course step. A working proof of concept (a standalone Moderation host, JWKS-validated authentication, a Strangler Fig docker-compose topology and extraction boundary tests) is preserved on the branch `future/microservices-extraction`. The plan for that step is [deconstruction-walkthrough.md](../educational/03-engineering-spec-and-roadmap/deconstruction-walkthrough.md).

### 8.4 Decomposition checklist

- [x] No cross-module references except to `Contracts`, enforced by architecture tests.
- [x] Contracts projects carry commands, queries, events and DTOs between modules.
- [x] One `DbContext`, one set of tables and one migration history per module; no cross-module foreign keys.
- [x] Per-module connection strings.
- [x] Transactional outbox per publishing module, idempotent inbox for consumers that must run once.
- [x] Replicated read models instead of synchronous cross-module queries on the pledge path (Contributions) and in Notifications and CampaignUpdates.
- [x] Asymmetric ES256 tokens with a JWKS endpoint.
- [x] Pluggable message bus abstraction (`IMessageBus`).
- [ ] The same for campaign publishing, which still reads Moderation's review status synchronously.
- [x] Tables moved into per-module schemas.
- [x] Per-module database roles, with migrations run by a separate owner role.
- [ ] A broker consumer, so events can cross a process boundary.
- [ ] An extracted module on the main line of the course (proof of concept on `future/microservices-extraction`).

### 8.5 Verdict for this intent

For teaching how to build a monolith that can be decomposed, the codebase is a strong reference. The structure that looks heavy in an introductory course is what makes the extraction plan mostly a hosting and messaging task rather than a rewrite. The work that remains (a broker consumer, a replicated review status in Campaigns, an edge gateway) is exactly the content of the next course step.
