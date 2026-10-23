# Design-to-Code Mapping: Round 1 (Crowdfunding Platform)

> **Purpose:** show a learner, step by step, where each Round 1 system-design decision lives in the `CrowdFundingHub` code, which test guards it, and where the code differs from the design.
> **Design source:** `hispeedsystem/system-design-mastery/interview-loops/04-financial-and-transactional/06-crowdfunding-platform.md` (Round 1, steps 1.0 to 1.6, plus "See It Built").
> **Method:** read from the code and tests, then re-checked after the inbox, schema and outbox changes (section 6). Line numbers drift as the code changes, so trust file and symbol names over numbers. Test names come from the test sources.
> **Status tags:** **MATCHES** (code implements the design), **SIMPLER VARIANT** (a smaller version of the design), **NOT BUILT** (no code).

Paths are relative to the repo root. `CAMP` = `src/Modules/Campaigns/CrowdFunding.Modules.Campaigns`, `CONT` = `src/Modules/Contributions/CrowdFunding.Modules.Contributions`, `NOTIF` = `src/Modules/Notifications/CrowdFunding.Modules.Notifications`, `BB` = `src/BuildingBlocks/CrowdFunding.BuildingBlocks`.

## 1. Summary

| Design step | Module | Status | Guarded by |
|---|---|---|---|
| 1.1 Where the money sits for 30 days | Contributions | NOT BUILT (charge-now state model only, payment gateway is a mock) | `ContributionDomainTests` |
| 1.2 Double-click creates two pledges | Contributions | MATCHES (optional `Idempotency-Key` header, unique index per backer and key) | `PledgeIdempotencyTests`, `PledgeIdempotencyE2ETests` |
| 1.3 The total on the page vs the pledges | Campaigns | SIMPLER VARIANT (idempotency table plus a locked counter) | `CampaignContributionConcurrencyTests` |
| 1.4 Limited tier oversold | Campaigns | MATCHES (lock, invariant, 15-minute hold); no database CHECK | `RewardTierConcurrencyE2ETests` |
| 1.5 Pledge after the deadline | Contributions, Campaigns | NOT BUILT (no closing fence) | partial |
| 1.6 Settlement at the deadline | Campaigns, Contributions | SIMPLER VARIANT (decision and state-only refunds); charge-once settlement NOT BUILT; webhook inbox MATCHES | `CampaignExpirationE2ETests`, `RefundSagaE2ETests`, webhook tests |
| Outbox, event registry, dead letter | BuildingBlocks | MATCHES | `EventTypeRegistryTests`, `OutboxMessageTests`, `OutboxLeanTableE2ETests` |
| Consumer inbox | all consumers | MATCHES in part (per-module inbox; written after the handler, so a crash window remains) | `InboxRetryTests` |
| "Tell people" | Notifications | SIMPLER VARIANT | `EmailNotificationOutboxE2ETests` |

## 2. The two flows, drawn

### Create a campaign (the light path)

```mermaid
sequenceDiagram
    participant C as CampaignsController
    participant D as CommandDispatcher
    participant H as CreateCampaignCommandHandler
    participant A as Campaign (Domain)
    participant R as CampaignRepository
    participant T as CampaignTransactionExecutor
    participant O as Outbox
    C->>D: SendAsync(CreateCampaignCommand)
    D->>H: Handle (after audit behavior)
    H->>A: Campaign.Create(...)
    A-->>H: Draft campaign + CampaignCreated event
    H->>T: execute in transaction
    T->>R: AddAsync(campaign)
    T->>O: write outbox row (same transaction)
    T-->>H: commit
```

### Make a contribution (the heavy path)

```mermaid
sequenceDiagram
    participant C as ContributionsController
    participant H as MakeContributionCommandHandler
    participant K as Contribution (Domain)
    participant W as Payment webhook
    participant O as Contributions outbox
    participant E as Campaigns event handler
    participant L as Campaign ledger and counter
    C->>H: POST contributions
    H->>H: check replicated cache, deadline, currency
    H->>K: Contribution.Create (Pending)
    W->>K: ConfirmPayment (Succeeded)
    K->>O: ContributionPaymentConfirmed (outbox)
    O->>E: publish
    E->>L: insert ledger row, add to RaisedAmount (advisory lock)
```

## 3. Step by step

