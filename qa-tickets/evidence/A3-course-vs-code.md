# Course vs reference code audit (agent 3)

Read-only. Course: /Users/maysamgamini/maysam-brain/contents/projects/projects-active/hicleanarch (working copy as of the audit; another session was editing it, 04-one-request, 05-guarantees and 06-jobs were fixed while I read, so line numbers below were re-grepped at the end).
Code used: crowdfunding-reclaim, branch feat/outbox-reclaim (HEAD 32b7f3d, newest). Where a finding is true of reclaim but not of main (b02a7af), it says so. Everything not tagged "reclaim only" is also false against main.
Method: (1) script comparing every ref=reference: excerpt line by line against both trees; (2) traces/check_traces.py against both trees (read-only); (3) manual verification of constants, counts, routes, status codes, test names, schemas, indexes against the source.

Headline: against main the course is in very good shape (all 195 reference excerpts and all 5 traces match main; counts of projects, tests, controllers, modules, inbox modules, constants 5 s/20/30 s/10 s/5, routes and status codes all verified). The material problems are (A) the outbox-reclaim work that exists only on feat/outbox-reclaim, (B) a handful of leftovers.

Quiz note: content/source/data/quizzes (step-1.1 to 1.6) are Stage 1 design quizzes; none has an answer that depends on the .NET code, so no quiz answer is wrong. There are no Stage 2 quizzes. architecture.yaml, requirements.yaml, walkthrough.yaml, ownership.yaml, loop.yaml, rounds.yaml, cast.yaml are design-only and code-independent.

---

## A. Outbox reclaim (true of main, false of reclaim): fix when reclaim merges

### F1: The outbox lesson teaches "nothing reclaims a stuck row", which reclaim makes false
- Severity: P1 once reclaim merges (P2 today if the course stays pinned to main)
- Where: stage2/10-outbox.md:12 ("never recovers a row it claimed and lost"), :319 (state diagram arrow "worker dies after the claim, nothing reclaims it"), :322-:332 (prose + recorded output "(0 rows)" for the claim run against expired Processing rows), :334 ("reclaim in one other place"), :349 (table row "A claim that was lost | Never recovered"), :351 (partial index "On Status = Pending"), :385 ("as the reference does for its unused lock column"), :722 ("this is the test the reference code would fail"), :742. Code: src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Persistence/OutboxClaimQuery.cs (WHERE clause now `(Pending AND ScheduledAtUtc <= {3}) OR (Processing AND LockedUntilUtc < {3})`), Outbox/ModuleOutboxProcessor.cs:109-121 (reclaimed row with Attempts >= 5 is dead-lettered), Persistence/ModelBuilderExtensions.cs (extra index), migrations 20261023163338/42/46_AddOutboxProcessingIndex (`ix_<module>_outbox_messages_processing` on LockedUntilUtc WHERE Status = 1), tests/IntegrationTests/.../OutboxReclaimE2ETests.cs.
- Evidence: course says the claim "only looks for Pending rows, and no code reads LockedUntilUtc" and shows a `(0 rows)` run. Reclaim reads LockedUntilUtc, claims expired Processing rows, increments Attempts on reclaim, and dead-letters after 5 claims ("Claimed N times without finishing").
- Fix: course session. Rewrite the reference-code paragraphs and the "states in the reference code" diagram (add Processing --> Processing : lease expires, reclaimed, Attempts + 1; Processing --> DeadLetter after 5 claims), drop the "(0 rows)" recording or replace it with a new recording, change the comparison table row, mention the second (Processing) index and the new dead-letter decision in the processor excerpt (:242-:280 "keeps only the decisions" omits the reclaim-exhaustion branch), and turn :722 into a test that exists (OutboxReclaimE2ETests).
- Acceptance: grep the course for "never recover|nothing reclaims|unused lock|would fail|0 rows" returns nothing stale; every ref=reference excerpt of OutboxClaimQuery.cs / ModuleOutboxProcessor.cs matches the file on feat/outbox-reclaim.

