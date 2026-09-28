# The Over-Engineering Audit: Where the Design Goes Further Than It Must

> **Curriculum focus:** accidental complexity and the cost of indirection
> **Key lesson:** every layer of indirection must pay for itself with a concrete benefit. When it stops paying, remove it.
> **Status:** revised 2026-09-28. Section 1 records what was removed; the rest describes the ceremony that remains and why.

---

## 1. Ceremony removed

An earlier audit of this codebase listed several layers that added work without adding guarantees. They are gone:

| Removed | Replaced by | Why the replacement is enough |
|---|---|---|
| Mapster mapping profiles between API contracts and commands | Explicit construction in the endpoint (`new CreateCampaignCommand(request.Title, ...)`) | The mappings were one-to-one. Explicit code is shorter, visible in the debugger and checked by the compiler. |
| Hand-written validation calls in each controller action | `ValidationPipelineBehavior` in `BuildingBlocks.Application/Validation` | Runs every FluentValidation validator for a command once, before the handler, as the outermost behavior. Endpoints never validate. |
| MVC controllers for every route | Minimal APIs in `src/API/CrowdFunding.API/Endpoints/`, using `TypedResults` and named routes for `CreatedAtRoute` | Less framework surface per route. One controller, `ModerationController`, is kept on purpose for comparison; see [minimal-apis-vs-controllers.md](../../docs/minimal-apis-vs-controllers.md). |
| Swashbuckle | Built-in `Microsoft.AspNetCore.OpenApi` (`/openapi/v1.json`) with Scalar UI, Development and Testing only | The framework's own generator; one less third-party dependency. |
| One central outbox poller in the API host | One `ModuleOutboxProcessor<TDbContext>` per publishing module, woken by a commit signal | Each module owns its outbox; see [outbox-architecture.md](../../docs/outbox-architecture.md). |

---

## 2. The create-campaign pipeline today

`POST /api/campaigns` still touches about fourteen files across five projects:

```
1.  API/Endpoints/CampaignEndpoints.cs                          (route, explicit mapping)
2.  API/Contracts/Campaigns/CreateCampaignRequest.cs            (API contract)
3.  BuildingBlocks.Application/Messaging/ICommandDispatcher.cs  (dispatcher)
4.  BuildingBlocks.Application/Validation/ValidationPipelineBehavior.cs
5.  Campaigns.Application/.../CreateCampaign/CreateCampaignCommand.cs
6.  Campaigns.Application/.../CreateCampaign/CreateCampaignCommandValidator.cs
7.  Campaigns.Application/.../CreateCampaign/CreateCampaignCommandHandler.cs
8.  Campaigns.Application/Abstractions/Persistence/ICampaignRepository.cs
9.  Campaigns.Domain/Aggregates/Campaign.cs                     (Campaign.Create)
10. Campaigns.Domain/Events/CampaignCreatedDomainEvent.cs
11. Campaigns.Infrastructure/Persistence/Repositories/CampaignRepository.cs
12. Campaigns.Infrastructure/Transactions/CampaignTransactionExecutor.cs  (domain event -> outbox row)
13. Campaigns.Infrastructure/Persistence/DbContexts/CampaignsDbContext.cs
14. BuildingBlocks.Infrastructure/Persistence/OutboxMessage.cs
```

The same use case in the Rosetta Stone's first tier is one file of about fifty lines ([`src/Samples/RosettaStone`](../../src/Samples/RosettaStone/README.md)). Put the two side by side when teaching.

### Remaining ceremony

**Command dispatching.** The endpoint sends a command through `ICommandDispatcher` rather than calling the handler. The dispatcher now earns part of its cost: it is where the validation pipeline and the audit behavior run, so cross-cutting policy is applied in one place. For a single create with no cross-cutting needs it would be indirection.

**Request and command duplication.** `CreateCampaignRequest` and `CreateCampaignCommand` carry the same six fields (`Title`, `Story`, `Category`, `GoalAmount`, `Currency`, `DeadlineUtc`). The aggregate is not a third copy: `Campaign.Create` also takes the owner and the current time, wraps the goal in `Money` and enforces invariants. The two records let the HTTP contract evolve separately from the application layer. Whether that is worth two types per use case is a legitimate team decision.

**Outbox on create.** Earlier versions of this audit called the outbox "dead weight" because only Moderation listened to `CampaignCreated`. That is no longer true: four modules consume it (Moderation opens a review; Contributions and Notifications update replicated read models; CampaignUpdates records activity). The outbox makes publication atomic with the insert and gives at-least-once delivery after a crash, even in process. For an event with no consumer it would still be unnecessary.

---

## 3. Hand-built ES256 signing and JWKS

Identity generates P-256 keys, stores them in PostgreSQL ([`EfSigningKeyStore.cs`](../../src/Modules/Identity/CrowdFunding.Modules.Identity.Infrastructure/Services/EfSigningKeyStore.cs)), encrypts the private keys at rest with AES-256-GCM ([`SigningKeyProtector.cs`](../../src/Modules/Identity/CrowdFunding.Modules.Identity.Infrastructure/Services/SigningKeyProtector.cs)) and serves the public set from [`JwksEndpoint.cs`](../../src/API/CrowdFunding.API/Security/JwksEndpoint.cs).

**Why it looks excessive.** Production systems usually delegate key generation, rotation and JWKS publishing to an identity provider (Keycloak, Duende IdentityServer, Cognito, Auth0). Debugging elliptic-curve parameters is not a Clean Architecture lesson.

**Why it stays.** Asymmetric tokens with a JWKS endpoint let any other process validate tokens without a shared secret or a call to Identity. That property is what makes module extraction possible without an auth rewrite. Teach it as a separate topic, after the module structure.

---

## 4. Four projects per module

Six modules with four projects each, plus the API host and three BuildingBlocks projects, give 28 production projects.

**The cost.** More projects to build and navigate, and a placement decision for every new type (`Contracts`, `Application` or `BuildingBlocks`?).

**The benefit.** Boundaries become facts about compiled assembly references, which the architecture tests check by reflection: `Domain` cannot reference EF Core, and one module cannot reference another's internals. A single project per module with `internal` types would give most of the isolation between modules but not the rule that the domain stays free of infrastructure.

---

## 5. Summary

```
The full pipeline is not justified for:
- lookups and simple CRUD with no invariants
- single-row updates without contention
- events nobody consumes

The full pipeline is justified for:
- money movements (pledges, ledger entries, refunds)
- operations serialized by advisory locks
- state changes that other modules must learn about reliably
```