### Step 1.1: where the money sits for 30 days: NOT BUILT

- **Design:** save the card at pledge time, charge at the deadline only if funded.
- **Code:** a pledge is a `Pending` row with a mock payment intent. Confirmation arrives by webhook or a manual endpoint. No payment provider is called anywhere in `src/`.
- **Where:** `CONT.Application/Features/Contributions/Commands/MakeContribution/MakeContributionCommandHandler.cs:37-94` (mock intent at line 86); `CONT.Domain/Aggregates/Contribution.cs` (`Create` 64-97, `ConfirmPayment` 126-144, `Refund` 179-190); `ContributionStatus` is Pending, Succeeded, Failed, Refunded.
- **Tests:** `ContributionDomainTests`, `MakeContributionCommandHandlerTests`, `ContributionAndOutboxE2ETests`.
- **Why it differs:** the code uses the charge-now model the design rejects, a deliberate simpler choice that the design's own "See It Built" section acknowledges.

### Step 1.2: double-click creates two pledges: MATCHES

- **Design:** an idempotency key or client-supplied pledge ID makes a retry return the first pledge.
- **Code:** `POST /api/campaigns/{id}/contributions` accepts an optional `Idempotency-Key` header (1 to 100 printable ASCII characters). The key is stored on the `Contribution` and a unique index on `(ContributorId, IdempotencyKey)` where the key is not null decides races (`ContributionConfiguration.cs`, `ContributionIndexes.IdempotencyKey`). `MakeContributionCommandHandler` looks the key up first, before any campaign check, so a retry after the deadline still gets the first pledge. A retry returns `200` with `Idempotent-Replayed: true`; the same key with a different amount, currency, campaign or reward reservation is a `409`; simultaneous requests are settled by the index and the losers read the winner. `ContributionTransactionExecutor` turns that specific unique violation into `IdempotencyKeyAlreadyUsedException`.
- **Differences from the design:** the key is optional, so a client that sends none still gets a new pledge each time; keys are scoped to the backer, not global; there is no expiry on stored keys.
- **Tests:** `PledgeIdempotencyTests` (unit: replay, mismatch, retry after the campaign closed, lost race) and `PledgeIdempotencyE2ETests` (twelve simultaneous requests create one pledge; mismatch is a conflict; two backers may share a key; a malformed key is rejected).

### Step 1.3: the total vs the pledges: SIMPLER VARIANT

- **Design:** an append-only ledger. The total is derived from it.
- **Code:** a table `campaign_contributions_ledger` with a unique index on `ContributionId` only; insert is `ON CONFLICT DO NOTHING` (`CAMP.Infrastructure/Persistence/Repositories/ContributionLedger.cs:22-42`). The total is a running counter, `Campaign.RaisedAmount`, added to in the same transaction under a per-campaign `pg_advisory_xact_lock` (`CampaignTransactionExecutor.cs:72-82`, `Campaign.cs:182-198`), with an `xmin` token and three retries as a second guard.
- **Not present:** an entry kind, a sign check constraint, an update/delete trigger, negative refund entries, and a total computed as a ledger sum. Append-only is by convention.
- **Tests:** `CampaignContributionConcurrencyTests.ConcurrentPledges_ShouldNotLoseUpdates`, `RedeliveredContribution_ShouldBeAppliedOnlyOnce`; `CampaignDomainTests.ApplyConfirmedContribution_*`.

### Step 1.4: limited tier oversold: MATCHES

- **Design:** serialise the claim of a limited slot, hold it for a short time.
- **Code:** a per-tier advisory lock serialises read-check-increment. The aggregate enforces `claimed + reserved <= capacity` (`CAMP.Domain/Aggregates/RewardTier.cs`, `ReserveSlot` 82-92). A reservation lasts 15 minutes (`RewardTierReservation.cs:63`), becomes a claim when payment confirms, and is released by a failure handler or a scavenger (`RewardTierReservationScavengerBackgroundService.cs`).
- **Gaps:** no database CHECK for the invariant (it lives only in code under the lock), the reservation is not tied to card setup, and nothing checks that a reservation belongs to the same backer and campaign.
- **Tests:** `RewardTierConcurrencyE2ETests.FiftyConcurrentReservations_AgainstFiveRemainingSlots_ShouldYieldExactlyFiveSuccesses`; `RewardTierTests`. The scavenger has no test.

