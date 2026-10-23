# Agent 2: code consistency audit (read-only)

Repo: /Users/maysamgamini/maysam-brain/contents/projects/projects-active/crowdfunding-reclaim, branch feat/outbox-reclaim.
Nothing was built, run or edited. Every finding was verified by reading the cited lines.
Paths below are relative to that repo root. M = src/Modules, BB = src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure.

Checked and found consistent (no finding): schema + migrations-history table in every module DI; table naming `<module>_outbox_messages` / `_dead_letter_events` / `_inbox_messages` across model, migrations and snapshots; no model/config file changed after the last migration of any module (git log check); every domain event has an outbox mapping in its executor; permission policy names vs controllers vs RolePermissionCatalog (all ten constants are in `All`, Admin gets all); CancellationToken pass-through (no parameterless async EF/HTTP calls; only two `CancellationToken.None` at startup); Mapster record pairs match by name (only expected differences); `AddRequestHandlersFromAssemblies` covers every handler assembly incl. the Notifications Infrastructure handlers; connection-string keys and database names agree between appsettings.Development.json, appsettings.HA.json, deploy/postgres/init-module-databases.sh and docker-compose.ha.yml (all 8 databases, ports 6000-6071); no TODO/FIXME in code.

---

### F1: Background loops have no top-level exception guard, so one DB error stops the whole host
- Severity: P1
- Where: BB/Outbox/ModuleOutboxProcessor.cs:62-64 (loop), :164 (publish catch), :186 (final SaveChanges); M/CampaignUpdates/.../Services/WebhookDispatcherBackgroundService.cs:39-41 (loop), :152 (second UpdateAsync inside the concurrency catch). Affects Campaigns, Contributions, Moderation outbox workers (all three subclasses) and the webhook dispatcher. Contrast: CampaignExpiration, RewardTierReservationScavenger and SigningKeyRefresh services all wrap their iteration in try/catch.
- Evidence: `while (!stoppingToken.IsCancellationRequested) { var claimed = await ProcessBatchCoreAsync(stoppingToken); ...` with no try/catch; Program.cs sets no `BackgroundServiceExceptionBehavior`, so an escaped exception stops the host (.NET default StopHost). Triggers: any Npgsql blip in the claim query or in the final `SaveChangesAsync`; a `DbUpdateConcurrencyException` when the final SaveChanges deletes a row another worker already reclaimed and deleted (see F12); the dispatcher's retry `UpdateAsync(current)` after a second concurrent change.
- Fix: wrap each loop iteration in try/catch (log, back off with Task.Delay, rethrow only on cancellation); in the dispatcher guard `ClaimDueBatchAsync` and both `UpdateAsync` calls.
- Acceptance: integration test that makes the claim query throw once (e.g. drop connection / stub DbContext) and asserts the worker keeps running and delivers the next message; same for a forced `DbUpdateConcurrencyException` in the dispatcher.

### F2: Contribution list and detail endpoints are anonymous and return payment data
- Severity: P1
- Where: src/API/CrowdFunding.API/Controllers/ContributionsController.cs:63 (GET list), :156 (GET by id); no class-level or method `[Authorize]` (every other mutating endpoint has one). Contracts: API/Contracts/Contributions/ListContributionsResponse.cs, GetContributionByIdResponse.cs.
- Evidence: list returns `ContributorId, Amount, Status, PaymentReference, FailureReason` for every pledge and accepts `?contributorId=` as a filter; by-id additionally returns `ExternalPaymentIntentId`. XML docs list only 200/404, no 401.
- Fix: require authentication; show a public view (count/total, no contributor id, no payment fields) to anonymous callers and the full row only to the contributor, the campaign owner, or `contributions.payments.manage`.
- Acceptance: E2E test: anonymous GET returns 401 (or the redacted shape); contributor B cannot read contributor A's pledge by id.

### F3: Extracted Moderation service lets any authenticated token read any review
- Severity: P1
- Where: services/CrowdFunding.Moderation.Service/Controllers/ModerationReviewsController.cs:21 (`[Authorize]` only), :41 GET `{campaignId}`; Program.cs `AddAuthorization()` registers no policies. Monolith equivalent: src/API/.../Controllers/ModerationController.cs:95 has `[Authorize(Policy = ModerationReview)]`.
- Evidence: Approve/Reject are protected only because the command handlers call `HasPermission(ModerationReview)` (ApproveCampaignReviewCommandHandler.cs:59). `GetCampaignReviewByCampaignIdQueryHandler` has no permission check, so a Backer token reads notes, scores and moderator id. The service also drops the monolith's rate limiting, audit pipeline behavior, correlation id and the list endpoint, and returns `ApproveCampaignReviewResult` where the monolith returns `CampaignReviewResponse` (contract divergence on the same route `api/moderation/reviews`).
- Fix: register the `moderation.review` permission policy in the service (reuse `PermissionConstants.All` loop from API Program.cs:196) and put it on GET; either add the audit behavior or document the loss; align the response type.
- Acceptance: service integration test: Backer-role JWT gets 403 on GET and POST; Moderator JWT gets 200.

