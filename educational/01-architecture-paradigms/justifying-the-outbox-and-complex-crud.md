# Justifying the Outbox and Complex CRUD: External Side Effects

> **Audience:** senior engineers, architects, tech leads
> **Question:** when is a transactional outbox necessary, and when is it overhead?

---

## 1. The objection

A common critique of DDD and Clean Architecture in a monolith:

> *Why write events to an outbox table and run a background worker with `FOR UPDATE SKIP LOCKED` just to update a row in the same PostgreSQL database that holds the other module's tables?*

The critique is fair for purely internal updates. When caller and callee share a database, one transaction can do the work with less code and less latency.

It stops being fair as soon as a state change must be coordinated with a system that cannot take part in the PostgreSQL transaction:
1. E-mail and SMS providers.
2. Serverless functions doing slow work (media analysis, transcoding, scanning).
3. Payment providers and their webhooks.
4. Outbound webhooks to customer systems.
5. Message brokers, analytics and audit sinks.

---

## 2. The dual-write hazard

### Anti-pattern A: external call before commit
```csharp
await _emailService.SendReceiptEmailAsync(backer.Email, contribution.Amount);
await _dbContext.SaveChangesAsync(); // may throw: deadlock, constraint violation, lost connection
```
**Ghost side effect:** the backer gets a receipt for a pledge the database never recorded.

### Anti-pattern B: external call after commit
```csharp
await _dbContext.SaveChangesAsync();
await _emailService.SendReceiptEmailAsync(backer.Email, contribution.Amount); // may fail or never run
```
**Lost side effect:** the pledge is recorded, but a crash, timeout or rate limit loses the e-mail, with nothing left to retry it.

```mermaid
sequenceDiagram
    autonumber
    participant API as API
    participant DB as PostgreSQL
    participant Ext as External API

    API->>DB: BEGIN; UPDATE contribution
    API->>Ext: send e-mail
    Note over Ext: e-mail delivered
    API->>DB: COMMIT
    Note over DB: fails: transaction rolled back
    Note over API,Ext: the customer holds a receipt for a record that does not exist
```

The fix is to record the intent to cause the side effect in the same transaction as the state change (the outbox row), and let a worker perform the side effect after commit, with retries. Delivery is then at-least-once, so the receiving side must be idempotent: either through the receiver's own idempotency key or through an inbox table on our side.

---

## 3. Five features in this codebase that need it

### 3.1 Transactional e-mail
- **Need:** backers and creators get e-mails when a pledge is confirmed, refunded, or a campaign changes state.
- **How:** the Contributions and Campaigns application events are written to their module's outbox in the same transaction as the change. The Notifications module handles them after dispatch and sends through `IEmailNotificationService` with a stable idempotency key (sent as `X-Message-Id`). Covered by `EmailNotificationOutboxE2ETests`.
- **Limit:** exactly-once is not guaranteed across a crash between the provider accepting the e-mail and our commit; the provider's idempotency key is what prevents duplicates.

### 3.2 Asynchronous media analysis (serverless callback)
- **Need:** automated media-safety analysis takes seconds and must not run inside `POST /api/campaigns`.
- **How (implemented part):** the analysis result arrives later at `POST /api/webhooks/moderation/media-analysis`, authenticated by an HMAC signature over the raw body (`X-Cloud-Signature`). `CampaignReview.RecordAutomatedMediaAnalysis` stores the scores; if the analysis passed the safety check it auto-approves the review, otherwise the review stays `Pending` for a human moderator, who now sees the scores. If a moderator already acted, the callback is a no-op. Covered by `MediaAnalysisWebhookE2ETests`.
- **Not implemented:** the outbound trigger that would send new media to the function; the codebase handles only the callback.

