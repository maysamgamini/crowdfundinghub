# Functional Business Modules

## Architecture Overview
The platform is organized as a **modular monolith** divided into six distinct bounded contexts. Each module maintains strict encapsulation, owning its own domain models, business logic, application orchestration, and persistence.

### Core Architectural Invariants
1. **Module Autonomy**: Each module owns one Entity Framework Core `DbContext`, one set of tables, and its own migrations. Each module owns one PostgreSQL schema named after it (`campaigns`, `contributions`, `identity`, `moderation`, `notifications`, `campaignupdates`) holding all its tables, its outbox, inbox and migration history. There are no cross-module foreign keys, `ModuleSchemaOwnershipTests` fails the build if a module maps a table outside its schema, and each module reads its own connection string (`ConnectionStrings:{Module}Db`, falling back to `DefaultConnection`) so it can move to its own database. See `docs/data-ownership.md`.
2. **Decoupled Cross-Module Communication**:
   - Synchronous module-to-module queries or command invocations occur strictly via types defined in `Contracts` assemblies. Direct references to another module's Domain, Application, or Infrastructure assemblies are prohibited and guarded by the architecture tests (`ModuleBoundaryConventionTests`).
   - Asynchronous state transitions and domain notifications are communicated via application events written to each publishing module's transactional outbox and delivered through `IMessageBus` (in process by default). Consumers that must not apply an effect twice use the inbox (`IIdempotentConsumer`).
   - Where a module needs another module's data on its hot path, it keeps its own replicated read model fed by events (for example Contributions' `active_campaigns_cache`) instead of querying the other module.
3. **Clean Architecture Structure**:
   - `Domain`: Enterprise invariants, aggregate roots, domain events, and value objects.
   - `Application`: CQRS command/query handlers, validation rules, current-user access checks, and service interfaces.
   - `Infrastructure`: EF Core contexts, entity configurations, repositories, caching, and external service adapters.
   - `Contracts`: Integration DTOs, cross-module commands/queries, and application events consumed by peer modules.
   Every module has exactly these four projects.

## Modules Summary
- `Campaigns`: Manages campaign creation, publication, cancellation, deadline-driven completion or failure, reward tiers and their reservations, the append-only funding ledger (`campaign_ledger_entries`), and distributed query caching.
- `Contributions`: Handles backer pledges, the payment state lifecycle (pending, confirmed, failed, refunded), payment-gateway webhook reconciliation, refunds when a campaign is cancelled or fails, and usage metering integration.
- `Identity`: Manages user registration, credential authentication, asymmetric ES256 JWT generation with encrypted-at-rest signing keys, public JWKS key rotation, refresh-token rotation, logout via security-stamp revocation, and role and permission management.
- `Moderation`: Implements review workflows for newly created campaigns, enabling moderators (or a signed media-analysis callback) to approve or reject submissions prior to publication.
- `Notifications`: Listens to application events across the system, sends e-mail notifications to users and backers through an HTTP e-mail provider, and manages each user's notification preferences and one-click unsubscribe.
- `CampaignUpdates`: Manages outbound webhook subscriptions for a campaign and fans a confirmed pledge out to them as signed `pledge.confirmed` deliveries (HMAC-SHA256, `X-CrowdFunding-Signature`), with SSRF protection on registration and retrying delivery.