### F4: With `Messaging:Provider = RabbitMQ` nothing consumes events, so no handler ever runs
- Severity: P1
- Where: BB/Messaging/RabbitMqMessageBus.cs (publish only; no consumer anywhere: grep for BasicConsume/AsyncEventingBasicConsumer in src and services finds only the test); BB/Messaging/MessagingDependencyInjection.cs:16-22; docker-compose.microservices.yml:62-66 and :86-88 set RabbitMQ for both hosts; services/CrowdFunding.Moderation.Service/README.md:18.
- Evidence: the outbox worker publishes only via `IMessageBus` (ModuleOutboxProcessor.cs:140 region). In RabbitMQ mode `ServiceProviderEventPublisher` is never called, so in the monolith the Contributions->Campaigns credit, refund saga, Notifications, CampaignUpdates and Moderation CampaignCreated handler all stop; outbox rows are deleted as "delivered". The extracted service therefore never creates a review (Approve returns 404 for every real campaign). Smoke test uses `Messaging:Provider=InProcess` and seeds the review by hand, which hides it. `Source` is also hard-coded `urn:crowdfunding:monolith` even when published by the service (RabbitMqMessageBus.cs:38).
- Fix: add a consumer hosted service that reads the exchange and calls `IEventPublisher.PublishAsync` (with the CloudEvent id as `InboxMessageContext.MessageId`), or make RabbitMQ mode publish in addition to in-process delivery; correct the README claim until then.
- Acceptance: compose-level or Testcontainers test: create campaign in monolith with RabbitMQ provider, assert a review row appears in the Moderation service; pledge confirmed raises `RaisedAmount`.

### F5: docker-compose.microservices.yml monolith cannot reach its databases
- Severity: P1
- Where: docker-compose.microservices.yml:50-60 (monolith-api, `ASPNETCORE_ENVIRONMENT: Development`, only `ConnectionStrings__DefaultConnection` set); src/API/CrowdFunding.API/appsettings.Development.json (module keys `IdentityDb`...`RosettaStoneDb` -> `Host=localhost`); BB/Configuration/ConfigurationExtensions.cs (module key wins over `DefaultConnection`).
- Evidence: inside the container the module-specific `localhost` strings win over the env-provided fallback, and Development runs `MigrationRunner.RunAsync` at boot, so the container fails at startup. The Moderation service container is fine because it sets `ConnectionStrings__ModerationDb`.
- Fix: set every `ConnectionStrings__<Module>Db` in the compose file (or run the monolith as Production plus a `migrate` init job).
- Acceptance: `docker compose -f docker-compose.microservices.yml up` reaches a healthy `/health/ready` on monolith-api.

### F6: One-click unsubscribe works for any user id with no token
- Severity: P1
- Where: src/API/CrowdFunding.API/Controllers/NotificationsController.cs:89-107 (`[AllowAnonymous]`, `targetUserId = request?.UserId ?? ...`); M/Notifications/.../Features/Preferences/UnsubscribeCommandHandler.cs; no `EnableRateLimiting`.
- Evidence: anyone who knows or guesses a user GUID can turn off that user's campaign-update and marketing preferences (and create a preference row for any id). docs/PLATFORM_SHORTCOMINGS.md:302-309 describe an `UnsubscribeToken` and say RFC 8058 was implemented; the code has no token.
- Fix: require a signed, per-user unsubscribe token (HMAC over user id) or an authenticated caller; apply the webhook/auth rate-limit policy.
- Acceptance: test: anonymous POST with another user's id and no valid token returns 400/401 and leaves preferences unchanged.