### F2: The claim-statement excerpts no longer match the file (reclaim only)
- Severity: P1 once reclaim merges (excerpt is quoted as "the statement itself")
- Where: stage2/10-outbox.md:210-227, stage2/06-jobs.md:235-250, stage2/19-audit.md:86-91; code OutboxClaimQuery.cs (statement now lines 52-70).
- Evidence (character level): course WHERE is `WHERE "Status" = {{(int)OutboxMessageStatus.Pending}}\n  AND "ScheduledAtUtc" <= {3}`; file has `WHERE ("Status" = {{...Pending}} AND "ScheduledAtUtc" <= {3})\n   OR ("Status" = {{...Processing}} AND "LockedUntilUtc" < {3})`. Course SET has `"LockedUntilUtc" = now() + ({2} || ' milliseconds')::interval`; file has `"LockedUntilUtc" = {2}` plus `"Attempts" = CASE WHEN m."Status" = {{...Processing}} THEN m."Attempts" + 1 ELSE m."Attempts" END`. The call now passes `nowUtc + lockDuration` as {2}.
- Fix: course session; re-excerpt all three, and fix the prose at 10-outbox:197 ("{2} lock time in milliseconds") and :199 ("The lock end still uses the database's now()") and 06-jobs:254 ("The `{3}` parameter... lock time written... nothing reads it"), which are now false: both sides use the application clock.
- Acceptance: the excerpt checker reports 0 mismatches for OutboxClaimQuery.cs; the sentence about the lock end using `now()` is gone.

### F3: Other places that state the gap as current fact (reclaim only)
- Severity: P1 once reclaim merges
- Where: stage2/06-jobs.md:254-261 ("nothing in the code reads it... never claimed again... The course design needs the same line"); stage2/11-delivery.md:322 (failure table: "nothing reclaims them yet. A known gap"); stage2/19-audit.md:74, :84-:95, :120, :127 (gap row "Open", "The claim looks for Pending only", exercise 2); stage2/21-ready.md:23 (rule 4 "One gap remains... The reference fails this check") and :38 (bullet "Outbox rows stuck in Processing are not reclaimed"); data/glossary.yaml:180 ("writes a lease time but never reads it"); data/divergence.yaml:120 ("The outbox claims only Pending rows, so a row stuck in Processing is never reclaimed"; `design:` line 119 says "recovers stuck rows", which becomes met); reference/01-design-vs-code.md:30 (table cell "an outbox that never reclaims stuck rows") and its generated entry; stage2/14-tests-that-teach.md (does not list OutboxReclaimE2ETests).
- Evidence: reclaim implements exactly the clause these pages say is missing; the course also uses CampaignUpdates' WebhookDeliveryTaskRepository as "the only other place that reclaims" (10-outbox:334, 06-jobs:258, 19-audit:93).
- Fix: course session, after deciding whether the course pins main or reclaim. If reclaim is adopted: mark the audit gap "Fixed", flip the 21-ready row 4 to fully Yes, change divergence status wording ("Partly built" may still hold because of the inbox window), update the glossary lease example, add OutboxReclaimE2ETests to reference/05-tests.md and the tests-that-teach lesson. If main stays the pin: no change, but add this list to the reclaim merge checklist.
- Acceptance: grep for "never reclaim|not reclaimed|nothing reads it|reads it" shows no stale claim; divergence.yaml:119-120 agree with the code.

### F4: Traces and line-number references drift against reclaim
- Severity: P2 (reclaim only)
- Where: traces/libraries.json and traces/make-contribution.json (HASH CHANGED for Outbox/ModuleOutboxProcessor.cs; make-contribution step 12 range [140,173] no longer matches), test-confirm-payment.json and test-contribution-credit.json also report DRIFT. Against main all five traces pass. Also stage2/08-patterns.md:91 table, and reference/03-code-walks.md steps that cite the processor.
- Evidence: check_traces.py against crowdfunding-reclaim: "HASH CHANGED libraries ... ModuleOutboxProcessor.cs", "RANGE DRIFT make-contribution step 12 ... [140, 173]". The processor grew from 174 to 187 lines (reclaim guard at 109-121).
- Fix: course session; re-record traces after the merge (python3 traces/check_traces.py --repo <tree>).
- Acceptance: check_traces.py exits 0 against the chosen tree.

