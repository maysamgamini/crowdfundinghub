# Developer Guidelines

## Working Agreement
- Keep endpoints thin. HTTP concerns stay in the API layer (Minimal APIs in `src/API/CrowdFunding.API/Endpoints/`); business behavior belongs in application or domain code. `Controllers/ModerationController.cs` is the one controller kept on purpose for comparison; do not add controllers (see `docs/minimal-apis-vs-controllers.md`).
- Keep domain projects free of infrastructure dependencies.
- Prefer explicit command, query, and event models over passing EF Core entities across layers.
- Add or update folder `README.md` files when you introduce new structural areas.
- Add XML summaries to new public types so the code stays self-documenting.

## Layer Responsibilities
### API
The API project owns transport concerns: routing, authentication, authorization policies, rate limiting, CORS, OpenAPI, turning exceptions into RFC 9457 ProblemDetails (`Observability/GlobalExceptionHandler.cs`), and explicit mapping between HTTP contracts and application models. Endpoints do not validate: every command passes through `ValidationPipelineBehavior`, which runs the registered FluentValidation validators and returns a 400 with per-property `errors`.

### Application
Application projects orchestrate use cases. Handlers should:
- enforce access rules through `ICurrentUser` (it reads only the JWT `sub` claim) and throw `UnauthenticatedException`, `ForbiddenAccessException` or `NotFoundException` rather than BCL exception types
- call repositories or read services through abstractions
- use transaction executors for state-changing workflows (take the aggregate's advisory lock and load the aggregate inside it when concurrent writers must be serialized)
- wrap non-idempotent effects of event handlers in the module's `IIdempotentConsumer` (the inbox), keyed by the event's `EventId`
- raise outcomes through domain or application events instead of directly calling other modules

### Domain
Domain projects own invariants and state transitions. Aggregates validate their own inputs and raise domain events when meaningful business changes occur. Reject an operation with a `DomainException` subtype from `BuildingBlocks.Domain`: `BusinessRuleViolationException` for invalid input (400) or `DomainConflictException` when the aggregate's current state forbids the operation (409). Never throw `InvalidOperationException` or `ArgumentException` for a business outcome: the API maps only exception types this codebase owns to 4xx and turns everything else into a generic 500.

### Infrastructure
Infrastructure projects implement repositories, EF Core contexts, entity configurations, transaction executors, and environment-specific services such as password hashing or JWT generation.

## Module Design Rules
- Prefer module-to-module communication through contract projects and application events.
- Avoid direct API-to-domain references. The architecture tests already guard this pattern for the main modules.
- Keep read services query-focused and side-effect free.
- Use one DbContext per module to preserve boundaries.

## Commands, Queries, and Events
### Commands
Create a dedicated folder for each command use case. Keep the command, handler, validator, and result together so the behavior stays easy to navigate.

### Queries
Keep query handlers free of mutation. Use read services for projection-heavy access instead of loading aggregates just to read data.

### Events
When a module needs to react to another module, prefer an application event contract (in the publisher's `Contracts` project, deriving from `BaseApplicationEvent`) plus an event handler. The transactional outbox delivers those events at least once, so every handler must be idempotent: naturally (upserts, state-machine guards, database unique keys) or through the module's `IIdempotentConsumer`.

## Packages
Versions live only in `Directory.Packages.props` (central package management). Reference packages without a `Version` attribute, and do not suppress NuGet vulnerability warnings.

## Persistence and Migrations
- Use EF Core configurations in each module's `Persistence/Configurations` folder.
- Store migrations inside the owning infrastructure project.
- Treat migration designer files as generated artifacts.
- Keep outbox mapping enabled for aggregates that emit domain events.
- Prefer invariants the database enforces (unique keys, CHECK constraints, triggers) for money and other state that must never drift; see `docs/append-only-ledger.md`. Hand-write the SQL parts of such migrations and cover them with integration tests against PostgreSQL.
- Module tables currently share the `public` schema; name new tables with the module prefix convention used by the module (for example `contributions_processed_events`).

## Security
- Register new permissions in `PermissionConstants` and, when relevant, map them through `RolePermissionCatalog`.
- Use `RequireAuthorization(PermissionConstants.X)` on endpoints and explicit current-user checks in handlers when ownership or contextual authorization matters.
- Secrets (JWT signing-key encryption key, webhook secrets) come from configuration or environment variables, never from source; startup must fail when a required secret is missing.
- Keep JWT-related changes isolated to the identity module and API startup unless the contract itself changes.

## Testing Expectations
- Add unit tests for new domain rules and handler orchestration.
- Update architecture tests if you intentionally change dependency boundaries. `ModuleBoundaryConventionTests` already cover every module by convention (four layers, cross-module references only through `Contracts`).
- Add integration tests for cross-module or infrastructure-heavy behavior.
- Prefer fakes in unit tests and reserve database-backed assertions for integration scenarios.
- Before pushing, run what CI runs: `dotnet format CrowdFunding.slnx --verify-no-changes`, `dotnet build CrowdFunding.slnx -c Release /warnaserror`, then `dotnet test CrowdFunding.slnx` (Docker is required for Testcontainers).

## Documentation Expectations
- Update the nearest folder `README.md` whenever a folder's purpose changes.
- Keep top-level docs aligned with actual runtime behavior, dependency setup, and local commands.
- Document new public classes, records, interfaces, and enums with XML summaries.

## Recommended Workflow
1. Start with the domain rule or contract change.
2. Implement the application handler or query logic.
3. Wire infrastructure dependencies and mappings.
4. Add or update tests.
5. Refresh folder docs and type summaries for the changed areas.

