# CrowdFunding Unit Tests

## Purpose
Contains fast-running in-memory unit tests exercising domain invariants, CQRS handlers, the command pipeline, authorization rules, transactional outbox serialization, webhook signatures, and OpenMeter usage metering. No database, broker, or network socket is involved.

## Test Suites & Coverage
- `CampaignsTests.cs`: Tests campaign aggregate rules (title, category and story lengths, deadline validation, goal amount), state transitions (draft -> published -> cancelled, successful, failed), ledger entry production (`RecordPledge` / `RecordRefund`), and the create/publish handlers' ownership and permission checks.
- `ContributionsTests.cs`: Tests contribution aggregate state transitions (pending -> confirmed / failed / refunded), payment reference attachment, duplicate confirmation prevention, and the reward-tier reservation id carried on failure and refund domain events.
- `RewardTierTests.cs`: Tests the reward-tier capacity invariant (`ClaimedCount + ReservedCount <= TotalCapacity`) across reserve, confirm, release, and release-claim operations.
- `MoneyTests.cs`: Tests the shared `Money` value object (rounding, currency normalization, same-currency arithmetic, non-negative results).
- `IdentityTests.cs`: Tests user registration (email normalization, no automatic `Admin` role, `Creator` and `Backer` roles for new users), login, role and permission grants, the admin seeder, refresh-token issue/rotation/reuse detection, logout, and the command validators.
- `IdentityInfrastructureTests.cs`: Tests PBKDF2 password hashing and verification, and `SigningKeyProtector` (AES-256-GCM round trip, key id bound as associated data, legacy plaintext decoding, fail-fast on a missing key).
- `ModerationTests.cs`: Tests review creation, approval and rejection transitions, note validation, and the moderation command handlers.
- `NotificationsTests.cs`: Tests the notification handlers and `HttpEmailNotificationService` (idempotency header, provider error propagation, List-Unsubscribe headers, opt-out suppression) and the notification preference query/command handlers.
- `MeteringTests.cs`: Tests OpenMeter CloudEvent generation, fee calculations (basis points, fixed processing fee, net amount), deterministic CloudEvent id deduplication, and error swallowing on ingestion failure.
- `WebhookSubscriptionTests.cs`: Tests SSRF protection (`IsSafeExternalUrl`), outbound HMAC signing, the subscription's consecutive-failure disable threshold, and delivery back-off.
- `StripeWebhookSignatureVerifierTests.cs` / `CloudFunctionSignatureVerifierTests.cs`: Test inbound webhook signature verification (valid signature, tampered body, wrong secret, timestamp tolerance, malformed headers).
- `OutboxTests.cs`: Tests deterministic advisory lock keys, `EventTypeRegistry` registration and versioned resolution, outbox payload round trips, and the retry/back-off/dead-letter state transitions.
- `OutboxSignalTests.cs`: Tests the outbox wake-up signal (notification before the fallback interval, fallback timeout, coalescing of many notifications, cancellation on shutdown).
- `ApplicationEventIdentityTests.cs`: Tests that an application event's `EventId` survives the outbox round trip and becomes the outbox row id.
- `CommandPipelineBehaviorTests.cs`: Tests behavior ordering in the command dispatcher and the audit-logging behavior's recording rules.
- `ValidationPipelineBehaviorTests.cs`: Tests that `ValidationPipelineBehavior` throws `RequestValidationException` and skips the handler when validation fails.
- `ExceptionClassifierTests.cs`: Tests the mapping of domain, application, framework, and Npgsql exceptions to HTTP status codes (400, 409, 503, and a generic 500 that never leaks the framework message).
- `ConfigurationExtensionsTests.cs`: Tests per-module connection string resolution (`ConnectionStrings:{Module}Db` falling back to `DefaultConnection`).
- `RosettaStoneTests.cs`: Tests the Rosetta Stone sample's command validator.

## Shared Test Doubles & Utilities
- `FakeTransactionExecutors.cs`: In-memory implementations of module transaction executors (`ICampaignTransactionExecutor`, `IContributionTransactionExecutor`, `IIdentityTransactionExecutor`, `IModerationTransactionExecutor`) executing actions directly without EF Core transactions.
- `TestCurrentUser.cs`: Controllable `ICurrentUser` test double allowing tests to set authenticated user ids, emails, roles, and fine-grained permissions.
- `TestDispatchers.cs`: In-memory test doubles for `ICommandDispatcher` and `IQueryDispatcher`.