### F5: Test and migration counts change if reclaim merges
- Severity: P3 (reclaim only)
- Where: reference/05-tests.md:20 ("100 methods in 33 files (98 facts and 2 theories)"), :24 table; stage2/21-ready.md:28 and 13-archtests.md:48 ("22 tests in 8 files") if the untracked tests/ArchitectureTests/.../DocumentationTests.cs (6 tests) lands.
- Evidence: counted today. main: integration 98 methods in 32 files (96 facts, 2 theories), plus ModerationService.IntegrationTests 2 facts in 1 file = the course's 100 / 33 / 98 facts. reclaim: integration 102 in 33 files (+OutboxReclaimE2ETests, 4 facts) so the course figure becomes 104 methods in 34 files, 102 facts. Unit tests (278 facts + 10 theories = 288 in 18 files, 27 data rows = 305 cases) and architecture tests (22 in 8 files without DocumentationTests; 28 in 9 with it) unchanged otherwise. Migrations per module on reclaim: CampaignUpdates 5, Campaigns 11, Contributions 15, Identity 5, Moderation 8, Notifications 4 (+1 each for Campaigns, Contributions, Moderation vs main); the course states no migration count, so no text changes.
- Fix: course session, recount after the pin is chosen.
- Acceptance: counts re-derived with grep -c of [Fact]/[Theory] per project match the page.

---

## B. False or stale against main

### F6: A decision option says the code is "one schema, naming discipline"; it has a schema per module
- Severity: P1
- Where: data/decisions.yaml:696 (option "One schema and one context, with naming discipline": "This is where the reference code is today."), :716 (`why`: "recommended because the reference code does not do it yet"), :678 (context is correct), :722 (note is correct). Code: every module DbContext calls `HasDefaultSchema(...)` (identity, campaigns, contributions, moderation, notifications, campaignupdates), migrations 20261023101510_MoveTablesToCampaignsSchema etc., ModuleSchemaRulesTests.
- Evidence: option text claims the code shares one schema and tables are told apart by prefixes; the code has six schemas. Line 716 says schema + context + role "is not done yet" while the same entry's note (722) says the schema is done and only the role is missing. The learner sees the code placed on the wrong option.
- Fix: course session. Change the option to "This is where the reference code was before the schema move" or remove the marker; reword 716 to "the code has the schema and the context per module, not the role".
- Acceptance: the only option marked "reference code is today" is the schema-per-module-one-login one (or none); 716 and 722 agree.

### F7: Glossary says the code has no pledge key
- Severity: P1
- Where: data/glossary.yaml:84 ("The reference code has no pledge key yet."). Code: ContributionsController.cs:118-137 (`Idempotency-Key` header, 200 + `Idempotent-Replayed: true`), Contribution.cs:45-47,92-94, ContributionConfiguration.cs:81-84 (unique index UX_contributions_ContributorId_IdempotencyKey, filtered `IdempotencyKey IS NOT NULL`), MakeContributionCommandHandler.cs:45-56,111-120, PledgeIdempotencyTests (12), PledgeIdempotencyE2ETests.
- Evidence: the course's own divergence.yaml:94, errors.yaml:14,74 and 04-one-request.md now say the key is built; the glossary contradicts them.
- Fix: course session; replace the sentence with "The reference code takes an optional Idempotency-Key header (see Design versus code)."
- Acceptance: grep "no pledge key" returns nothing.

### F8: Stale line references in the patterns table (executor and processor)
- Severity: P2
- Where: stage2/08-patterns.md "Tell other modules" row (about line 86): "CampaignTransactionExecutor.cs 88 to 94 writes the outbox row before the commit at 101 ... 96 saves; ModuleOutboxProcessor.cs 59 to 132 publishes it later". Code (main and reclaim): CampaignTransactionExecutor.cs GetDomainEvents 96, AddRangeAsync 101, SaveChangesAsync 104, CommitAsync 109, Notify 115. ModuleOutboxProcessor.cs: ExecuteAsync 56-, ProcessClaimedBatchAsync 99-174 (main) / 99-187 (reclaim).
- Evidence: these numbers match commit 837d8aa (88/93/96/101; processor SaveChanges at 133), before the wake-up signal was added; e.g. line 101 is now the AddRange, not the commit, and 132 is inside a comment block.
- Fix: course session; use 96-104 / commit 109 and 99-174 (or the post-merge numbers). Other rows in the table (controller 97, 119; Program.cs 110; AuditDependencyInjection 27; CampaignRepository 26-32; dispatcher and audit ranges) I checked and are right.
- Acceptance: each cited line shows the described statement.