### F7: Moderation reviews have no concurrency token or concurrency translation (deviates from Campaigns, Contributions, Identity)
- Severity: P2
- Where: M/Moderation/.../Persistence/Configurations/CampaignReviewConfiguration.cs (no `xmin`; every other aggregate has one at e.g. ContributionConfiguration.cs:98); M/Moderation/.../Transactions/ModerationTransactionExecutor.cs (only a bare `catch`, no `DbUpdateConcurrencyException` -> `ConcurrencyConflictException`, unlike Identity/Campaigns/Contributions executors).
- Evidence: two moderators (or a moderator and the media-analysis webhook) can load the same Pending review; both pass `EnsurePending`, last write wins, and both `CampaignReviewApproved` and `CampaignReviewRejected` events are written to the outbox, so downstream modules (Notifications, CampaignUpdates, Campaigns publish gate) see contradictory outcomes.
- Fix: add `xmin` token + migration, translate the exception in the executor, document 409 on approve/reject.
- Acceptance: concurrent approve+reject integration test yields exactly one success and one 409, and exactly one event in the outbox.

### F8: GlobalExceptionHandler leaks raw messages for any InvalidOperationException / ArgumentException and has no mapping for DbUpdateConcurrencyException
- Severity: P2
- Where: src/API/CrowdFunding.API/Observability/GlobalExceptionHandler.cs:40-41 (400 with `exception.Message`), :54 (only unique-violation `DbUpdateException` mapped); duplicate in services/CrowdFunding.Moderation.Service/Observability/ServiceExceptionHandler.cs.
- Evidence: the class comment says only sanitized cases expose their message, but framework/EF/Npgsql `InvalidOperationException` ("A second operation was started...", "Sequence contains no elements", DI errors) are returned verbatim with HTTP 400 instead of 500. A raw `DbUpdateConcurrencyException` (modules without a translating executor: CampaignUpdates repositories, Notifications, Moderation) reaches the catch-all as 500. Besides the known owner-delete case, the same path exists for any future CampaignUpdates `UpdateAsync`; also `OperationCanceledException` from an aborted request becomes a logged 500.
- Fix: introduce domain-specific exceptions for state-machine errors (or filter by exception source), map `DbUpdateConcurrencyException` to 409 and `OperationCanceledException` (RequestAborted) to 499/no-log.
- Acceptance: unit tests on the handler: `new InvalidOperationException("Npgsql internal")` does not reach the response body; `DbUpdateConcurrencyException` -> 409; cancelled request -> not logged as error.

### F9: Media-analysis webhook skips its own validator; out-of-range scores overflow numeric(3,2)
- Severity: P2
- Where: src/API/CrowdFunding.API/Controllers/ModerationWebhooksController.cs:76-79 (sends command without validating); validator exists and is registered: M/Moderation/.../RecordMediaAnalysisCommandValidator.cs (0..1); column `HasPrecision(3,2)` in CampaignReviewConfiguration.cs:40-43. Same pattern: `ReconcilePaymentWebhookCommandValidator` is registered (ContributionsApplicationDependencyInjection) but PaymentWebhooksController.cs:87 never calls it.
- Evidence: a signed payload with `toxicityScore: 12.5` passes the controller, the domain accepts it (CampaignReview.cs:103-110 has no range check), and the save fails with a Postgres numeric overflow, a `DbUpdateException` that is not a unique violation, so 500 and the cloud function retries forever.
- Fix: call the validators in both webhook controllers (400 on failure) or enforce the range in the aggregate.
- Acceptance: webhook test with score 12.5 returns 400 and writes nothing.

### F10: Refund saga can finish "successfully" after a cancellation, losing the rest of the refunds
- Severity: P2
- Where: M/Contributions/.../Events/CampaignTerminationRefundHandler.cs:49 (`while (!cancellationToken.IsCancellationRequested)`).
- Evidence: if the token is cancelled between two batches the loop just exits and `Handle` returns normally; the outbox then deletes the message and the inbox marks the handler done, so remaining Succeeded contributions (batches of 100) are never refunded. Idempotence only helps if the event is redelivered.
- Fix: `cancellationToken.ThrowIfCancellationRequested()` inside the loop instead of a silent exit.
- Acceptance: unit test cancels after the first batch of a 250-contribution campaign and asserts the handler throws (message stays pending).

### F11: Outbox lease (30 s) is shorter than a worst-case batch, and shutdown consumes retry attempts
- Severity: P2
- Where: BB/Outbox/ModuleOutboxProcessor.cs:30 (`LockDuration` 30 s), BatchSize 20 processed sequentially (:113-170) with one `SaveChangesAsync` at the end (:186); catch at :164 treats every exception, including `OperationCanceledException` on host stop, as a failed attempt (`MarkFailed`).
- Evidence: handlers run inline (advisory locks, concurrency retries with delays in AddContributionToCampaign); 20 slow messages can exceed 30 s, so a second worker reclaims rows still being processed (duplicate delivery; inbox is written after the handler, so side effects can repeat) and the first worker's final delete hits already-deleted rows (see F1). Each deployment that interrupts a batch adds an attempt; 5 restarts dead-letter a healthy message.
- Fix: renew the lease per message or save after each message; skip `MarkFailed` when the token is cancelled.
- Acceptance: test with a handler that sleeps longer than the lease asserts single delivery; test that cancelling mid-batch leaves `Attempts` unchanged.