### Step 1.5: pledge after the deadline: NOT BUILT (closing fence)

- **Design:** a closing fence so no pledge is accepted after the deadline and none that was accepted is left out of the funded decision.
- **Code:** the deadline is checked in application code at pledge time (`MakeContributionCommandHandler.cs:50`) against a replicated cache. The confirm path checks only that the campaign is active; the credit path checks only `Status == Published`. There is no database enforcement and no ENDED state.
- **Consequence (from reading the code, untested):** a payment confirmed after the campaign turned Successful or Failed makes `ApplyConfirmedContribution` throw. The message is retried, then dead-lettered, and the money is not refunded.
- **Tests:** `Handle_ShouldThrow_WhenCampaignCannotAcceptContributions`; the webhook-after-cancellation test. No test of the closing race.

### Step 1.6: settlement at the deadline: SIMPLER VARIANT

```mermaid
flowchart TD
    A[CampaignExpirationBackgroundService polls every minute] --> B{RaisedAmount >= GoalAmount?}
    B -- yes --> C[CompleteCampaign under campaign lock]
    B -- no --> D[FailCampaign under campaign lock]
    D --> E[CampaignFailed event via outbox]
    E --> F[CampaignTerminationRefundHandler]
    F --> G[Mark Succeeded contributions Refunded, batches of 100]
```

- **Decider:** compares the stored counter with the goal (`CampaignExpirationBackgroundService.cs:76`). The read happens before the lock; the handlers re-validate under it.
- **Refund saga:** state only. It marks contributions Refunded and never calls a payment provider (`CONT.Application/Features/Contributions/Events/CampaignTerminationRefundHandler.cs`).
- **NOT BUILT:** charging each pledge exactly once at settlement (no settlement jobs, no charge attempts, no payment-provider idempotency keys, no charge states).
- **Webhook inbox: MATCHES.** Signature check, stale-timestamp rejection and dedupe by event ID (`PaymentWebhooksController.cs`, `StripeWebhookSignatureVerifier.cs`, `PaymentWebhookIdempotencyStore.cs`, table `processed_payment_webhooks`). The check and insert are a SELECT then an INSERT in one transaction, with the primary key as the backstop.
- **Tests:** `CampaignExpirationE2ETests`, `RefundSagaE2ETests`, `CampaignTerminationRefundHandlerTests` (idempotent on redelivery, batches), the webhook reconciliation tests.

### Cross-cutting: the outbox: MATCHES

- **Write:** each module's transaction executor maps domain events to outbox rows in the same transaction (`CampaignTransactionExecutor.cs:57-148`, `ContributionTransactionExecutor.cs`).
- **Drain:** `BB.Infrastructure/Outbox/ModuleOutboxProcessor.cs`, one worker for each module that has an outbox (Campaigns, Contributions, Moderation). Claim query uses `FOR UPDATE SKIP LOCKED` (`OutboxClaimQuery.cs`); batch 20, five attempts, then dead letter.
- **Registry and bus:** `EventTypeRegistry` keyed by event name and version; in-process bus by default, RabbitMQ optional.
- **Crash recovery:** the claim also takes `Processing` rows whose lease has run out, writes the lease from the application clock, and counts a reclaim as an attempt so a message that keeps killing its worker is dead-lettered (`OutboxReclaimE2ETests`).
- **Tests:** `EventTypeRegistryTests`, `OutboxMessageTests`, `OutboxLeanTableE2ETests`, `ContributionAndOutboxE2ETests`, `RabbitMqMessageBusTests`.

### Cross-cutting: consumer inbox: MATCHES IN PART

> A per-module inbox now exists: table `<module>_inbox_messages`, keyed by message ID and handler, written after the handler succeeds (`InboxRegistration.cs`, `EfInbox.cs`, `ServiceProviderEventPublisher.cs`; test `InboxRetryTests`). It is used by the in-process outbox dispatch only; the RabbitMQ path does not set a message ID. The record is written after the handler, so a crash between the two re-runs the handler once. The text below describes the code before that change and still explains why handlers must stay idempotent.

Before the inbox existed, there was no inbox type and no per-consumer processed-event table, and consumers relied on natural state checks, which still back the inbox up: the ledger unique index, `Contribution.Refund` only from Succeeded, reservation status checks, and upserts in the replicated cache. Handlers run one after another inside one publish, so a failure in a later handler re-runs the earlier ones.

