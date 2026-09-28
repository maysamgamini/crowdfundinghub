# Changelog

## 2026-09-28: Data ownership, one schema and one database role per module

Every module now owns one PostgreSQL schema instead of sharing `public`, and connects as its own database role that can use only that schema, so PostgreSQL itself refuses cross-module queries. The course material for the change is [`docs/data-ownership.md`](docs/data-ownership.md): why code-only separation is not enough, the Campaigns to Moderation review-status read as the cross-module worked example, the per-module roles, the owner role and the connection-pool math, and how a schema per module becomes a database per service.

### Changed

- **Schema per module.** Each module DbContext declares `Schema` and calls `HasDefaultSchema(Schema)`; the migrations history table uses the same constant. Schemas: `campaigns`, `contributions`, `identity`, `moderation`, `notifications`, `campaignupdates` (the name the CampaignUpdates history table already used). Outbox, dead-letter and inbox tables move with their module. `system.audit_records` stays in the shared `system` schema with one owner (`AuditDbContext`) and no readers.
- **Raw SQL is schema-qualified.** The outbox claim reads its table from the EF model through the new [`QualifiedTableName`](src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Persistence/QualifiedTableName.cs) (module outbox processors no longer pass a table name), the inbox claim uses the same helper, and the ledger and webhook-claim statements name `campaigns.` and `campaignupdates.` explicitly.
- **Tests.** [`ModuleSchemaOwnershipTests`](tests/ArchitectureTests/CrowdFunding.ArchitectureTests/ModuleSchemaOwnershipTests.cs) fails the build when a module has no schema of its own, shares one, or maps a table outside it. [`ModuleSchemaMigrationTests`](tests/IntegrationTests/CrowdFunding.IntegrationTests/ModuleSchemaMigrationTests.cs) upgrades a database holding data in `public` and proves rows, ledger triggers and outbox delivery survive.
- **Per-module database roles.** [`db/init/01-module-roles.sql`](db/init/01-module-roles.sql) creates `<schema>_app` roles with `USAGE` and DML on their own schema only (no DDL, `TRUNCATE`, trigger control or ownership, so the ledger triggers cannot be bypassed), `audit_app` with `SELECT, INSERT` on `system.audit_records` only, and the `crowdfunding_migrator` owner role; default privileges cover tables added later. Docker compose runs it on a new volume, with development passwords from `02-dev-passwords.sql`. `MigrationRunner` runs migrations with the credentials of `ConnectionStrings:Migrations` on each module's own connection. Every module string sets `Maximum Pool Size` (46 connections per replica in total).
- **Role tests.** The shared integration-test factory provisions the roles and connects every module as its role, so the whole suite runs under enforcement. [`ModuleDatabaseRoleTests`](tests/IntegrationTests/CrowdFunding.IntegrationTests/ModuleDatabaseRoleTests.cs) proves each role can work in its schema and is refused everywhere else.

### Configuration required

| Setting | Required | Notes |
| :--- | :--- | :--- |
| `ConnectionStrings:{Module}Db` (`CampaignsDb`, `ContributionsDb`, `IdentityDb`, `ModerationDb`, `NotificationsDb`, `CampaignUpdatesDb`, `AuditDb`) | Yes, to enforce ownership | Each module's role and pool ceiling. Without it a module falls back to `DefaultConnection`. |
| `ConnectionStrings:Migrations` | For the migration job | The owner role's credentials; only the user name and password are used. |
| Role passwords | Yes, outside development | Run `db/init/01-module-roles.sql` once per database as a superuser, then set passwords from the secret store. |

### Database migrations

- `MoveTablesToModuleSchema` in each of the six modules: `ALTER TABLE public.x SET SCHEMA <module>` for every table the module owns. A catalog change: no rows are copied; indexes, constraints and triggers move with their tables. Each statement briefly takes an exclusive lock, so run it while the application is idle.
- The Campaigns migration also moves the ledger trigger functions into `campaigns` and recreates `campaigns_raised_amount_matches_ledger()` with a schema-qualified table name, because PL/pgSQL resolves table names through `search_path` at run time.
- `GrantModuleRole` in each of the six modules: grants the module's role DML on the tables moved in from `public` (default privileges apply only to tables created in the schema) and revokes its rights on the migrations history. Skipped when the role does not exist. The roles script runs before `migrate` on a new database; on an existing one it can run before or after, because it hands the tables the old owner created (in `public` or already moved) to the migrator.

