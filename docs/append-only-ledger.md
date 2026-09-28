# The Append-Only Funding Ledger: Database-Enforced vs. Code-Enforced Invariants

A campaign's raised amount is money. Two rules must hold under retries, redeliveries, concurrent requests and future code changes:

1. A confirmed contribution is counted exactly once; a refund removes exactly what that contribution added.
2. The raised amount always equals what the contributions and refunds add up to.

## Before: a mutable total guarded by code

```
confirmed event ──▶ handler
                     ├─ pg_advisory_xact_lock(campaign)          serialize writers
                     ├─ INSERT campaign_contributions_ledger     dedupe table (ContributionId unique)
                     │    ON CONFLICT DO NOTHING
                     ├─ campaign.RaisedAmount += amount           read-modify-write in C#
                     ├─ UPDATE campaigns ... WHERE xmin = @old    optimistic concurrency
                     └─ retry loop on concurrency conflict
```

Every rule lived in application code. The dedupe table only recorded *that* a contribution had been seen; the number that mattered, `RaisedAmount`, was a counter any code path could overwrite. Refunds never reduced it. Nothing but review discipline stopped a new command, a migration script or a hand-run SQL statement from writing a wrong total, and nothing could reconstruct the right one afterwards.

## After: an append-only ledger, a derived snapshot, and invariants in PostgreSQL

```
confirmed event ──▶ AddContributionToCampaignCommandHandler
                     ├─ pg_advisory_xact_lock(campaign)          still the single serialization point
                     ├─ campaign.IsAcceptingContributions?        cancelled/completed ⇒ Credited = false
                     ├─ entry = campaign.RecordPledge(...)        domain rules: status, amount > 0, currency
                     └─ CampaignLedger.AppendAsync(entry)
                          ├─ INSERT campaigns.campaign_ledger_entries ... ON CONFLICT (ContributionId, Kind) DO NOTHING
                          └─ UPDATE campaigns.campaigns SET raised_amount = SUM(entries) RETURNING raised_amount, xmin

refunded event ──▶ RecordContributionRefundCommandHandler
                     └─ same path with campaign.RecordRefund(...): a negative entry, inserted only
                        if a pledge entry exists for that contribution
```

| Invariant | Enforced by |
| :--- | :--- |
| A contribution is credited at most once, refunded at most once | Unique index `(ContributionId, Kind)`; `INSERT … ON CONFLICT DO NOTHING` |
| A refund reverses only a credited pledge | The refund INSERT's own `WHERE EXISTS (pledge)` clause |
| Pledges are positive, refunds negative, only the opening balance lacks a contribution | CHECK constraint `ck_campaign_ledger_entries_kind_sign` |
| History is never edited | Trigger `campaign_ledger_entries_append_only` rejects UPDATE and DELETE |
| `raised_amount = SUM(entries)` | Trigger `campaigns_raised_amount_on_update` / `_on_insert` rejects any other value, from EF Core, a script or psql |
| Only published campaigns accept funds | The `Campaign` aggregate (`RecordPledge`), checked under the advisory lock |

The recompute is `SUM`, never `+=`, so even the one statement allowed to write the snapshot cannot drift from the ledger. `CampaignLedgerTests` proves the triggers fire for an ORM write and a raw SQL write alike.

## Why the lock is still needed

The unique key makes duplicates harmless, but two different questions still need an order:

- **Cancel vs. pledge.** A pledge must not be credited after a cancellation commits. Both take the same per-campaign advisory lock and reload the campaign inside it, so the pledge either commits before the cancel (and is later refunded) or sees `Cancelled` and credits nothing.
- **The snapshot sum.** Under READ COMMITTED, two concurrent appends could each compute `SUM` without the other's uncommitted entry. Holding the lock across append + recompute means each `SUM` sees every committed entry.

What went away is the optimistic-concurrency retry loop in the credit path: nothing reads a total, adds to it in C#, and writes it back any more.

## Trade-offs

- **Writes per credit:** one INSERT plus one UPDATE with a `SUM` over the campaign's entries (indexed by `CampaignId`). Fine for crowdfunding volumes; a campaign with millions of entries would switch the recompute to `raised_amount + NEW.Amount` inside the trigger, or keep a running total in a separate snapshot row.
- **Logic in the database:** constraints and triggers are code too. They live in a reviewed EF migration, are covered by integration tests, and are what makes the rule hold for every client, including the ones this codebase does not control.
- **Hot row:** every credit updates the campaign row, serialized by the advisory lock. A viral campaign is bounded by that lock's throughput (thousands per second on PostgreSQL); beyond that, batch credits or shard the snapshot.
- **Ordering assumption:** a refund that arrives before its pledge credit is a no-op; if the pledge is later credited (only possible while the campaign is still published), it stays counted. In this system refunds only happen after cancellation or failure, when credits are already refused.
- **Migration:** existing credits become pledge entries, and any unexplained remainder of a campaign's `raised_amount` becomes one `OpeningBalance` entry, so the invariant holds from the first moment (`CampaignLedgerMigrationTests`). The down migration keeps pledges but cannot represent refunds or opening balances in the old table.

## Where to look

- Domain: `Campaign.RecordPledge`, `Campaign.RecordRefund`, `CampaignLedgerEntry`, `CampaignLedgerEntryKind`
- Application: `AddContributionToCampaignCommandHandler`, `RecordContributionRefundCommandHandler`, `ContributionRefundedLedgerHandler`, `ICampaignLedger`
- Infrastructure: `CampaignLedger`, `CampaignLedgerEntryConfiguration`, migration `AppendOnlyCampaignLedger`
- Tests: `CampaignLedgerTests`, `CampaignLedgerMigrationTests` (real PostgreSQL), `CampaignsTests` (domain and handlers)