### F9: Code-walk page claims it says when the code was read
- Severity: P3
- Where: reference/03-code-walks.md:21 ("which is why the page says when it was read"). The page states no date (correctly, per hygiene).
- Fix: course session; delete the clause.
- Acceptance: sentence gone.

### F10: "Charge-now variant" wording contradicts the funding-model entry
- Severity: P3
- Where: stage2/01-start.md:11 ("a simpler charge-now variant of the design"), :~140 ("It is a charge-now variant, and it never charges a real card"), stage2/07-payments.md:276 ("a charge-now variant of the design"). Code: MakeContributionCommandHandler.cs attaches `pi_mock_{id}` and makes no gateway call.
- Evidence: divergence.yaml (Funding model) says "neither route is complete: no real charge happens". Calling it "charge-now" suggests a charge is made.
- Fix: course session; say "a mock-payment variant" or "the code records a pending payment and never charges".
- Acceptance: wording consistent with divergence.yaml.

### F11: Three test projects vs four test projects
- Severity: P3
- Where: stage2/02-project-map.md:41 ("Three test projects"), :11 and 01-start.md "30 projects ... three test projects". Code: tests/ has four projects (ArchitectureTests, UnitTests, IntegrationTests, IntegrationTests/CrowdFunding.ModerationService.IntegrationTests); the solution file CrowdFunding.slnx lists 30 projects and only the first three test projects, so "30 / three" is right for the solution, but the fourth exists, runs in CI (`.github/workflows/ci.yml`), and is counted in the course's own integration totals (reference/05-tests.md:20, 33 files).
- Fix: course session; add "a fourth, for the extracted Moderation service, is outside the solution file" or reword to "three test projects in the solution".
- Acceptance: the 33-file integration count and the project count are explained by the same sentence.

### F12: CI branch named in learner text
- Severity: P3 (hygiene)
- Where: stage2/21-ready.md:28 ("on every push and pull request to `main`"). Code: .github/workflows/ci.yml `branches: [main]` (true).
- Fix: course session; say "to the default branch" or drop it.
- Acceptance: no branch names in content.

---

## C. Hygiene