```mermaid
sequenceDiagram
    autonumber
    participant Creator
    participant API as Monolith
    participant CF as Analysis function (external)
    participant Review as CampaignReview

    Creator->>API: POST /api/campaigns
    API-->>Creator: 201 Created (review Pending)
    Note over API,CF: trigger to the function: not implemented here
    CF->>API: POST /api/webhooks/moderation/media-analysis (HMAC signed)
    API->>Review: RecordAutomatedMediaAnalysis(...)
    Note over Review: passed: Approved; otherwise stays Pending with scores
```

### 3.3 Payment webhook reconciliation
- **Need:** payments complete asynchronously. The provider's webhook can arrive before the browser returns, and providers redeliver events.
- **How:** `POST /api/webhooks/payments/stripe` verifies the `Stripe-Signature` HMAC, then reconciles the event with the contribution. The `Contribution` aggregate enforces its state machine (`Pending -> Succeeded` or `Failed`, later `Refunded`), and the inbox (`IIdempotentConsumer.ExecuteOnceAsync`, keyed by the provider's event id) claims the event with `INSERT ... ON CONFLICT DO NOTHING` inside the same transaction as its effect, so a redelivery is acknowledged and ignored. The resulting application event goes through the Contributions outbox to the Campaigns ledger. Covered by `PaymentGatewayWebhookReconciliationE2ETests` and `InboxIdempotencyE2ETests`.

### 3.4 Reward-tier inventory
- **Need:** "only 50 at this price" tiers under bursts of concurrent reservations.
- **How:** `ReserveRewardTierSlotCommandHandler` serializes reservations per tier with `pg_advisory_xact_lock`, so `ClaimedCount + ReservedCount <= TotalCapacity` holds exactly; an `xmin` concurrency token is a second line of defense. A reservation expires after 15 minutes (`RewardTierReservation.ReservationWindow`), a background scavenger releases expired ones, and failed or refunded payments release their slot through event handlers. Covered by `RewardTierConcurrencyE2ETests`.
- This is the "complex CRUD" case: no external system, but a rich aggregate and explicit locking are justified by the invariant.

### 3.5 Outbound creator webhooks
- **Need:** creators register endpoints that receive `pledge.confirmed` events.
- **How:** the CampaignUpdates module consumes the pledge-confirmed event through the inbox and writes `webhook_delivery_tasks` rows. `WebhookDispatcherBackgroundService` claims due rows with `FOR UPDATE SKIP LOCKED`, signs each payload with HMAC-SHA256 in `X-CrowdFunding-Signature` (`t={unix},v1={hex}` over `"{t}.{body}"`), retries on a back-off schedule and marks a task `Dead` after the last attempt. The untrusted URL is never called from a request thread. Delivery is at-least-once, so receivers must deduplicate. Covered by `WebhookSubscriptionE2ETests`.

---

## 4. Decision matrix

| Integration | Target | Dual-write hazard | Pattern | Reason |
| :--- | :--- | :---: | :--- | :--- |
| Same-module write | Same module's tables | No | Direct EF Core write | One ACID transaction |
| Simple cross-module effect | Same database | Low | Direct call or read projection, unless another module must react reliably | An outbox adds latency and moving parts |
| E-mail / SMS | Provider API | Yes | Outbox + provider idempotency key | Lost or ghost messages otherwise |
| Serverless compute | Function endpoint | Yes | Outbox for the trigger, signed callback for the result | Long-running work does not belong in a request |
| Payment webhooks | Our endpoint, called by the provider | Redeliveries | Signature check + inbox + aggregate state machine | Duplicate events must not double-apply |
| Outbound webhooks | Customer URLs | Yes | Delivery-task table + background dispatcher + dead state | Slow or hostile URLs must not block callers |
| Events to other processes | Message broker | Yes | Outbox (polling today; CDC considered) | Broker and database can diverge otherwise |

---

## 5. Summary

> If rolling back the database transaction also undoes the side effect, keep it in the transaction. If the side effect leaves the database (HTTP, SMTP, a function, a broker), record it in an outbox or task table in the same transaction and perform it afterwards, and make the receiving side idempotent.
