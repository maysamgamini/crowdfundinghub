# Functional Business Modules

## Architecture Overview
The platform is organized as a **modular monolith** divided into six distinct bounded contexts. Each module maintains strict encapsulation, owning its own domain models, business logic, application orchestration, and persistence schema.

### Core Architectural Invariants
1. **Module Autonomy**: Each module owns its own database schema, tables, migrations, and Entity Framework Core `DbContext`.
2. **Decoupled Cross-Module Communication**:
   - Synchronous module-to-module queries or command invocations occur strictly via contract interfaces defined in `Contracts` assemblies. Direct references between module domain or infrastructure assemblies are prohibited and guarded by automated architecture tests.
   - Asynchronous state transitions and domain notifications are communicated via in-process application events dispatched through the transactional outbox pattern.
3. **Clean Architecture Structure**:
   - `Domain`: Enterprise invariants, aggregate roots, domain events, and value objects.
   - `Application`: CQRS command/query handlers, validation rules, current-user access checks, and service interfaces.
   - `Infrastructure`: EF Core contexts, entity configurations, repositories, caching, and external service adapters.
   - `Contracts`: Integration DTOs, cross-module commands/queries, and application events consumed by peer modules.

## Modules Summary
- `Campaigns`: Manages campaign creation, draft editing, publication, cancellation, funding progress tracking, and distributed query caching.
- `Contributions`: Handles backer pledges, payment state lifecycle (pending, confirmed, failed, cancelled), and usage metering integration.
- `Identity`: Manages user registration, credential authentication, asymmetric ES256 JWT generation, public JWKS key rotation, and RBAC permission checks.
- `Moderation`: Implements review workflows for newly created campaigns, enabling moderators to approve or reject submissions prior to publication.
- `Notifications`: Listens to application events across the system and dispatches notifications to users and backers.
- `CampaignUpdates`: Subscribes to campaign activity events to generate real-time activity feeds and audit timelines.
