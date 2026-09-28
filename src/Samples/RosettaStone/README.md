# The Rosetta Stone: Three Tiers, One Business Requirement

Three implementations of the exact same use case — **create a campaign with a title, a story,
and a target amount** — so a reader can compare them without imagining anything. Tiers 1 and 2
live in this project, which is its own runnable host; Tier 3 is the production endpoint of
`CrowdFunding.API`. The production API does not reference this sample (an architecture test,
`ApiHostDependencyTests`, enforces it): teaching code never ships in the production host.

Run the sample (it applies its own `rosetta` migrations on startup) and open its API reference:

```bash
docker compose up -d                                  # Postgres
dotnet run --project src/Samples/RosettaStone         # http://localhost:5090/scalar/v1
dotnet run --project src/API/CrowdFunding.API         # Tier 3, see the root README
```

The connection string comes from `Properties/launchSettings.json`
(`ConnectionStrings__DefaultConnection`); set that environment variable yourself when running
outside the launch profile.

| Tier | Endpoint | Folder |
| :--- | :--- | :--- |
| 1 — Minimal API CRUD | `POST /rosetta/v1/campaigns/tier1-minimal-api` | [`01-MinimalApiCrud/`](./01-MinimalApiCrud/) |
| 2 — Pragmatic CQRS | `POST /rosetta/v1/campaigns/tier2-pragmatic-cqrs` | [`02-PragmaticCqrs/`](./02-PragmaticCqrs/) |
| 3 — Rich DDD + Outbox | `POST /api/campaigns` (the real, production endpoint) | [`03-RichDomainModel/`](./03-RichDomainModel/) |

Send the same payload to any of them:

```json
{ "title": "Test", "story": "A valid story of at least twenty characters.", "targetAmount": 1000, "currency": "USD" }
```

Send an invalid one (blank title, or a non-positive `targetAmount`) to any of them and all three
answer with the same shape: an RFC 9457 `application/problem+json` validation-problem body —
`{ "type", "title", "status": 400, "errors": { "<field>": ["<message>"] } }` — because Tier 1
and Tier 2 both call `Results.ValidationProblem(...)`, the identical machinery ASP.NET Core's
production API returns for Tier 3 when its `ValidationPipelineBehavior` rejects the command. See
`tests/IntegrationTests/CrowdFunding.IntegrationTests/RosettaStoneParityTests.cs`, which boots
both hosts against one real Postgres instance and asserts this across all three tiers.

## Project layout

- `Program.cs`: The sample's own host (Scalar UI, migrations on startup, the Rosetta endpoints).
- `RosettaStoneDependencyInjection.cs`: Registers the `RosettaStoneDbContext`, the Tier 2 handler and the validator.
- `RosettaStoneEndpoints.cs`: Maps Tier 1 and Tier 2 under `/rosetta/v1/campaigns` with one OpenAPI tag.
- `RosettaValidationProblem.cs`: Turns a FluentValidation result into the same `Results.ValidationProblem` body Tier 3 returns.
- `01-MinimalApiCrud/`, `02-PragmaticCqrs/`, `03-RichDomainModel/`: The three tiers (Tier 3's folder is a README that points at the production code).
- `Persistence/`: `CampaignRecord`, `RosettaStoneDbContext` (schema `rosetta`) and its migrations.
- `Properties/launchSettings.json`: The launch profile (`http://localhost:5090`) and its connection string.

## The scorecard

Real numbers from this codebase, not estimates — Tier 1 and Tier 2 are `wc -l` on the files in
this project; Tier 3 is every file actually touched by `CreateCampaignCommandHandler`'s call
graph today.

| Dimension | Tier 1: Minimal API | Tier 2: Pragmatic CQRS | Tier 3: Rich DDD + Outbox |
| :--- | :---: | :---: | :---: |
| **Files touched** | 1 | 3 | 14 |
| **Lines of code** | 49 | 92 | 960 total (see note) |
| **Assemblies involved** | 1 (`Samples.RosettaStone`) | 2 (`Samples.RosettaStone` + `BuildingBlocks.Application`) | 5 |
| **Persistence model** | Flat record, direct `DbSet.Add` | Flat record, direct `DbSet.Add` via handler | Aggregate root, private setters, invariants enforced in the constructor/factory |
| **Validation** | Manual `if` | `FluentValidation`, called explicitly by the endpoint | `FluentValidation`, run by the command pipeline (`ValidationPipelineBehavior`) |
| **Decomposition effort** | Zero — owns its own `rosetta` schema already | Zero — owns its own `rosetta` schema already | Low by design — contracts, events, and the transaction boundary are already module-owned; extracting a module into its own service is the next course step and is not implemented on this branch |
| **Audit / outbox guarantee** | None — a lost write after `SaveChangesAsync` is just gone | None — same as Tier 1 | Guaranteed at-least-once delivery: `CampaignCreatedDomainEvent` → outbox row committed in the *same* transaction as the row it describes |
| **Concurrency defense** | None needed — single-writer insert, no shared mutable state | None needed — same reason | `pg_advisory_xact_lock` per campaign plus `xmin` optimistic concurrency as a safety net on later commands (`AddContributionToCampaign`, `Publish`, `Cancel`) that mutate this same row; the funding total is an append-only ledger checked by database constraints |
| **When to choose this tier** | Config, lookups, prototypes, anything with no downstream consumers | Standard CRUD with validation but no cross-module consequences | Financial ledgers, multi-step lifecycles, anything another module's outbox subscriber depends on |

**Note on the 960-line Tier 3 total** (measured when Tier 3's entry point was an MVC controller;
it is now a Minimal API, see `03-RichDomainModel/README.md`): the endpoint, the `Campaign` aggregate, the repository,
and the transaction executor are *shared* across every Campaigns write use case (Publish,
Cancel, AddContributionToCampaign, ...), not exclusive to `CreateCampaign`. The 14-file, 960-line total is
what a reader has to open and understand to trace one request end to end — that cognitive cost
is real and is exactly what this ticket exists to make visible — but the code you'd actually
*write* for one more command in an already-justified Tier 3 module is much smaller than 960
lines, because most of that total is infrastructure the first command already paid for. See
[`03-RichDomainModel/README.md`](./03-RichDomainModel/README.md) for the file-by-file
breakdown.

## Choosing a tier without dogma

Ask two questions about the write, not about "what pattern does this team use":

1. **Does any other module need to react to this write, reliably, even across a crash?**
   If yes, you need the outbox — which in this codebase means the Tier 3 pipeline (or at
   minimum a hand-rolled outbox write in a Tier 2-shaped handler). If no, skip it.
2. **Can two concurrent requests corrupt this row in a way that costs real money or breaks an
   invariant a human will notice?** If yes, you need optimistic concurrency and probably an
   aggregate that refuses to construct itself in an invalid state — Tier 3. If the write is a
   single-writer insert with no read-modify-write race, you don't.

A "yes" to either question is what Tier 3 is *for* — it is not the default, and a codebase where
every table insert answers "yes" to both questions has a design problem, not a discipline
problem. A "no" to both is what Tier 1 or Tier 2 are for, and reaching for Tier 3 anyway is the
architectural dogmatism this sample exists to make expensive and visible.

See also: [`educational/01-architecture-paradigms/poly-pattern-architecture.md`](../../../educational/01-architecture-paradigms/poly-pattern-architecture.md)
for the narrative version of this argument.