### F12: Pledge accepts any reward-tier reservation id (not bound to backer or campaign)
- Severity: P2
- Where: M/Contributions/.../MakeContributionCommandHandler.cs (builds `Contribution.Create(..., command.RewardTierReservationId, ...)` with no check); M/Campaigns/.../AddContributionToCampaignCommandHandler.cs `ConfirmRewardTierReservationAsync` (checks only `Status == Reserved`, not `reservation.CampaignId == command.CampaignId` or the backer); release handlers ContributionPaymentFailedRewardTierHandler / ContributionRefundedRewardTierHandler act on the id carried by the event.
- Evidence: a pledge to campaign Y can confirm or, when it fails, release a reservation held by another backer or for campaign X; an expired reservation silently gives the backer no reward while the pledge is credited.
- Fix: verify campaign id and backer id on the reservation in the Campaigns handler and ignore (log) mismatches.
- Acceptance: test: pledge carrying another user's reservation id leaves that reservation Reserved and the tier count unchanged.

### F13: Audit trail covers one command; payloads include refresh tokens
- Severity: P2
- Where: only `[AuditableAction]` use is M/Campaigns/.../CancelCampaignCommand.cs:11; BB.Application/Audit/AuditLoggingPipelineBehavior.cs:55-68.
- Evidence: Approve/Reject review, AssignRole, GrantPermission, DeactivateUser, Confirm/Fail payment carry no attribute, so they are audited only when the actor is in role "Admin"; a Moderator or a user granted `identity.permissions.grant` leaves no trail. When an Admin does call refresh/logout the whole command is serialized: `RefreshAccessTokenCommand(string RefreshToken)` and `LogoutCommand(string RefreshToken)` put live refresh tokens into `audit_records.payload_json`.
- Fix: mark all permission-gated mutating commands `[AuditableAction]`; add a redaction attribute/allow-list for token fields.
- Acceptance: E2E: moderator approval creates an audit row; audit payload for logout contains no token value.

### F14: Default email provider keeps every email in an unbounded in-memory queue
- Severity: P2
- Where: M/Notifications/.../Services/IEmailNotificationSink.cs:36-42 (`ConcurrentQueue`, never trimmed); registered singleton in NotificationsInfrastructureDependencyInjection.cs:38; appsettings.json `Notifications:EmailProvider = "Logging"`; LoggingEmailNotificationService.cs:86 records every send.
- Evidence: the sink is meant for tests but is the production default and `SentMessages` copies the queue to an array on each read; memory grows with every notification.
- Fix: register the sink only in the test host, or cap the queue.
- Acceptance: unit test: after 10,000 sends the sink holds at most the configured cap.

### F15: Base appsettings and compose files disagree on the database that exists
- Severity: P2
- Where: src/API/CrowdFunding.API/appsettings.json (`DefaultConnection` -> `Database=crowdfundingdb`), services/CrowdFunding.Moderation.Service/appsettings.json (`ModerationDb` -> `crowdfundingdb`) vs docker-compose.yml (`POSTGRES_DB: postgres`, init script creates only `<module>_db`).
- Evidence: outside Development (no module keys) the monolith and service point at a database no compose file creates; only docker-compose.microservices.yml creates `crowdfundingdb`. Also appsettings.Development.json and appsettings.HA.json hard-code port 5432/6379 while compose uses `${CF_PG_PORT}` / `${CF_REDIS_PORT}`.
- Fix: make the base default match a database the standard compose creates (or document that Production must supply module strings); read ports from env in README.
- Acceptance: startup smoke in a non-Development environment with only compose.yml services up.

### F16: Raw SQL hard-codes schemas while the rest of the code reads them from the model
- Severity: P3
- Where: M/CampaignUpdates/.../WebhookDeliveryTaskRepository.cs:41,48 (`campaignupdates.webhook_delivery_tasks`); M/Campaigns/.../ContributionLedger.cs:35 (`campaigns.campaign_contributions_ledger`). OutboxClaimQuery.cs and EfInbox.cs read the schema from the EF model.
- Evidence: a schema rename would break these two statements at runtime only.
- Fix: resolve the qualified table name from the model as OutboxClaimQuery does.
- Acceptance: ModuleSchemaRulesTests-style test that renames nothing but asserts the statements run against a context configured with a different default schema.

