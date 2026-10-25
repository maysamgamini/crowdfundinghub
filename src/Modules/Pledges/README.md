# Pledges Module

## Purpose
The Pledges module manages backer pledges, payment confirmation, refunds when a campaign fails, and usage metering. The payment gateway is a mock; no real provider is called.

## Pledge lifecycle

```mermaid
stateDiagram-v2
    [*] --> Pending: MakePledge
    Pending --> Succeeded: ConfirmPledgePayment (webhook or manual)
    Pending --> Failed: FailPledgePayment
    Succeeded --> Refunded: campaign failed or cancelled
    Failed --> [*]
    Refunded --> [*]
```

## A pledge, end to end

```mermaid
sequenceDiagram
    participant B as Backer
    participant H as MakePledgeHandler
    participant C as Pledge
    participant G as Payment webhook
    participant O as Pledges outbox
    participant Ca as Campaigns
    B->>H: POST /api/campaigns/{campaignId}/pledges
    H->>H: check deadline and currency against the replicated campaign cache
    H->>C: Create (Pending) + mock payment intent
    G->>C: ConfirmPayment (Succeeded), signature and event-id checked
    C->>O: PledgePaymentConfirmed, same transaction
    O->>Ca: outbox worker publishes
    Ca->>Ca: credit ledger and raised total
```

## Capabilities
- **Commands:** `MakePledge`, `ConfirmPledgePayment`, `FailPledgePayment`, `ReconcilePaymentWebhook`.
- **Queries:** `GetPledgeById`, `ListPledgesByCampaign`.
- **Event handlers:** `PledgeConfirmedMeteringEventHandler` (platform fee and CloudEvent to OpenMeter), `CampaignTerminationRefundHandler` (refunds `Succeeded` pledges, 100 at a time, each through the payment provider), and replicated campaign read-model handlers.
- **Webhook guard:** signature check, stale-timestamp rejection and dedupe by event id in `processed_payment_webhooks`.
- **Concurrency:** `xmin` token on `Pledge`, plus a unique key on `(BackerId, IdempotencyKey)` for retried pledges. The advisory lock that serialises crediting a campaign sits in Campaigns.

## Who can read a pledge

A pledge shows who backed what and its payment references, so the read endpoints need a token and are scoped by role.

| Caller | List pledges of a campaign | One pledge |
|---|---|---|
| No token | 401 | 401 |
| The backer | only their own | their own, in full |
| Another backer | empty list | 404 (the same answer as for an id that does not exist) |
| The campaign's owner | all, without payment references | without payment references or payment intent |
| A payments manager | all, in full | in full |

The campaign's owner comes from Pledges' own replicated copy of the campaign, so the check needs no call into Campaigns. The rules live in the query handlers (`PledgeVisibility`), so they hold whichever host serves the module.

## Safe retries

`POST /api/campaigns/{id}/pledges` accepts an optional `Idempotency-Key` header. A retry with the same key returns the first pledge (`200`, `Idempotent-Replayed: true`); the same key with a different request is a `409`; a unique index on (backer, key) decides simultaneous requests.

```mermaid
sequenceDiagram
    participant B as Backer
    participant H as MakePledgeHandler
    participant Db as pledges.pledges
    B->>H: POST with Idempotency-Key k
    H->>Db: pledge for (backer, k)?
    alt found, same request
        Db-->>H: first pledge
        H-->>B: 200 Idempotent-Replayed
    else found, different request
        H-->>B: 409 Conflict
    else not found
        H->>Db: INSERT (unique on backer and key)
        alt lost the race
            Db-->>H: unique violation
            H->>Db: read the winner
            H-->>B: 200 Idempotent-Replayed
        else inserted
            H-->>B: 201 Created
        end
    end
```

A retention task clears a pledge's idempotency key after `Retention:IdempotencyKeyDays` (7 by default), so the key only protects a genuine retry window and the unique index does not carry keys for ever; the pledge itself is untouched.

**One live pledge per backer per campaign.** A partial unique index on `(CampaignId, BackerId)` for `Pending` and `Succeeded` pledges, plus a check in the handler, refuses a second live pledge with `409`; it ends when the pledge fails or is refunded. This is what stops a backer with two tabs and two idempotency keys.

**Late confirmations are refunded.** A payment confirmed after its campaign ended is refused by Campaigns inside the campaign lock (`LatePledgeRejected`), and `LatePledgeRefundHandler` refunds the pledge.

**Refunds go through the payment provider.** `PledgeRefunder` calls `IPaymentProvider.RefundAsync` outside the database transaction, then marks the pledge `Refunded` and keeps the provider's reference. The idempotency key is per refund attempt (`pledge:<id>:refund:<n>`): a retry reuses it, so a crash between the provider call and the status change cannot refund twice, and the number goes up only after the provider answered "no". The provider is `FakePaymentProvider`, which replays the stored answer for a repeated key like a real one.

**Not built:** saving a card at pledge time, charge-once settlement at the deadline, creator payouts and a real payment provider. See `educational/03-engineering-spec-and-roadmap/design-to-code-mapping-round1.md`.

## Data and messaging

| | |
|---|---|
| Database (local stack) | `pledges_db` |
| PostgreSQL schema | `pledges`, with its own `__EFMigrationsHistory` |
| Poly-pattern tier | 3 (money, xmin, unique-key webhook guard, events) (see `docs/POLY_PATTERN_TIER_ASSIGNMENT.md`) |
| Outbox | `pledges` outbox, drained by `PledgesOutboxBackgroundService` |
| Inbox | `pledges_inbox_messages` |

## Projects & Layers
- `Domain`: `Pledge` aggregate root, `PledgeStatus`, domain events.
- `Application`: command, query and event handlers.
- `Infrastructure`: `PledgesDbContext`, repositories, transaction executor, webhook idempotency store, outbox worker.
- `Contracts`: `PledgePaymentConfirmedApplicationEvent` and related contracts.