## 2026-09-28: Production hardening for the Clean Architecture course

This release prepares the codebase to be the reference for a C# Clean Architecture course for senior engineers. Several techniques were ported from the sibling HiSpeedAlgo backend (`apps/backend` in the parent repository) where it was ahead; others fix gaps specific to this codebase. Each item links to the code and the document that explains it.

### Ported from apps/backend, and why

| Technique | Backend source | Here | Why |
| :--- | :--- | :--- | :--- |
| Inbox: `IdempotentConsumer` + `ProcessedEvent` | `BuildingBlocks.Infrastructure/Events/IdempotentConsumer.cs` | [`Inbox/EfIdempotentConsumer.cs`](src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Inbox/EfIdempotentConsumer.cs), per-module `*_processed_events` tables | The outbox retries a whole event when any handler fails, so non-idempotent handlers (the pledge webhook fan-out) ran twice. Improved on the original: the claim is `INSERT … ON CONFLICT DO NOTHING` inside the effect's transaction, so there is no check-then-insert race. |
| Outbox wake-up: `ChannelOutboxSignal` | `BuildingBlocks.Infrastructure/Outbox/ChannelOutboxSignal.cs` | [`Outbox/OutboxSignal.cs`](src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Outbox/OutboxSignal.cs), [`OutboxCommitSignalInterceptor.cs`](src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Outbox/OutboxCommitSignalInterceptor.cs) | Cross-module reactions waited up to one 5 s poll. Now a commit that writes outbox rows wakes that module's processor; the poll is the fallback sweep. One signal per module DbContext because every module runs its own processor. |
| OpenTelemetry setup | `API/Observability/OpenTelemetryConfiguration.cs` | [`Observability/OpenTelemetryConfiguration.cs`](src/API/CrowdFunding.API/Observability/OpenTelemetryConfiguration.cs) | An `ActivitySource` existed but no SDK subscribed to it. Traces and metrics now export over OTLP (Aspire dashboard in `docker-compose.yml`), plus Npgsql instrumentation. |
| Rate limiting with 429 ProblemDetails | `API/Security/RateLimitingExtensions.cs` | [`RateLimiting/RateLimitingConfiguration.cs`](src/API/CrowdFunding.API/RateLimiting/RateLimitingConfiguration.cs) | Rejections had no body; per-user partitioning never worked because the limiter ran before authentication. |
| AES-256-GCM encryption of signing keys at rest | `BuildingBlocks.Infrastructure/Security/SigningKeyEncryption.cs` | [`SigningKeyProtector.cs`](src/Modules/Identity/CrowdFunding.Modules.Identity.Infrastructure/Services/SigningKeyProtector.cs) | Anyone with read access to the database could forge tokens. Hardened on the way in: the key must be configured (no built-in fallback) and each ciphertext is bound to its `kid`. |
| Database overload mapped to 503 | `API/Diagnostics/GlobalExceptionHandler.cs` | [`ErrorHandling/ExceptionClassifier.cs`](src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/ErrorHandling/ExceptionClassifier.cs) | Npgsql pool exhaustion is transient; clients get `Retry-After` instead of a 500. |
| Built-in OpenAPI + Scalar | `API/Program.cs` | [`Documentation/OpenApiConfiguration.cs`](src/API/CrowdFunding.API/Documentation/OpenApiConfiguration.cs) | .NET 10 generates OpenAPI 3.1 natively; Swashbuckle removed. |
| Append-only ledger with a unique idempotency key and a snapshot (`CoinLedgerEntry`, `EfEconomyStore`) | `Modules/Economy` | [`docs/append-only-ledger.md`](docs/append-only-ledger.md) | `Campaign.RaisedAmount` was a mutable counter guarded by code; the database now enforces that it equals the sum of an append-only ledger. |
| Package versions and central package management | `*.csproj` | [`Directory.Packages.props`](Directory.Packages.props) | Versions had drifted across 30 projects; NU1903 (known vulnerability) was suppressed. |