### "Tell people": SIMPLER VARIANT

Receipts and refund or cancellation alerts are built (`NOTIF.Application/Events/NotificationEventHandlers.cs`, idempotency through an `X-Message-Id` header). Handlers for published, cancelled and review events are empty stubs, and no email is sent when a campaign succeeds or fails, so a backer is told about a refund but never that the campaign was funded.

## 4. Where the "See It Built" section overstates the code

The loop's "See It Built" section describes the reference code ahead of the code itself. These claims were not supported by the code when this page was first written, and should be corrected there (or the code brought up to them):

1. "Inboxes that make every consumer idempotent": a per-module inbox now exists, but it is written after the handler, covers only the in-process path, and is not pruned.
2. "A unique key on contribution and entry kind": the unique index is on `ContributionId` alone.
3. "A check constraint ties each entry's sign to its kind": not found.
4. "A trigger rejects updates and deletes": not found.
5. "Refunds are negative compensating entries": refunds change contribution status and never touch the ledger or decrement `RaisedAmount`.
6. "Recomputes the raised total as a sum over the ledger": the total is an incremental counter.

## 5. Gaps this mapping exposes

> Note: the schema-per-module row below was found while checking a lesson. It was checked statically (migrations and entity configurations) and has not been confirmed against a live database.

| Gap | Design step | Suggested ticket |
|---|---|---|
| ~~No idempotency on pledge creation~~ **Closed:** `Idempotency-Key` header with a unique index per backer and key | 1.2 | Done |
| No closing fence; late confirmations dead-letter without refund | 1.5 | Add an ENDED state and refund late confirmations |
| ~~Outbox rows stuck in `Processing` are never reclaimed~~ **Closed:** reclaim on lease expiry, lease on the application clock, attempts bounded | outbox | Done |
| Ledger lacks entry kind, sign check, trigger, refund entries | 1.3 | Decide whether to build it or change the design text |
| ~~Module tables are not in per-module schemas~~ **Closed:** every module has `HasDefaultSchema`, a `MoveTablesTo<Module>Schema` migration and its own history table, checked by `ModuleSchemaRulesTests` against a real database. Each module also now gets its own database in the local stack | Cross-cutting (schema per module) | Done |
| No settlement charge-once mechanism | 1.6 | Out of scope while charge-now stands; document the choice |
| No success/failure outcome email | "Tell people" | Add handlers or remove the stubs |
| Scavenger untested (outbox crash recovery is covered by `OutboxReclaimE2ETests`) | 1.4 | Add a test |

## 6. What changed since the first reading

```mermaid
flowchart LR
    A["First reading"] --> B{"Re-checked against main"}
    B -->|changed| C["Inbox built<br/>Schemas built<br/>Outbox due-clock bug fixed<br/>Database per module locally"]
    B -->|still true| D["No closing fence<br/>Ledger has no kind, sign check, trigger<br/>Success/failure email stubs"]
    B -->|drifted| E["Line numbers only"]
```

- **Outbox stuck rows (new, fixed).** A row left in `Processing` by a crashed worker is now reclaimed once its lease runs out, and the lease is written from the application clock.
- **Outbox due-clock (new, fixed).** The claim query compared an application-set `ScheduledAtUtc` with the database's `now()`; a database clock slightly behind made just-queued rows unclaimable. It now compares against the application clock. 
- **Pledge idempotency (new, built).** See step 1.2.
- **Closing fence, ledger shape, success/failure email (still true).** Searches for check constraints, triggers and ledger entry kinds found nothing. The notification handlers for published, cancelled and review events are still `Task.CompletedTask`.
- **Webhook subscriptions (new).** A stale background save could re-activate a deleted subscription; fixed with an `xmin` token and covered by `WebhookSubscriptionConcurrencyTests`. It also fixed the tier assignment for that feature (stays Tier 3).
- **Tests added since the first reading:** `InboxRetryTests`, `ModuleSchemaRulesTests`, `NotificationPreferencesTier2Tests`, `WebhookSubscriptionConcurrencyTests`. See `docs/TESTS_THAT_TEACH.md`.
- **Still untested:** the reservation scavenger. Outbox crash recovery is covered by `OutboxReclaimE2ETests`.
