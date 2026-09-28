# Platform Shortcomings

This document lists the known limitations of the CrowdFunding Hub modular monolith on branch `feature/clean-arch-course`, as of 2026-09-28. Each entry says what the limitation is, why it matters, and how to fix it, with links to the relevant code.

The first version of this document (2026-09-08) listed 14 shortcomings. Most are now fixed. They are summarized under [Resolved](#resolved). For design choices that are deliberate rather than gaps, see [TRADE_OFFS_AND_LIMITATIONS.md](TRADE_OFFS_AND_LIMITATIONS.md).

## Summary

| # | Area | Limitation | Severity |
| :-: | :--- | :--- | :-: |
| 1 | Messaging | No broker consumer: RabbitMQ bus publishes only | High (blocks extraction) |
| 2 | Messaging | Outbox wake-up signal is per process | Low |
| 3 | Messaging | Outbound webhooks are at-least-once | Low (documented contract) |
| 4 | Messaging | E-mail and metering rely on provider idempotency keys | Medium |
| 5 | Security | Anonymous unsubscribe trusts a bare user id | High |
| 6 | Security | No rotation of the signing-key encryption key (KEK) | Medium |
| 7 | Scale | Rate limiting is per instance | Medium |
| 8 | Persistence | Module tables share the `public` schema | Medium |
| 9 | Payments | Refunds are recorded, not executed at the gateway; no dispute handling | High |
| 10 | Domain | Ledger edge cases: early refund, hot-campaign serialization | Low |
| 11 | Domain | Single currency per campaign | Low (scope choice) |
| 12 | Application | Queries are not validated by the pipeline | Low |
| 13 | Observability | Logs are not exported through OpenTelemetry | Low |
| 14 | Modules | Publishing a campaign reads Moderation synchronously | Medium (blocks extraction) |

## Remaining limitations

### 1. No broker consumer: the RabbitMQ bus publishes only

**What.** `IMessageBus` has two implementations. [`InProcessMessageBus`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Messaging/InProcessMessageBus.cs) is the default (`Messaging:Provider=InProcess`) and is the working mode. [`RabbitMqMessageBus`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Messaging/RabbitMqMessageBus.cs) publishes a CloudEvents envelope ([`CloudEventEnvelope`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Messaging/CloudEventEnvelope.cs)) to RabbitMQ, but nothing in the codebase consumes from RabbitMQ.

**Why it matters.** With `Messaging:Provider=RabbitMq`, events leave the process and no handler runs. Cross-process delivery, and therefore extracting any module into its own service, is not implemented on this branch.

**Fix path.** Add a hosted consumer that reads the queue, unwraps the envelope, and dispatches to the same `IEventHandler<T>` handlers. Guard each handler with the inbox (`IIdempotentConsumer.ExecuteOnceAsync`, keyed by the CloudEvents `id`, which equals `BaseApplicationEvent.EventId`) because the broker delivers at least once. Add dead-letter queues and a contract test per event type.

**Microservice extraction.** Moderation is an in-process module of the monolith on this branch. Extracting it into its own service is the upcoming course step. A working proof of concept (standalone Moderation host, JWKS-validated auth, a Strangler Fig topology, extraction boundary tests) is preserved on branch `future/microservices-extraction`. It is not implemented here.

### 2. The outbox wake-up signal is per process

**What.** Each module's [`ModuleOutboxProcessor`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Outbox/ModuleOutboxProcessor.cs) claims rows with `FOR UPDATE SKIP LOCKED` ([`OutboxClaimQuery`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Persistence/OutboxClaimQuery.cs)). It is woken by [`OutboxSignal<TDbContext>`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Outbox/OutboxSignal.cs), which [`OutboxCommitSignalInterceptor`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Outbox/OutboxCommitSignalInterceptor.cs) notifies after a transaction that wrote outbox rows commits. The signal is an in-memory channel. Other replicas do not hear it and pick up work on the 5-second fallback sweep.

**Why it matters.** Correctness is unaffected: `SKIP LOCKED` lets any replica claim any row, and expired leases are reclaimed. Only latency on other replicas is affected, and only when the committing replica is not the one that dispatches.

**Fix path.** If cross-replica latency matters, add PostgreSQL `LISTEN/NOTIFY` as a second wake-up source next to the in-process signal, keeping the sweep as the safety net.

**Considered and not implemented: a CDC relay.** A change-data-capture relay (Debezium reading the WAL through logical decoding and publishing outbox inserts to a broker) was considered. It removes polling and claim updates entirely, but it adds Kafka Connect, a replication slot to operate, and a broker, and it only pays off once a broker consumer exists (item 1). `SKIP LOCKED` polling plus the in-process commit signal was chosen. Rows are deleted on success, so table bloat stays bounded. See [outbox-architecture.md](outbox-architecture.md).

### 3. Outbound webhooks are at-least-once

**What.** [`WebhookDispatcherBackgroundService`](../src/Modules/CampaignUpdates/CrowdFunding.Modules.CampaignUpdates.Infrastructure/Services/WebhookDispatcherBackgroundService.cs) retries failed deliveries. A receiver that returns an error, or times out after processing, will see the same event again.

**Why it matters.** Receivers that do not deduplicate will double-process `pledge.confirmed`.

**Fix path.** This is the intended contract, not a bug. Document it for subscribers: deduplicate on `(eventType, contributionId)` (the payload has no separate event id) and verify the `X-CrowdFunding-Signature` header ([`WebhookSignature`](../src/Modules/CampaignUpdates/CrowdFunding.Modules.CampaignUpdates.Contracts/Webhooks/WebhookSignature.cs)), rejecting stale timestamps. Exactly-once delivery to an external system is not achievable; exactly-once processing is the receiver's job.

### 4. E-mail and metering rely on provider idempotency keys

**What.** [`HttpEmailNotificationService`](../src/Modules/Notifications/CrowdFunding.Modules.Notifications.Infrastructure/Services/HttpEmailNotificationService.cs) sends an idempotency header derived from the business key. [`PledgeConfirmedMeteringEventHandler`](../src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/Metering/PledgeConfirmedMeteringEventHandler.cs) does the same for usage metering. Neither goes through the inbox.

**Why it matters.** If the process crashes after the provider accepts the call but before the handler's work commits, the event is redelivered and the call is made again. Whether that produces a duplicate depends on the provider honoring the key.

**Fix path.** Record each external side effect in an outbound-request table inside the handler's transaction, then send from a relay that marks the row sent. Alternatively, wrap the handler in `IIdempotentConsumer.ExecuteOnceAsync` and accept that the claim and the external call still cannot be atomic.

### 5. Anonymous one-click unsubscribe trusts a bare user id

**What.** `POST /api/notifications/unsubscribe` in [`NotificationEndpoints`](../src/API/CrowdFunding.API/Endpoints/NotificationEndpoints.cs) accepts an optional `UserId` in the body from an anonymous caller.

**Why it matters.** Anyone who knows or guesses a user id can unsubscribe that user from non-transactional e-mail. User ids are GUIDs, so guessing is hard, but ids leak (URLs, logs, webhook payloads).

**Fix path.** Put a signed, expiring token in the `List-Unsubscribe` link (HMAC over user id, list and expiry, or a single-use token stored hashed). Validate it in the endpoint and drop the raw `UserId` field.

### 6. No rotation of the signing-key encryption key (KEK)

**What.** [`SigningKeyProtector`](../src/Modules/Identity/CrowdFunding.Modules.Identity.Infrastructure/Services/SigningKeyProtector.cs) encrypts private JWT signing keys at rest with AES-256-GCM under one key from `SigningKeyEncryption:Key`. JWT signing keys rotate; this key-encryption key does not.

**Why it matters.** A leaked KEK exposes every stored signing key, and replacing it today means a manual re-encryption.

**Fix path.** The `ENC:v1:` prefix leaves room for versioning. Add a KEK id to the prefix, accept a set of KEKs (one active, others decrypt-only), and re-encrypt rows under the active KEK on warm-up, the same way plaintext rows are upgraded now. A managed KMS removes the key from configuration altogether.

### 7. Rate limiting is per instance

**What.** [`RateLimitingConfiguration`](../src/API/CrowdFunding.API/RateLimiting/RateLimitingConfiguration.cs) uses the built-in in-memory limiters: a global policy (300 requests per 60 s per `sub`, else per IP) and strict policies for auth, payments and webhooks.

**Why it matters.** With N replicas behind a load balancer, the effective limit is up to N times the configured one. Brute-force protection on login weakens as the deployment scales out.

**Fix path.** Move the strict policies to a shared store (a Redis token bucket, or limits at the gateway/WAF). Keep the in-memory global limiter as a cheap first line.

### 8. Module tables share the `public` schema

**What.** Each module has its own DbContext and its own tables, with no cross-module foreign keys and a per-module connection string (`ConnectionStrings:{Module}Db`, falling back to `DefaultConnection`). Only each module's EF migrations history table lives in a per-module schema (`campaigns`, `contributions`, `identity`, `moderation`, `notifications`, `campaignupdates`; audit uses `system`). The tables themselves are in `public`, separated by naming convention (for example `campaigns`, `campaign_reviews`, `contributions_processed_events`).

**Why it matters.** Ownership is enforced by code and review, not by the database. Nothing stops one module's connection from reading another module's tables, and moving a module to its own database requires knowing which tables it owns.

**Fix path.** Call `HasDefaultSchema("<module>")` in each module's DbContext, add a migration that moves the tables (`ALTER TABLE ... SET SCHEMA`), and give each module its own database role with `USAGE` on its schema only. Start with [`CampaignsDbContext`](../src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Infrastructure/Persistence/DbContexts/CampaignsDbContext.cs).

### 9. Refunds are recorded, not executed at the gateway; no dispute handling

**What.** When a campaign is cancelled or fails, [`CampaignTerminationRefundHandler`](../src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/Contributions/Events/CampaignTerminationRefundHandler.cs) moves every `Succeeded` contribution to `Refunded` and publishes `ContributionRefundedApplicationEvent`. No payment-gateway refund is called. [`ReconcilePaymentWebhookCommandHandler`](../src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/Contributions/Commands/ReconcilePaymentWebhook/ReconcilePaymentWebhookCommandHandler.cs) handles only `payment_intent.succeeded` and `payment_intent.payment_failed`; disputes and chargebacks are ignored.

**Why it matters.** The platform's books say money was returned while the gateway has not been asked to return it. A disputed payment is not reflected in the contribution or the campaign ledger.

**Fix path.** Add a `Refunding` state and a gateway port (`IPaymentGateway.RefundAsync` with the contribution id as idempotency key), sent through the outbox. Confirm `Refunded` from the gateway's `charge.refunded` webhook. Handle `charge.dispute.*` events with `Disputed` and `ChargedBack` states and a ledger refund entry.

### 10. Ledger edge cases

**What.** The campaign funding ledger ([`CampaignLedger`](../src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Infrastructure/Persistence/Repositories/CampaignLedger.cs)) is append-only and recomputes `raised_amount` as the sum of its entries. Two edge cases remain:

- A refund entry requires an existing pledge entry. A refund that arrives before its pledge credit is a no-op. This is acceptable today because refunds only follow cancellation or failure, which come after pledges.
- Every credit for one campaign serializes on a per-campaign `pg_advisory_xact_lock`. A very popular campaign becomes a hot row.

**Why it matters.** The first case matters once refunds can be triggered independently (item 9, disputes). The second limits pledge throughput for a single campaign.

**Fix path.** For early refunds, park them (a pending-refund row replayed when the pledge lands) or relax the pledge requirement and let the sum go through a separate reconciliation. For hot campaigns, shard the counter (N sub-balances summed on read) or batch credits. See [append-only-ledger.md](append-only-ledger.md).

### 11. Single currency per campaign

**What.** [`MakeContributionCommandHandler`](../src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/Contributions/Commands/MakeContribution/MakeContributionCommandHandler.cs) rejects a pledge whose currency differs from the campaign's, and [`Money`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Domain/ValueObjects/Money.cs) refuses cross-currency arithmetic.

**Why it matters.** Backers must pay in the campaign's currency. This is a scope choice for the course, not a defect: the mismatch is rejected before any contribution exists.

**Fix path.** Store both the contributed amount and the settled amount in campaign currency, with the exchange rate and when it was locked. Credit the ledger with the settled amount only.

### 12. Queries are not validated by the pipeline

**What.** [`ValidationPipelineBehavior`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Application/Validation/ValidationPipelineBehavior.cs) runs FluentValidation validators for commands only.

**Why it matters.** Query parameters (paging, ids) are checked ad hoc or not at all, which is inconsistent with commands.

**Fix path.** Add the same behavior for the query pipeline and write validators for queries that take user input.

### 13. Logs are not exported through OpenTelemetry

**What.** [`OpenTelemetryConfiguration`](../src/API/CrowdFunding.API/Observability/OpenTelemetryConfiguration.cs) exports traces and metrics over OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. Logs stay on Serilog (compact JSON to console and files) and carry trace and span ids.

**Why it matters.** Logs do not appear next to traces in the Aspire dashboard or an OTLP backend; correlation needs a separate log pipeline.

**Fix path.** Add the Serilog OpenTelemetry sink, or the `WithLogging` OTLP exporter, behind the same endpoint setting.

### 14. Publishing a campaign reads Moderation synchronously

**What.** [`PublishCampaignCommandHandler`](../src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/PublishCampaign/PublishCampaignCommandHandler.cs) asks [`ICampaignReviewStatusReader`](../src/Modules/Moderation/CrowdFunding.Modules.Moderation.Contracts/Queries/GetCampaignReviewStatusByCampaignId/ICampaignReviewStatusReader.cs) (defined in Moderation.Contracts, implemented in-process by [`CampaignReviewStatusReader`](../src/Modules/Moderation/CrowdFunding.Modules.Moderation.Infrastructure/Services/CampaignReviewStatusReader.cs)) whether the review is approved. The dependency points only at Contracts, so the boundary tests pass, but at runtime Campaigns queries Moderation's data in the same process.

**Why it matters.** It is the same synchronous cross-module read that was removed from the pledge path (Contributions now uses its replicated `active_campaigns_cache`). Once Moderation runs as its own service, publishing would need a network call to Moderation and would fail whenever Moderation is down.

**Fix path.** Replicate the review decision into Campaigns: handle `CampaignReviewApprovedApplicationEvent` and `CampaignReviewRejectedApplicationEvent` in Campaigns (idempotently) and store the status on the campaign or in a small local read model; `PublishCampaignCommandHandler` then checks local state. Add an architecture test that forbids Campaigns.Application from depending on Moderation.Contracts query types, as `ReplicatedReadModelBoundaryTests` does for Contributions.

## Resolved

Fixed since the 2026-09-08 review. One line each; the code is the reference.

| Former shortcoming | What fixed it |
| :--- | :--- |
| No refunds when a campaign is cancelled or fails | `CampaignTerminationRefundHandler`, `ContributionStatus.Refunded`, compensating ledger entries (gateway call still missing, item 9) |
| No campaign deadline expiry | [`CampaignExpirationBackgroundService`](../src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Infrastructure/BackgroundServices/CampaignExpirationBackgroundService.cs) with `Campaign.CompleteSuccessfully` and `Campaign.MarkFailed` |
| No refresh tokens or session revocation | 15-minute access tokens, rotating refresh tokens ([`RefreshTokenService`](../src/Modules/Identity/CrowdFunding.Modules.Identity.Infrastructure/Services/RefreshTokenService.cs)), `/logout`, security stamp checked via [`RedisSecurityStampRevocationStore`](../src/Modules/Identity/CrowdFunding.Modules.Identity.Infrastructure/Services/RedisSecurityStampRevocationStore.cs) |
| No audit log of administrative actions | [`AuditLoggingPipelineBehavior`](../src/BuildingBlocks/CrowdFunding.BuildingBlocks.Application/Audit/AuditLoggingPipelineBehavior.cs) with `[AuditableAction]` and its own `AuditDbContext` |
| Connection-pool contention from cross-module reads | Contributions reads a replicated read model ([`ActiveCampaignCacheRepository`](../src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/Persistence/Repositories/ActiveCampaignCacheRepository.cs)); per-module connection strings |
| Outbox table bloat from status updates | Rows deleted on success; dead-letter table after 5 attempts |
| Single 5-second outbox timer, 4 messages per second ceiling | Per-module `ModuleOutboxProcessor` that drains continuously, woken on commit; 5 s is only the fallback sweep |
| Stale signing keys on other replicas; cache evicted before commit | [`SigningKeyRefreshBackgroundService`](../src/Modules/Identity/CrowdFunding.Modules.Identity.Infrastructure/Services/SigningKeyRefreshBackgroundService.cs) plus Pub/Sub invalidation; cache eviction deferred until after commit (`EnqueueCacheInvalidation`) |
| No reward tiers | [`RewardTier`](../src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/RewardTier.cs) with reservations and a reservation scavenger |
| No API Dockerfile | [`src/API/CrowdFunding.API/Dockerfile`](../src/API/CrowdFunding.API/Dockerfile) |
| No CI | [`.github/workflows/ci.yml`](../.github/workflows/ci.yml): format check, Release build with warnings as errors, architecture, unit and integration tests |
| No OpenTelemetry | Traces and metrics over OTLP; `docker-compose.yml` runs the Aspire dashboard |
| Stub notifications, no preferences | [`NotificationPreference`](../src/Modules/Notifications/CrowdFunding.Modules.Notifications.Domain/Aggregates/NotificationPreference.cs), RFC 8058 unsubscribe headers, preference-aware e-mail (anonymous unsubscribe still weak, item 5) |
| Increment-based campaign totals | Append-only ledger with database-enforced sum ([append-only-ledger.md](append-only-ledger.md)) |

Not addressed and out of scope for the course: backer profiles (avatar, biography, public backing history).
