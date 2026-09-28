# CrowdFunding Integration Tests

## Purpose
Integration and end-to-end test suite executing against containerized PostgreSQL 16, Redis, and RabbitMQ instances (`Testcontainers`). It covers what in-memory tests cannot: real HTTP pipeline behavior, PostgreSQL concurrency semantics (advisory locks, `xmin` tokens, unique keys, triggers), the append-only campaign ledger, outbox and inbox delivery, and cross-instance behavior over Redis.

## Fixtures & Shared Infrastructure
- `CrowdFundingApiFactory.cs`: Boots the full ASP.NET Core pipeline (routing, binding, validation, JWT authentication, authorization policies, rate limiting, `GlobalExceptionHandler`) against real PostgreSQL and Redis containers. `ProcessOutboxMessagesAsync` drains the outboxes deterministically instead of waiting for the fallback poll.
- `CrowdFundingApiCollection.cs`: Shares one factory (and its containers) across every E2E test class.
- `ApiTestExtensions.cs`: HTTP helpers that register and log in through the real endpoints rather than hand-crafting JWTs.
- `CampaignsPostgresFixture.cs` / `ContributionsPostgresFixture.cs`: Module-level PostgreSQL containers with that module's migrations applied, for concurrency tests below the HTTP layer.
- `AssemblyInfo.cs`: Disables cross-collection parallelism, because some fixtures change process-wide environment variables read at host build time.

## Test Suites
- **Campaign ledger and concurrency**: `CampaignLedgerTests.cs` (concurrent and redelivered pledges, refunds, and the `raised_amount = SUM(entries)` invariant), `CampaignLedgerMigrationTests.cs` (backfill of pre-ledger data), `ModuleSchemaMigrationTests.cs` (upgrade from tables in `public` to one schema per module: rows preserved, ledger triggers and outbox delivery still work), `ContributionConcurrencyTests.cs` (`xmin` conflict between confirm and fail), `RewardTierConcurrencyE2ETests.cs` (50 concurrent reservations against 5 slots), `CampaignNestedTransactionTests.cs` (only the outermost transaction harvests events and commits), `CampaignCachePostCommitInvalidationTests.cs` (cache eviction only after commit).
- **Business flows over HTTP**: `CampaignLifecycleE2ETests.cs`, `ContributionAndOutboxE2ETests.cs`, `CampaignExpirationE2ETests.cs`, `RefundSagaE2ETests.cs`, `EmailNotificationOutboxE2ETests.cs`, `AuditLoggingE2ETests.cs`, `CoverageGapE2ETests.cs` (list/pagination, health checks, rejection and 404 paths).
- **Messaging**: `OutboxSignalE2ETests.cs` (wake-up only after commit, lease reclaim), `OutboxLeanTableE2ETests.cs` (processed rows deleted, poison rows dead-lettered), `InboxIdempotencyE2ETests.cs` (`IIdempotentConsumer` applies an effect once), `RabbitMqMessageBusTests.cs` (publish-only CloudEvents envelope observed by a raw test consumer), `OpenTelemetryE2ETests.cs` (the outbox dispatch span continues the request's trace).
- **Webhooks**: `PaymentGatewayWebhookReconciliationE2ETests.cs` (Stripe signature, redelivery, out-of-order events), `MediaAnalysisWebhookE2ETests.cs` (signed media-analysis callback), `WebhookSubscriptionE2ETests.cs` (SSRF rejection, ownership, signed outbound delivery).
- **Identity and security**: `UserAuthenticationE2ETests.cs`, `RefreshTokenRotationE2ETests.cs`, `SigningKeyEncryptionE2ETests.cs` (keys encrypted at rest, legacy rows re-protected), `SigningKeyMultiReplicaInvalidationE2ETests.cs`.
- **API platform**: `ApiPlatformE2ETests.cs` (JWKS, correlation id, RFC 9457 shape, 429), `EdgeHardeningTests.cs` (429 ProblemDetails with `traceId` and `Retry-After`, per-`sub` partitioning, CORS), `MinimalApiEndpointTests.cs` (malformed JSON is 400, `Location` headers, OpenAPI parity with the comparison controller), `OpenApiDocumentationTests.cs` (built-in OpenAPI document and Scalar UI).
- **Scale-out and decoupling**: `MultiDatabaseConnectionDecouplingTests.cs` (Campaigns on its own database via `ConnectionStrings:CampaignsDb`), `ReplicatedReadModelResilienceTests.cs` (pledging still works with the Campaigns database unreachable), `SignalRRedisBackplaneE2ETests.cs` (broadcast across two instances).
- **Teaching sample**: `RosettaStoneParityTests.cs` (the three Rosetta Stone tiers accept the same payload and return the same validation ProblemDetails).

## Prerequisites
Integration tests require a running Docker daemon on the host machine for the Testcontainers images.
