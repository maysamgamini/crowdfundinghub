# Agent 6: hispeedsystem consistency audit (read-only)

Repos read: hispeedsystem (HS), crowdfunding-reclaim (CF code, branch feat/outbox-reclaim, HEAD 32b7f3d), hicleanarch (HC).
Paths: HS = /Users/maysamgamini/maysam-brain/contents/projects/projects-active/hispeedsystem, loops under system-design-mastery/interview-loops/04-financial-and-transactional (abbrev. LOOPS), CF = .../crowdfunding-reclaim, HC = .../hicleanarch.
"06" = LOOPS/06-crowdfunding-platform.md. Line numbers are as of this read.

Method notes: I wrote and ran a read-only link/anchor checker (scratchpad qa/linkcheck.py, qa/hs_anchor.py) over all of system-design-mastery and over every hispeedsystem.com URL in hicleanarch. All relative links in system-design-mastery resolve and all 06 cross-loop step references (payment 1.1/1.2/1.4/1.5/3.2, hotel 1.2/2.3, outbox 2.6) point at headings with the claimed topic. Situations: 45 ids, no duplicates, id equals filename, every primitiveSlug and trackId exists. The site resolves short anchors (#step-23, #r29) through src/lib/sectionReferences.ts, so those are valid. No file:// links, no ASCII box diagrams, no commit ids in the loops.

---

### F1: "Recomputes the raised total as a sum over the ledger" is false (also repeated in Round 2)
- Severity: P1
- Where: HS 06:2154 (See It Built) and 06:979 (R2 step 2.1 "the reference implementation does it, under a per-campaign lock"); conflicts with HC content/source/data/divergence.yaml "Raised total" entry and CF code.
- Evidence: 06:2154 "It recomputes the raised total as a sum over the ledger under a per-campaign lock on every pledge". 06:979 "Recompute SUM(entries) on each pledge. It's correct (the reference implementation does it, under a per-campaign lock...)". Code: CF src/Modules/Campaigns/.../AddContributionToCampaignCommandHandler.cs:60-100 takes the advisory lock, inserts one ledger row, then calls `campaign.ApplyConfirmedContribution(...)`, which adds to `RaisedAmount` (Campaign.cs:182). No SUM anywhere. divergence.yaml: "The counter is not a sum over the ledger."
- Owner: hispeedsystem
- Fix: Say the code keeps an incremental counter under the per-campaign advisory lock and decides funded/failed from that counter; keep the "exactly right at Round 1 scale, replaced by 2.1" sentence. Delete the parenthetical at 979 or reword to "a counter under a lock".
- Acceptance: grep 06 for "sum over the ledger" and "reference implementation does it" returns nothing; the replacement wording matches divergence.yaml "Raised total".

### F2: The ledger description in See It Built is the design, not the code
- Severity: P1
- Where: HS 06:2143 (Campaigns row); CF src/Modules/Campaigns/.../Persistence/Configurations/ContributionLedgerEntryConfiguration.cs and Repositories/ContributionLedger.cs; HC divergence.yaml "Ledger".
- Evidence: 06 says "an append-only campaign ledger in which a unique key on contribution and entry kind makes a redelivered pledge or refund a no-op, a check constraint ties each entry's sign to its kind, and a trigger rejects updates and deletes (refunds are negative compensating entries)". Code: only `HasIndex(x => x.ContributionId).IsUnique()` ("uq_campaign_contributions_ledger_contribution_id"), `INSERT ... ON CONFLICT ("ContributionId") DO NOTHING`; entity has no kind column; `ApplyConfirmedContribution` rejects Amount <= 0; no HasCheckConstraint, no trigger anywhere in src; refunds only change Contribution status. Amounts are decimal(18,2). divergence.yaml: "There is no entry kind, sign check or trigger, and refunds change a status instead of adding an entry."
- Owner: hispeedsystem
- Fix: Rewrite the cell: ledger table of confirmed contributions with a unique contribution id and ON CONFLICT DO NOTHING; no kind, sign check, trigger or refund entries (that is step 1.3's design, not yet built).
- Acceptance: the cell contains none of "entry kind", "sign", "trigger", "negative compensating"; wording agrees with divergence.yaml.

### F3: "It charges at pledge time and refunds if the campaign fails" overstates a mock payment path
- Severity: P1
- Where: HS 06:2153 and 06:269; CF src/Modules/Contributions/.../MakeContributionCommandHandler.cs:100-102; HC divergence.yaml "Funding model" (status "Not built yet").
- Evidence: 06:2153 "It charges at pledge time and refunds if the campaign fails: the charge-now model from step 1.1"; 06:269 "The reference implementation at the end of this page makes exactly that choice." Code: "No live gateway call is made here" then `contribution.AttachPaymentIntent($"pi_mock_{contribution.Id:N}", "Mock")`; money state moves only on a manual confirm-payment endpoint or webhook; refunds mark status without a provider call (06:2156 says this last part correctly).
- Owner: hispeedsystem
- Fix: Say it models the charge-now shape (Pending then Succeeded then Refunded) against a mock provider; no real card is charged or saved. Soften 269 to "the reference implementation follows the charge-now shape".
- Acceptance: both places mention the mock provider; no sentence implies a real charge.

### F4: "Inboxes that make every consumer idempotent" is overstated
- Severity: P1
- Where: HS 06:2138; CF src/BuildingBlocks/.../Events/ServiceProviderEventPublisher.cs:36-58; HC divergence.yaml "Inboxes and the outbox".
- Evidence: 06 "...events carried by a transactional outbox, and inboxes that make every consumer idempotent." Code: the module inbox is checked before and written after `InvokeAsync(handler)` in a separate statement (a crash between the two re-runs the handler), applies only to the in-process outbox path (RabbitMqMessageBus does not set the message id), Identity has none, nothing prunes it. HS loop 05 step 1.3 teaches the opposite shape: "the inbox row and the effect are committed in one transaction".
- Owner: hispeedsystem
- Fix: Say "per-module inboxes narrow repeated deliveries; handlers must still be idempotent, because the inbox is recorded after the handler, not in its transaction". Point at 05 step 1.3 as the target design.
- Acceptance: the sentence no longer says "every"; the same-transaction-versus-after-handler difference appears once.

### F5: "Implements Round 1's design" while step 1.1, 1.5, 1.6 and the one-live-pledge rule are still unbuilt
- Severity: P1
- Where: HS 06:2138 headline and the "Where it differs" list 06:2151-2157; HC divergence.yaml entries Funding model, Closing fence, Fees and payouts, Refunds, Status names (all "Not built yet") and Double-click protection ("Partly built").
- Evidence: 06 "implements Round 1's design and parts of Round 2". The differs list names charge-now, the sum, fees/payouts, state-only refunds, reservation purpose. It does not say: no SetupIntent or saved card; no closing fence (CF Campaign.ApplyConfirmedContribution checks status Published, not deadline); no settlement job, charge attempts or per-attempt key; no one-live-pledge rule (grep for already_backing / one-live finds nothing); different status names (Draft/Published/Successful/Failed, Pending/Succeeded/Failed/Refunded); deadline job polls every 1 minute and moves straight to Successful/Failed, sweeper runs every 2 minutes (RewardTierReservationScavengerBackgroundService.cs:21) against the design's 10 s.
- Owner: hispeedsystem
- Fix: Replace the headline with "implements the module shape and parts of steps 1.2, 1.3 and 1.4" and add one bullet each for: no closing fence, no save-card/settle/charge-attempt path, no one-live-pledge rule, different status names, sweeper 2 min and job 1 min. Link divergence.yaml's course page as the detailed table.
- Acceptance: every "Not built yet"/"Partly built" entry in divergence.yaml has a matching bullet or is covered by the headline.

### F6: See It Built is missing what the code now has, and its review date is stale
- Severity: P2
- Where: HS 06:2138-2157 ("checked against the code on 2026-09-28"); CF commits b02a7af, cbfb223, 32b7f3d, plus schema/database/HA changes.
- Evidence: Newly true and absent from the page: Idempotency-Key header with unique (ContributorId, IdempotencyKey) filtered index (ContributionConfiguration.cs:81-84, 200/409 handling in ContributionsController.cs:101-137); outbox reclaim on lease expiry (OutboxClaimQuery.cs, 30 s lease, 5 attempts then dead-letter, `OutboxReclaimE2ETests`); in-process wake-up signal (5 s timer remains); schema per module and a database per module in the local run (deploy/postgres/init-module-databases.sh) and a replicated Patroni profile. The page still says "one PostgreSQL database with a schema per module", has no step-1.2 note, and carries a hard-coded date.
- Owner: hispeedsystem
- Fix: Add a short "since then" list (key on pledge creation, reclaim, signal, database per module locally, HA profile) and drop the dated sentence ("checked against the code on 2026-09-28") in favour of undated wording.
- Acceptance: no calendar date remains in the See It Built section; the listed capabilities appear; "one PostgreSQL database" is qualified ("by default").

### F7: Step 1.4 "late card" text contradicts the same page's pledge state diagram and unique index
- Severity: P1
- Where: HS 06:384 versus 06:525/538 (pledge stateDiagram) and 06:309-312 (`one_live_pledge_per_backer`); HC content/source/stage1/24-step-4.md:44-48 and divergence.yaml "A card saved after the hold ended".
- Evidence: 06:384 "the activation tries to re-claim a slot... If one is free, the pledge becomes ACTIVE and the backer never notices." Diagram: `PENDING_SETUP --> EXPIRED: hold expired or campaign ended` and `EXPIRED --> [*]` (no EXPIRED to ACTIVE arrow). The unique index is `WHERE status IN ('PENDING_SETUP','ACTIVE')`, so reactivating an EXPIRED pledge after the backer pledged again would violate it. HC: "In the course design the old pledge stays EXPIRED, and a final status never comes back."
- Owner: hispeedsystem (the course already chose; the loop is the odd one out)
- Fix: Either add `EXPIRED --> ACTIVE: card saved late, slot re-claimed` plus a note about the unique index, or adopt the course's "stay EXPIRED, pledge again with the saved card" and update step 1.4, the summary row and the diagram note.
- Acceptance: text, diagram and divergence.yaml agree on whether EXPIRED is final.

### F8: Idempotency-key retention differs across the loops (7 days versus 30 days)
- Severity: P1
- Where: HS LOOPS/01:276 and :705 (7 days); 02:354 (30 days); 05:1205 (30 days); 06:750 (R1.7, 30 days) while 06 step 1.2 says it uses the payment loop's step 1.1 "as is"; HC content/source/stage1/22-step-2.md:204.
- Evidence: 01 "We keep keys for 7 days". 02 "we keep keys for 30 days (a design choice...)". 06:750 "idempotency rows (kept 30 days) about 1 GB". HC step 2: "Keys live for 7 days, as the payment loop has it... The loop's size estimate for this table assumes 30 days." The course had to patch it, so a learner reading the loop alone gets two numbers for the same pattern.
- Owner: hispeedsystem
- Fix: Pick one window for the crowdfunding loop (7 days, matching its parent loop) and recompute R1.7's 1 GB (about 0.25 GB at 7 days), or say explicitly that 30 days is a separate choice and why.
- Acceptance: 06 step 1.2 and R1.7 state the same window; HC 22-step-2 can drop the "assumes 30 days" caveat.

### F9: Email volume behind the Round 1 cost table is not derived and is half of what its own rules imply
- Severity: P2
- Where: HS 06:766; HC content/source/stage1/02-numbers.md ("Where the 90,000 emails come from").
- Evidence: 06 "~51,000 emails a day (pledge receipts, outcome emails, creator notices) x 30 = 1.53M ... = $153", total "~$885/month", "about 690 times". HC: 25,000 receipts + 25,000 creator notices + 25,000 outcome + 15,000 charge receipts = 90,000, $270, total about $1,000, "about 610 times", 0.004%. Nothing in 06 shows how 51,000 arises.
- Owner: hispeedsystem
- Fix: Show the email count in R1.7 (adopt 90,000 and update total, the 690x and 0.003% figures) or document the batching assumption that yields 51,000.
- Acceptance: R1.7 contains an email derivation; totals and ratios match HC 02-numbers or HC cites the loop's derivation.

### F10: Close job can strand a campaign in ENDED (design gap the course quietly patches)
- Severity: P2
- Where: HS 06:488 (step 1.6 item 2) and 06:430-440 (step 1.5 "in a new transaction"); HC content/source/stage1/26-step-6.md:61.
- Evidence: 06 "Every 30 seconds it finds campaigns that are `LIVE` with `deadline <= now()`, runs step 1.5's closing fence on each, then sums the ledger after the fence". Step 1.5 says the sum runs "in a new transaction". A crash between the two leaves status ENDED, which the job never selects again. HC: "polls every 30 seconds for campaigns... that are LIVE, or are ENDED but not yet decided... if the job stops between them, a job that looked only at LIVE would never decide that campaign." Not listed as a divergence in divergence.yaml.
- Owner: hispeedsystem (and list it in HC if kept)
- Fix: Change the job's query to `status IN ('LIVE','ENDED') AND deadline <= now()` and add a failure-mode row in R1.9.
- Acceptance: R1.9 lists "close job dies between fence and decision" and step 1.6 selects ENDED rows.

### F11: Outbox guidance disagrees between the components/primitives and the loops (CDC-first versus poll+signal-first)
- Severity: P2
- Where: HS system-design-mastery/components/12-change-data-capture-and-outbox-pattern.md:91 and :120, components/10-two-phase-commit-and-saga-orchestration.md:207; versus LOOPS/05:856-905 and CF docs/EVENT_DELIVERY_APPROACHES.md sections 2B, 3, 4.
- Evidence: components/12:120 "Relying on Polling with Soft-Delete Flags... Rule: Prefer log-based CDC"; :91 polling "Queries every interval even when idle, claims and deletes on the busiest database" with latency "Up to one poll interval". components/10:207 "The Senior SWE Solution: The Transactional Outbox Pattern with CDC". Loop 05 and EVENT_DELIVERY: poll plus signal is milliseconds, no new infrastructure, "Choose it when you run one deployable"; CDC "overkill" for a monolith and chosen "when the scale justifies it". Neither component page mentions `SKIP LOCKED` claims or the signal.
- Owner: hispeedsystem
- Fix: Reword the component rule to "CDC when volume or consumer count justifies it; otherwise claim with SKIP LOCKED plus a wake-up signal", and link loop 05 step 2.1.
- Acceptance: components/10 and 12 no longer give an unconditional "prefer CDC"; they link to 05 step 2.1.

### F12: LISTEN/NOTIFY trade-offs: loop 05 teaches a throughput ceiling that EVENT_DELIVERY_APPROACHES omits; CDC delay quoted differently
- Severity: P2
- Where: CF docs/EVENT_DELIVERY_APPROACHES.md section 2C and section 3 table; HS LOOPS/05:871-884.
- Evidence: 05 "PostgreSQL takes a global lock during the commit of every transaction that ran NOTIFY, which serializes those commits. One published write-up measured a ceiling of about 2,900 writes a second". EVENT_DELIVERY C lists bad points as non-durable, small payloads, dedicated connection, PgBouncer, and "Choose it when several processes write to the same outbox", with no mention of commit serialization. Delay for CDC: EVENT_DELIVERY "milliseconds to seconds"; 05 "Tens to hundreds of ms". Loop 05 and primitive 05 both agree with EVENT_DELIVERY on non-durability, 8,000-byte payload and pooler issue.
- Owner: crowdfunding code or docs (EVENT_DELIVERY_APPROACHES.md)
- Fix: Add the commit-serialization caveat to section 2C ("avoid when the writer commits thousands of times per second") and align the CDC delay wording with loop 05.
- Acceptance: section 2C mentions the global commit lock; both repos quote compatible CDC delays.

### F13: Three different "claim" shapes with no page explaining when each applies
- Severity: P2
- Where: HS LOOPS/05:342 ("No leases, no timers: the database's lock is the claim"), 05:376 (bucket claim), 06:489 (settlement jobs: `FOR UPDATE SKIP LOCKED` and a lease), 06:707 ("lease 60 s"); CF OutboxClaimQuery.cs and docs/EVENT_DELIVERY_APPROACHES.md section 5 (Status=Processing, LockedUntilUtc, reclaim after 30 s).
- Evidence: 05 holds the row lock across the publish, rolling back on a crash, "No leases"; 06 uses a lease because settlement runs for minutes; the reference outbox commits the claim (Pending to Processing with a 30 s lease) and reclaims later, counting attempts toward 5. A learner moving from loop 05 to the code sees the opposite claim design. 05 also teaches the inbox "in one database transaction" with the effect, which the code does not do (F4).
- Owner: hispeedsystem
- Fix: Add a short comparison sentence to 05 step 1.4 ("hold the lock for short publishes; commit a lease-claim when handlers run long, as the reference code and settlement do, and count reclaims as attempts").
- Acceptance: 05 step 1.4 or 2.3 names the lease-claim variant and links 06 step 1.6.

### F14: Course (HC) still says the outbox never reclaims stuck rows; code on feat/outbox-reclaim now does
- Severity: P2 (becomes P1 on merge)
- Where: HC content/source/data/divergence.yaml "Inboxes and the outbox"; stage2/10-outbox.md:319,334,349; stage2/11-delivery.md:322; stage2/18-decisions-over-time.md:109; stage2/19-audit.md:74,93,120,127; stage2/21-ready.md:38; stage2/06-jobs.md:261.
- Evidence: divergence.yaml "The outbox claims only Pending rows, so a row stuck in Processing is never reclaimed." CF OutboxClaimQuery.cs claims `Status = Processing AND LockedUntilUtc < now` and increments Attempts; ModuleOutboxProcessor.cs:124 dead-letters at 5; EVENT_DELIVERY section 5 "any worker then reclaims them and counts an attempt". Also the design (divergence.yaml design column) "recovers stuck rows" is now true of the code.
- Owner: hicleanarch
- Fix: After the branch merges, change the divergence entry and the listed lessons/audit rows from "gap" to "built, with a 30 s lease, attempts counted, dead letter after 5".
- Acceptance: grep HC content for "never reclaimed|nothing reclaims|not reclaimed" returns nothing.

### F15: Stray backup file tracked in the course
- Severity: P3
- Where: HC content/source/stage2/11-delivery.md-E (tracked by git, 25 KB sed backup).
- Evidence: `git ls-files | grep -- "-E$"` lists it; it duplicates 11-delivery.md including the stale "nothing reclaims them yet" row.
- Owner: hicleanarch
- Fix: git rm the file.
- Acceptance: no file ending in -E under content/.

### F16: 16 broken `/components/...` links in five cheat sheets
- Severity: P2
- Where: HS system-design-mastery/cheatsheets/01 (lines 118-121), 02 (:92), 03 (:87-89), 04 (:102-104), 05 (:93-95).
- Evidence: e.g. 03:87 `[Database Sharding & Replication](/components/02-database-sharding-and-replication)`. Eleven distinct targets have no file in system-design-mastery/components (the route resolves `components/<slug>.md`, and next.config has no redirects): 01-object-storage, 02-database-sharding-and-replication, 03-distributed-cache, 04-distributed-rate-limiter, 05-message-queue, 06-distributed-unique-id-generator, 07-api-gateway, 08-distributed-consensus, 10-distributed-transactions, 11-search-indexing, 15-distributed-tracing. (Cheat sheet 07's /components links do resolve.)
- Owner: hispeedsystem
- Fix: Repoint to the real slugs, e.g. 04-distributed-cache-patterns-and-eviction, 05-message-queues-vs-event-streams, 02-distributed-rate-limiting, 08-database-sharding-and-partition-keys, 09-distributed-consensus-raft-and-paxos, 10-two-phase-commit-and-saga-orchestration, 14-api-gateway-and-reverse-proxy, 15-distributed-unique-id-generators; drop links with no counterpart (object storage, search indexing, tracing).
- Acceptance: qa/linkcheck.py reports no "MISSING /components" lines.

### F17: Course hrefs to Round 2 heading use a double-dash anchor that the site never generates
- Severity: P3
- Where: HC content/source/stage1/05-r2-scope.md:10, content/problems/crowdfunding/stage1/r2-scope.json:16, content/audit.json:6994.
- Evidence: `...06-crowdfunding-platform#round-2--senior--a-hit-campaign-a-million-backers-and-settlement-day`. HS slug (MarkdownRenderer.tsx getHeadingId) collapses whitespace after dropping the dot character, giving `round-2-senior-a-hit-campaign-a-million-backers-and-settlement-day`. All other hispeedsystem.com anchors in HC resolve.
- Owner: hicleanarch
- Fix: Use the single-dash anchor (or `#r2` style short form is not valid for H2; use the full slug).
- Acceptance: qa/hs_anchor.py prints nothing.

### F18: Webhook dedupe table has a different name than the loop it says it copies
- Severity: P3
- Where: HS 06:674 (`webhook_inbox`) and 06:497 "exactly as the payment loop's step 1.4 does" versus LOOPS/01:633 and :407 (`webhook_events`); CF table is `processed_payment_webhooks`.
- Evidence: 06 `CREATE TABLE webhook_inbox (event_id TEXT PRIMARY KEY...)`; 01 `CREATE TABLE webhook_events`.
- Owner: hispeedsystem
- Fix: Use one name across 01, 06 and the Round 2 text (06:1190 calls it "the inbox").
- Acceptance: grep for webhook_inbox|webhook_events shows one name.

### F19: "Exactly once" used bare in the crowdfunding loop while the other loops qualify it
- Severity: P3
- Where: HS 06:46, 06:56, 06:472, 06:581 ("Charge 250 backers exactly once") and R1.3 "charged at most once"; versus LOOPS/01:75, :2038 ("exactly once in effect"), LOOPS/05 step 3.5 (per-hop contract, warns against promising exactly-once).
- Evidence: 06:46 "Charge every pledge exactly once" while 06:1102 itself notes the PSP key expires after 24 hours and 06:489 item 4 says a timeout is an unknown resolved by asking the PSP.
- Owner: hispeedsystem
- Fix: Say "exactly once in effect (at most one successful charge, retried until known)" once in the opener and use it in the step title.
- Acceptance: 06 defines the term once and matches 01/05 wording.

### F20: Round 1 retry schedule has no number; the course invents one
- Severity: P3
- Where: HS 06:495 (step 1.6 item 6 "retried a few times over the next day, then marked FAILED") and 06:1139-1141 (R2: days 1, 3, 5 plus a day-7 final attempt); HC content/source/stage1/26-step-6.md:70 "We assume three attempts".
- Evidence: the loop gives R1 no attempt count and a 1-day window; R2 uses a 7-day window and four retries. HC adds "three attempts" and in 16-r2-check.md:38 "attempts so far are the deadline charge (attempt 1) and the retries on day 1 and day 3 (attempts 2 and 3)", which belong to R2.
- Owner: hispeedsystem (state the R1 number) / hicleanarch (mark as its assumption)
- Fix: Put "three attempts over 24 hours" in step 1.6, or have HC label it "course assumption".
- Acceptance: the number exists in one authoritative place.

---

## Design claims versus reference-code status (CF branch feat/outbox-reclaim)

| Design claim (06) | Status in code | Page that should say so |
|---|---|---|
| 1.0 Charge on click, mutable total | Partly: counter total (F1), mock charge (F3) | See It Built |
| 1.1 Save card, SetupIntent, charge off-session at deadline | Still false (mock PI, no card save) | divergence "Funding model" |
| 1.1 Charge-now alternative = what reference does | Overstated: mock, no real charge (F3) | See It Built, 06:269 |
| 1.2 Idempotency key on pledge creation | Now true (unique (backer,key) filtered index; 200 replay, 409 mismatch; design says 201/422) | Absent from See It Built (F6) |
| 1.2 One live pledge per backer per campaign | Still false | Absent (F5) |
| 1.3 Append-only ledger, unique (request, kind), sign CHECK, trigger | False: unique contribution id only, ON CONFLICT DO NOTHING (F2) | See It Built wrong |
| 1.3 Total = sum of ledger; snapshot updated in same txn | False: counter under advisory lock decides (F1) | See It Built wrong, 06:979 wrong |
| 1.4 Tier counter, conditional update, CHECK | Different mechanism: advisory lock + domain check + xmin, no CHECK | divergence "Tier reservations" |
| 1.4 15-minute hold | True (RewardTierReservation.ReservationWindow = 15 min) | Stated correctly |
| 1.4 Sweeper every 10 s | Different: every 2 min, 50 per pass | Missing from See It Built (F5) |
| 1.4 Late card re-claims same pledge | N/A (no card saving); loop contradicts own diagram (F7) | 06 step 1.4 |
| 1.5 Closing fence (row lock + status LIVE) | Still false; deadline checked in app and not in ApplyConfirmedContribution | Missing (F5) |
| 1.6 Close job every 30 s, ENDED state, exact ledger sum | Different: 1 min, no ENDED, decides from counter | divergence "Deadline job" |
| 1.6 Settlement jobs, SKIP LOCKED + lease, charge-once attempt keys, one success per pledge | Still false (no charge path) | Missing (F5) |
| 1.6 Webhook inbox deduped by event id in same txn | True for payment webhook (processed_payment_webhooks, in executor txn); module inbox is after-handler (F4) | See It Built partly |
| R1.6 Outbox in same txn as change, relay | True (per-module outbox, delete on success, SKIP LOCKED, 30 s lease, reclaim, 5 attempts, dead letter) | See It Built silent on reclaim (F6) |
| Wake-up signal / poll interval | True: in-process one-slot signal, 5 s timer safety net | Matches EVENT_DELIVERY; absent from HS page |
| Schema per module | True (default single DB, schema per module) | Stated |
| Database per module | True for local run and HA profile; microservices compose still one DB | Not stated; "one PostgreSQL database" outdated (F6) |
| Refund saga (state only) | True: batches of 100, no provider call | Stated correctly |
| Fees metered, no payouts | True | Stated correctly |
| Creator cannot back own campaign (R3.2 mention) | True (MakeContributionCommandHandler.cs:86) | Stated correctly |
| Status names (DRAFT/LIVE/ENDED/FUNDED/...) | Different (Draft/Published/Successful/Failed/Cancelled; Pending/Succeeded/Failed/Refunded) | divergence "Status names"; absent from See It Built |