### F13: Recording and check dates in learner text
- Severity: P3
- Where: `# recorded 2026-10-23, PostgreSQL 16.15 ...` headers in 20 output blocks: stage2/10-outbox.md (lines 89, 117, 232, 325, 429, 447, 511, 520, 549, 641) and stage2/12-schema.md (58, 147, 178, 261, 290, 324, 337, 347, 363, 404); stage1/21-step-1.md:23,27 and stage1/09-r2-failed-cards.md:44 ("checked 2026-09-28"), stage1/05-r2-scope.md:48; data/architecture.yaml:67 ("checked by the loop on 2026-09-28").
- Evidence: the rule is no review dates in content; the run date belongs in the pin doc. The output headers also carry tool versions, which are useful; only the date is the problem.
- Fix: course session; change to `# recorded run, PostgreSQL 16.15 ...` and drop "checked <date>" from external citations (or keep only if the date is the source's access date and the team decides that is not a review date; I flag it).
- Acceptance: grep -rE "20(25|26)-[01][0-9]-[0-3][0-9]" content/source returns only example data (09-contracts.md:286-287 deadlines).

### F14: Commit ids and run dates sit in the trace data
- Severity: P3
- Where: content/traces/*.json and traces/*.json: `"repo": {"commit": ...}` (content/traces still 57482c3; traces/ updated to b02a7af but uncommitted), test-confirm-payment.json and test-contribution-credit.json `recordedRun.date`/`recordedRun.commit` (2026-10-21, a486c10). Also docs/reference-pin.md still says the verification target is 837d8aa while the traces say b02a7af.
- Evidence: src/features/lesson/CodeWalkView.tsx renders only `recordedRun.result` and `.command`, so the React app does not show them; tools/codewalk/template.html (the standalone wireframe) does render runDate and runCommit.
- Fix: course session; keep ids in traces only if never rendered, or move them to docs/reference-pin.md; sync content/traces from traces/ (they differ in all five files); update reference-pin.md to the real target.
- Acceptance: grep of the shipped site output for a 7-hex commit id finds none; content/traces equals traces/.

### F15: Stray sed backup in the lessons folder
- Severity: P3
- Where: content/source/stage2/11-delivery.md-E (untracked). It is an older copy with different links, and it still says "nothing reclaims them yet" (line 322).
- Fix: course session; delete it.
- Acceptance: `ls content/source/stage2 | grep -- '-E$'` is empty.

### F16: Diagrams
- Severity: none (pass)
- Evidence: no fenced block without a language, no box-drawing characters, no ASCII box art in content/source; all diagrams are mermaid fences. Branch names: none besides F12. Commit ids in lesson text: none.

---

## D. Checked and found accurate (no finding)
- Excerpts: all 195 reference= blocks match main line by line; against reclaim only the three OutboxClaimQuery excerpts (F2) differ.
- Constants: lock 30 s, retry delay 10 s, batch 20, max attempts 5, outbox poll 5 s, webhook dispatcher poll 5 s / batch 20, scavenger 2 min / 50 / 15 min window, expiration poll 1 min, log retention 14 files, cache 30 s, webhook tolerance 300 s.
- Counts: 6 modules, 4 with Contracts, 27 projects in src (22 in modules: 4,4,4,4,3,3), 30 in the solution, nine controllers, 22 architecture tests in 8 files, 5 OutboxSignalTests, 12 PledgeIdempotencyTests, 43 methods in ContributionsTests.cs (16 in ContributionDomainTests), 288 unit methods in 18 files, 5 inbox modules, 3 outbox-worker modules, 15 divergence entries with correct label counts (1/6/4/4).
- Idempotency (divergence.yaml:94, errors.yaml:14/74/108, 04-one-request.md retry section, 05-guarantees rule 1, 06-jobs key-cleanup row, reference/05-tests.md rows): header rules, 200 + Idempotent-Replayed, 409 on mismatch, index name semantics, race handling all match the code. Per-module inbox "written after the handler in a separate statement" matches ServiceProviderEventPublisher/EfInbox. Wake-up signal (IOutboxSignal, ChannelOutboxSignal, Notify after commit in Campaigns/Contributions/Moderation executors, OutboxWakeUpLatencyE2ETests 5 samples / 1.5 s / 10 s cap) matches exactly, and the delivery lesson, outbox "wake-up signal" section, decision 20 and the concepts teach polling plus signal, not polling only.
- Schema per module, six schema names, `system` and `rosetta` schemas, per-module DB in Development/HA appsettings, three ModuleSchemaRulesTests names, init-module-databases compose, HA compose (16 nodes + 3 etcd).
- Routes and statuses: /api/campaigns, publish, cancel, contributions, confirm-payment, fail-payment, reward-tiers/reserve, /api/webhooks/payments/stripe, 400 mapping for InvalidOperationException, 409 for ResourceConflictException, 401 for bad webhook signature.

## E. Lessons and concepts judged accurate against main
Stage 2: 02-project-map, 03-layers, 07-payments, 09-contracts, 11-delivery (except line 322 under reclaim), 12-schema, 13-archtests, 14-tests-that-teach, 15-operate, 16-change-the-store, 17-extraction, 18-decisions-over-time, 20-mental-models, 04-one-request (as now rewritten), 05-guarantees (as now rewritten). Reference: 00-index, 02-errors, 03-code-walks (apart from F9), 04-libraries, 05-tests (counts per F5). Data: architecture, requirements, walkthrough, ownership, loop, rounds, cast, errors, syntax, quizzes. Concepts: all 48 cards (design-level; outbox-wake-up-signal and outbox-delivery-options are right).