### F17: Injected clock bypassed in several places
- Severity: P3
- Where: M/CampaignUpdates/.../Events/CampaignActivityEventHandlers.cs:32 and WebhookDispatcherBackgroundService.cs:59,108 (module has `ICampaignUpdatesDateTimeProvider`); M/Notifications/.../ReplicatedCampaignTitleEventHandler.cs:23 (`INotificationsDateTimeProvider` exists); M/Campaigns/.../ContributionLedger.cs:36; M/Campaigns/.../CreateCampaignCommandValidator.cs:37; Campaigns/Contributions/Moderation executors use `DateTime.UtcNow` for `OutboxMessage.Create` (CampaignTransactionExecutor.cs:186-198, ContributionTransactionExecutor.cs:141-160, ModerationTransactionExecutor.cs:98-101) although each module owns a provider.
- Evidence: tests that fake the module clock still get real timestamps in these rows. The outbox claim also uses `DateTime.UtcNow` (intentional, same application clock), so mixing a faked executor clock with the real claim clock would make due-date logic inconsistent if someone fixes only one side.
- Fix: inject the module provider (executors can take it via constructor); keep the outbox claim and writer on the same source.
- Acceptance: unit test with a fixed provider asserts `OccurredOnUtc`/`UpdatedAtUtc` equal the fake time.

### F18: Index and table naming conventions are mixed
- Severity: P3
- Where: snapshots: `ix_*` lower snake (outbox, `ix_active_campaigns_cache_lookup`), `uq_campaign_contributions_ledger_contribution_id`, `UX_contributions_ContributorId_IdempotencyKey` (PascalCase), EF defaults `IX_...` elsewhere; `identity_signing_keys` is the only table carrying a module prefix inside its schema (others: `users`, `refresh_tokens`), while inbox/outbox tables carry the prefix in all modules.
- Evidence: Contributions/Domain/Aggregates/ContributionIndexes.cs defines the idempotency index name; the executor matches it by name, so renaming for style would break the translation unless both change together.
- Fix: pick one convention for hand-named indexes and add an architecture test listing allowed names; rename `identity_signing_keys` -> `signing_keys` with a migration if desired.
- Acceptance: test that scans the model for `HasDatabaseName` values and matches the regex.

### F19: Dead or stale code and comments
- Severity: P3
- Where: `CompleteCampaignCommandValidator` and `FailCampaignCommandValidator` exist but are not registered in CampaignsApplicationDependencyInjection (only five Campaigns + two RewardTier validators are); M/Moderation DI registers handlers explicitly although `AddRequestHandlersFromAssemblies` already scans them (and omits `ListCampaignReviewsQueryHandler`, which works only because of the scan); src/API/.../Program.cs TICKET-036 comment says "(when wired up) account deactivation" but DeactivateUserCommandHandler already revokes the stamp; the BOM + duplicated `using` blocks at the top of Campaigns/Contributions/Moderation DI files.
- Evidence: see files; no behaviour impact today.
- Fix: delete or register; update comments.
- Acceptance: build with `IDE0005` as warning and a test that every `AbstractValidator<T>` is registered.

### F20: Test names that promise more than they check
- Severity: P3
- Where: tests/IntegrationTests/CrowdFunding.IntegrationTests/ModuleSchemaRulesTests.cs:`EveryModuleTable_LivesInItsModuleSchema_NotInPublic` (builds its own DbContextOptions with the history schema instead of using the production `Add*Infrastructure` registration, so a DI mistake like a wrong `MigrationsHistoryTable` schema passes; Audit and Rosetta contexts are not migrated); ModerationServiceExtractionSmokeTests.cs runs the service with `Messaging:Provider=InProcess`, so it cannot detect F4 and seeds the review by hand, and its only authorization case is Admin (nothing proves a non-moderator is refused, see F3).
- Fix: build options through the real DI extension; add a non-moderator case and a RabbitMQ-mode case.
- Acceptance: tests fail when the production registration is changed to the wrong schema or when the service omits the policy.

---
Notes on items checked and not reported
- Notifications, CampaignUpdates and Identity have no outbox/signal by design (documented in their DbContexts); Identity has no inbox because it has no event handlers.
- The owner-delete 500 on CampaignUpdates is the known gap; I found no second HTTP path with the same cause, the other path (dispatcher retry) is covered by F1.
- Dispatcher and outbox "inbox written after handler", "inbox never pruned" are known gaps and not repeated.
