# CrowdFunding Unit Tests

## Purpose
Contains fast-running in-memory unit tests exercising domain invariants, CQRS handlers, authorization policies, transactional outbox serialization, and OpenMeter usage metering.

## Test Suites & Coverage
- `CampaignsTests.cs`: Tests campaign aggregate rules (title/description lengths, deadline validation, goal calculations), draft creation, state transitions (draft -> published -> cancelled), owner permission checks, and contribution ledger application.
- `ContributionsTests.cs`: Tests contribution aggregate state transitions (pending -> confirmed / failed / cancelled), payment reference attachment, duplicate confirmation prevention, and currency mismatch validation.
- `IdentityTests.cs`: Tests user self-registration, unique email normalization, password hashing/verification, login credential validation, role assignment (`Admin`, `Moderator`, `User`), and asymmetric ES256 key generation.
- `ModerationTests.cs`: Tests review creation upon campaign creation events, approval and rejection transitions, moderator permission enforcement, and reviewer feedback attachment.
- `MeteringTests.cs`: Tests OpenMeter CloudEvent generation, fee calculations (basis points, fixed processing fee, net amount), deterministic CloudEvent ID deduplication, and resilient error swallowing.
- `OutboxTests.cs`: Tests outbox message serialization, JSON payload integrity, event type discriminator mapping, and `EventTypeRegistry` resolution.

## Shared Test Doubles & Utilities
- `FakeTransactionExecutors.cs`: In-memory implementations of module transaction executors (`ICampaignsTransactionExecutor`, `IContributionsTransactionExecutor`, `IIdentityTransactionExecutor`, `IModerationTransactionExecutor`) executing actions directly without EF Core transactions.
- `TestCurrentUser.cs`: Controllable `ICurrentUser` test double allowing tests to easily mock authenticated user IDs, emails, roles, and fine-grained permissions.
- `TestDispatchers.cs`: In-memory test doubles for `ICommandDispatcher` and `IQueryDispatcher`.
