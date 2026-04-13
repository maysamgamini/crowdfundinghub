# Developer Guidelines

## Working Agreement
- Keep controllers thin. HTTP concerns stay in the API layer; business behavior belongs in application or domain code.
- Keep domain projects free of infrastructure dependencies.
- Prefer explicit command, query, and event models over passing EF Core entities across layers.
- Add or update folder `README.md` files when you introduce new structural areas.
- Add XML summaries to new public types so the code stays self-documenting.

## Layer Responsibilities
### API
The API project owns transport concerns: routing, validation responses, authentication, authorization policies, and mapping between HTTP contracts and application models.

### Application
Application projects orchestrate use cases. Handlers should:
- enforce access rules through `ICurrentUser`
- call repositories or read services through abstractions
- use transaction executors for state-changing workflows
- raise outcomes through domain or application events instead of directly calling other modules

### Domain
Domain projects own invariants and state transitions. Aggregates should validate their own inputs and raise domain events when meaningful business changes occur.

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
When a module needs to react to another module, prefer an application event contract plus an event handler. The outbox pattern in this repo is the delivery mechanism for those reactions.

## Persistence and Migrations
- Use EF Core configurations in each module's `Persistence/Configurations` folder.
- Store migrations inside the owning infrastructure project.
- Treat migration designer files as generated artifacts.
- Keep outbox mapping enabled for aggregates that emit domain events.

## Security
- Register new permissions in `PermissionConstants` and, when relevant, map them through `RolePermissionCatalog`.
- Use policies in controllers and explicit current-user checks in handlers when ownership or contextual authorization matters.
- Keep JWT-related changes isolated to the identity module and API startup unless the contract itself changes.

## Testing Expectations
- Add unit tests for new domain rules and handler orchestration.
- Update architecture tests if you intentionally change dependency boundaries.
- Add integration tests for cross-module or infrastructure-heavy behavior.
- Prefer fakes in unit tests and reserve database-backed assertions for integration scenarios.

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

