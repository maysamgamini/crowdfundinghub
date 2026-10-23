# QA tickets

Findings from a consistency check across the active repositories (crowdfundinghub, hicleanarch, hispeedsystem, hitechies-backend). Six read-only audits produced the evidence in `evidence/`; the tickets below group the findings by cause and by the repository that owns the fix. Evidence ids look like `A2-F3`: audit 2, finding 3.

| Audit | Scope |
|---|---|
| A1 | crowdfundinghub documents against the code |
| A2 | crowdfundinghub code and configuration consistency |
| A3 | the course text against the reference code |
| A4 | the course's internal consistency |
| A5 | hitechies-backend against crowdfundinghub |
| A6 | hispeedsystem design documents against the code and the course |
| A7 | mismatches reported by the course's authoring session |

## Fixed before filing

- Contribution list and detail endpoints were anonymous (audit A2-F2): fixed, with unit and end-to-end tests.
- Outbox claim ignored expired leases, and the lease and due time used different clocks: fixed (reclaim, attempt counting, application clock).
- Idempotent pledge creation and the outbox wake-up signal: built.

## Tickets

| Ticket | Severity | Owner repo | Title | Status |
|---|---|---|---|---|
| [QA-029](QA-029.md) | P0 | hitechies-backend | A message that crashes its worker is reclaimed forever (reclaim does not count as an attempt) | Open |
| [QA-031](QA-031.md) | P0 | hitechies-backend | Broker consumer commits the offset after three failures with only a log line (and on shutdown) | Open |
| [QA-001](QA-001.md) | P1 | crowdfunding | Background loops have no exception guard, so one database error stops the whole host | Fixed |
| [QA-002](QA-002.md) | P1 | crowdfunding | Extracted Moderation service lets any signed-in token read any review | Fixed |
| [QA-003](QA-003.md) | P1 | crowdfunding | With Messaging:Provider = RabbitMQ nothing consumes events, so no handler ever runs | Fixed |
| [QA-004](QA-004.md) | P1 | crowdfunding | docker-compose.microservices.yml monolith cannot reach its databases; base settings and compose disagree | Open |
| [QA-005](QA-005.md) | P1 | crowdfunding | One-click unsubscribe is anonymous and appears to take any user id | Open |
| [QA-021](QA-021.md) | P1 | crowdfunding | Docs teach the removed ICampaignContributionAvailabilityReader as the live design | Fixed |
| [QA-022](QA-022.md) | P1 | crowdfunding | TRADE_OFFS still describes the old outbox worker (4 events/s, PeriodicTimer, 'Processed' rows, TODO items, backoff numbers) | Fixed |
| [QA-023](QA-023.md) | P1 | crowdfunding | Unclosed fence leaks NetArchTest code; box-character diagrams; documentation tests need tuning | Fixed |
| [QA-028](QA-028.md) | P1 | hitechies-backend | Outbox claim compares application-clock time with database now(), and writes the lease with now() | Open |
| [QA-030](QA-030.md) | P1 | hitechies-backend | One wake-up signal shared by four module processors; Coaching signals before the commit | Open |
| [QA-032](QA-032.md) | P1 | hitechies-backend | IdempotentConsumer is unused and not race-safe; docs promise idempotent consumption | Open |
| [QA-033](QA-033.md) | P1 | hitechies-backend | Exception handler lacks concurrency and unique-violation mapping (crowdfundinghub lacks the 503) | Open |
| [QA-035](QA-035.md) | P1 | hicleanarch | Outbox lessons, excerpts and traces teach 'nothing reclaims a stuck row' (changes once reclaim is on main) | Open |
| [QA-036](QA-036.md) | P1 | hicleanarch | Schema statements: 'one schema' in a decision option and the cast primer; glossary says no pledge key | Open |
| [QA-037](QA-037.md) | P1 | hicleanarch | Idempotency-key retention is 7 days in some pages and 30 in others (also across hispeedsystem loops); retry rule described three ways | Open |
| [QA-038](QA-038.md) | P1 | hicleanarch | Animated 'unsuccessful campaign' trace says 188 backers then shows 250 | Open |
| [QA-043](QA-043.md) | P1 | hispeedsystem | 'See It Built' in the crowdfunding loop still states things the code does not do, and is missing what it now does | Open |
| [QA-044](QA-044.md) | P1 | hispeedsystem | Step 1.4 'late card' text contradicts the loop's own state diagram; the close job can strand a campaign in ENDED | Open |
| [QA-006](QA-006.md) | P2 | crowdfunding | Moderation reviews have no concurrency token, so concurrent approve and reject can both succeed | Open |
| [QA-007](QA-007.md) | P2 | crowdfunding | GlobalExceptionHandler leaks raw messages, misses DbUpdateConcurrencyException, has no 503 | Open |
| [QA-008](QA-008.md) | P2 | crowdfunding | Media-analysis webhook skips its own validator; out-of-range scores return 500 | Open |
| [QA-009](QA-009.md) | P2 | crowdfunding | Refund saga can finish 'successfully' after a cancellation, losing the remaining refunds | Open |
| [QA-010](QA-010.md) | P2 | crowdfunding | Outbox lease can expire before a batch ends; shutdown consumes retry attempts; lease is never renewed | Partly fixed |
| [QA-011](QA-011.md) | P2 | crowdfunding | A pledge accepts any reward-tier reservation id (not bound to backer or campaign) | Open |
| [QA-012](QA-012.md) | P2 | crowdfunding | Audit trail covers one command and records refresh tokens in payloads | Open |
| [QA-013](QA-013.md) | P2 | crowdfunding | Default email provider keeps every email in an unbounded in-memory queue | Open |
| [QA-014](QA-014.md) | P2 | crowdfunding | Outbox trace spans have no listener, so trace context is dropped | Open |
| [QA-016](QA-016.md) | P2 | crowdfunding | One live pledge per backer per campaign is not enforced | Open |
| [QA-017](QA-017.md) | P2 | crowdfunding | Closing fence at the deadline is not built | Open |
| [QA-024](QA-024.md) | P2 | crowdfunding | 'Every module has an outbox/inbox/Contracts' and the counts (projects, tests, xmin tokens) are wrong | Fixed |
| [QA-025](QA-025.md) | P2 | crowdfunding | Dead links and wrong file names | Fixed |
| [QA-026](QA-026.md) | P2 | crowdfunding | Wrong routes, status names, stale HA plan, pre-hardening documents without banners, mapping paragraph denying the inbox | Fixed |
| [QA-027](QA-027.md) | P2 | crowdfunding | Reference-repo mismatches reported by the site session | Fixed |
| [QA-034](QA-034.md) | P2 | hitechies-backend | Docs contradict the repo: Debezium connectors exist but docs deny CDC; DECISIONS.md missing; same class names with different semantics | Open |
| [QA-039](QA-039.md) | P2 | hicleanarch | Glossary lint is self-contradicting; 'resolver' used 37 times against a banned term; two glossary sources | Open |
| [QA-040](QA-040.md) | P2 | hicleanarch | Walkthrough, status table, cast, concept cards and one quiz need corrections | Open |
| [QA-041](QA-041.md) | P2 | hicleanarch | Stale line references in the patterns table | Open |
| [QA-045](QA-045.md) | P2 | hispeedsystem | Cost table not derived; outbox guidance disagrees across loops (CDC-first versus poll plus signal); three claim shapes unexplained | Open |
| [QA-046](QA-046.md) | P2 | hispeedsystem | Broken links and naming | Open |
| [QA-047](QA-047.md) | P2 | process | Automate the checks that found these problems | Open |
| [QA-015](QA-015.md) | P3 | crowdfunding | Code hygiene: hard-coded schemas in raw SQL, bypassed clock, mixed index naming, dead code, test names that promise more than they check | Open |
| [QA-018](QA-018.md) | P3 | crowdfunding | Ledger lacks entry kind, sign check, trigger and negative refund entries; total is a counter | Open |
| [QA-019](QA-019.md) | P3 | crowdfunding | Success and failure outcome emails are not built | Open |
| [QA-020](QA-020.md) | P3 | crowdfunding | Inbox is written after the handler and never pruned | Partly fixed |
| [QA-042](QA-042.md) | P3 | hicleanarch | Hygiene: dates, commit ids, branch names, localhost URL, stray backup file, trace data and anchors | Open |

Severity: P0 data loss or silent event loss, P1 wrong behaviour or a lesson that teaches something false, P2 stale or inconsistent, P3 cosmetic.