Deliberately not ported: the backend's single in-process bus and Redpanda choice, its outbox job (this codebase's `FOR UPDATE SKIP LOCKED` claim, attempts and dead-letter table are ahead), and its `Result` types.

### Changed

- **Errors.** Domain rules throw `DomainException` subtypes (`BusinessRuleViolationException` 400, `DomainConflictException` 409); handlers throw `NotFoundException`, `UnauthenticatedException`, `ForbiddenAccessException`. Only owned exception types become 4xx; framework, EF Core and Npgsql messages are never returned. State conflicts such as reserving a sold-out reward tier now answer 409 instead of 400.
- **Validation.** `ValidationPipelineBehavior` runs FluentValidation for every dispatched command; hand-written validation in endpoints is gone.
- **Minimal APIs.** Every endpoint is a Minimal API in `src/API/CrowdFunding.API/Endpoints/`; `ModerationController` is kept on purpose for comparison ([`docs/minimal-apis-vs-controllers.md`](docs/minimal-apis-vs-controllers.md)). Mapster removed in favor of explicit mapping. URLs are unchanged.
- **Funding ledger.** `campaign_ledger_entries` replaces the contribution dedupe table: pledges and compensating refund entries, DB-enforced idempotency, append-only trigger, and a trigger that keeps `campaigns.raised_amount = SUM(entries)`. Refunds now reduce the raised amount. A pledge confirmed after cancellation is not credited (and no longer dead-letters).
- **Outbox.** Rows a crashed worker left in `Processing` are reclaimed when their lease expires; a failed processor pass no longer stops the host.
- **Identity.** Access tokens carry `sub` as the only identity claim and hosts validate with inbound claim mapping off.
- **Edge.** Config-driven CORS, trusted forwarded headers, a global per-caller rate limit.
- **Modules.** CampaignUpdates and Notifications gained `Contracts` projects; every aggregate root uses `BaseEntity`; convention-based architecture tests cover all modules.
- **Samples.** The Rosetta Stone sample runs as its own host; the production API no longer references it.
- **Moderation.** Moderation runs as an in-process module. Microservice extraction is the upcoming course step; the proof-of-concept is preserved on branch `future/microservices-extraction`.
- **Build.** Central package management, backend-aligned versions, Testcontainers 4.14.

### Configuration required

| Setting | Required | Notes |
| :--- | :--- | :--- |
| `SigningKeyEncryption:Key` (`SigningKeyEncryption__Key`) | Yes, in every environment | Base64-encoded 32-byte key from a secret store (`openssl rand -base64 32`). Startup fails without it. `appsettings.Development.json` holds a development-only key. Existing plaintext signing keys are re-encrypted on the first start. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | No | Enables OTLP export (Development: `http://localhost:4317`). Standard `OTEL_*` variables apply. |
| `Cors:AllowedOrigins` | No | Browser origins allowed to call the API; empty means none. |
| `ForwardedHeaders:KnownProxies`, `ForwardedHeaders:KnownNetworks` | Behind a reverse proxy | Proxies trusted to report the client IP and scheme. |
| `RateLimiting:Global:PermitLimit`, `WindowSeconds` | No | Default 300 requests per 60 s per user (or IP). |

### Database migrations

- Contributions `AddInboxProcessedEvents`: creates `contributions_processed_events`, copies `processed_payment_webhooks` into it, drops the old table.
- CampaignUpdates `AddInboxProcessedEvents`: creates `campaign_updates_processed_events`.
- Identity `EncryptSigningKeysAtRest`: renames the private-key column; rows are re-encrypted at startup.
- Campaigns `AppendOnlyCampaignLedger`: creates `campaign_ledger_entries`, backfills pledges and opening balances so every campaign satisfies `raised_amount = SUM(entries)`, drops `campaign_contributions_ledger`, installs the append-only and snapshot triggers.
