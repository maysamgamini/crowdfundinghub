# Course repo internal consistency audit (read-only)

Repo: /Users/maysamgamini/maysam-brain/contents/projects/projects-active/hicleanarch
Method: read content/source/**, content/animations, src/ renderers, tools/; ran no scripts, tests or builds in the repo. Throwaway python (in the scratchpad only) was used to cross-check link targets, anchors, front matter, quiz ids/sections and generated-JSON parity.

Verified clean (no finding): all 21 Stage 2 and 23 Stage 1 front matters have slug/title/summary/terms, slugs and titles are unique, every `terms` entry exists in glossary.yaml; generated JSON equals its source for every lesson, data file and quiz; every `/p/crowdfunding/...#anchor` into a lesson, a divergence entry (slugged titles), a code-walk/library card (slugged captions) or a decision id resolves; quiz `lesson`/`numbers.section` ids exist; Stage 2 lesson numbers quoted in 01-start (2-3, 5, 6-7, 8-11, 12-14, 15-17, 18-21) match the 21-file order; the 22 architecture tests / 8 files add up; pledge and campaign each have nine statuses in loop.yaml, walkthrough.yaml and the text.

## Findings, ranked by learner impact

### F1: Idempotency answers are kept 30 days on the API page and 7 days in step 1.2 (and in the quiz)
- Severity: P1
- Where: content/source/stage1/01-api.md:259 vs content/source/stage1/22-step-2.md:204; also stage1/02-numbers.md:40; stage2/04-one-request.md:518 and stage2/06-jobs.md:420 use 7; data/quizzes/step-1.2.yaml 1.2-q10 uses 7.
- Evidence: 01-api: "We assume stored answers are kept for 30 days, long enough for any retry." Step 1.2: "Keys live for 7 days, as the payment loop has it. A scheduled job deletes older rows ... The loop's size estimate for this table assumes 30 days." Numbers page: "idempotency rows (kept 30 days) about 1 GB".
- Fix: Pick one retention for the design (7 days) and say on the API and numbers pages "kept 7 days (the size estimate assumes 30)", or move the API sentence to the step 1.2 wording.
- Acceptance: a fact-registry check (see end) that finds every "N days" next to "key"/"stored answer"/"idempotency rows" and requires one design value plus a marked exception.

### F2: Cast primer says the reference code keeps every table in one schema; everything else says one schema per module
- Severity: P1
- Where: content/source/stage1/17-cast-primer.md:38 vs data/glossary.yaml (modular monolith, schema), stage2/01-start.md:88, stage2/12-schema.md, data/divergence.yaml:230 (Modules), data/decisions.yaml:678.
- Evidence: primer: "It keeps every table in one database schema, and this course leaves out the other four modules." Glossary: "Each module owns its own tables, in its own PostgreSQL schema. All the schemas sit in one database with one login." Divergence: "its own PostgreSQL schema, all in one database with one login".
- Fix: Reword the primer to "one database, one schema per module".
- Acceptance: grep check that no learner text matches `(one|single) (database )?schema` in a reference-code sentence; or a fact-registry entry "schema per module: yes".

### F3: Animated "unsuccessful campaign" trace says 188 backers, then 250
- Severity: P1
- Where: content/animations/architecture.json:726 and 774 (188) vs 784, 792, 819, 825, 831, 834 (250).
- Evidence: intro "$11,280 pledged by 188 backers"; step 3 "moves all 188 active pledges to RELEASED" but its packet label is "RELEASED x 250", metric `"released": 250`, and step 5 "The worker sends 250 emails ... 250 emails: not funded". 188 x $60 = $11,280, so 188 is right.
- Fix: Change the packet label, the `released` metrics and the email text to 188.
- Acceptance: a test that, for each scenario, compares numbers in `intro` with `metrics` maxima and labels; note this file is hand-edited and is not in the DATA table of tools/content/sync.mjs, so content:check never sees it.

### F4: Idempotency-key card lists "request key" both as an alias and as an avoided name, while the glossary defines "request key"
- Severity: P1 (self-contradicting lint rule, 10 flagged passages)
- Where: content/source/concepts/idempotency-key.md (aliases and avoid both contain `request key`); data/glossary.yaml:83; data/cast.yaml:105,301; data/walkthrough.yaml:2,26; data/decisions.yaml:52,258; quizzes/step-1.2.yaml (1.2-q06, q08, q12 prompts).
- Evidence: card: `aliases: [idempotency key, request key]` and `avoid: [request key, dedupe key, dedup key]`. Glossary: "The course uses two: a request key (one per backer action ...) and a charge key". content/audit.json reports variant-name "request key in quiz 1.2-q06 ...".
- Fix: Decide the term. "Request key" is the established course word (it is the counterpart of "charge key"), so remove it from `avoid` and keep it as an alias.
- Acceptance: audit.mjs should fail when `aliases` and `avoid` intersect.

### F5: The word "resolver" is used throughout Stage 1/2 and in a quiz although the recovery-job card forbids it; the job has three names
- Severity: P2 (term drift, learner-facing)
- Where: content/source/concepts/recovery-job.md (`avoid: [resolver]`, `allow: []`); stage1/26-step-6.md:4,28,65,160,209,210,228; stage1/08-r2-settlement, 11-r2-money, 13-r2-architecture, 15-r2-tradeoffs; stage2/07-payments.md:12,39; stage2/15-operate.md:274,277; stage2/17-extraction.md:143; data/quizzes/step-1.6.yaml:112 (1.6-q10 "What should the resolver do"). Versus "Recovery job" in data/cast.yaml:233, data/walkthrough.yaml, stage2/06-jobs.md:4,33.
- Evidence: step 6 title/summary: "a crash resolver"; card: "A job that puts pledges stuck in CHARGING back in the queue". Stage 2 07-payments says "a crash resolver" while 06-jobs says "the recovery job". At least 37 hits for `resolver` in Stage 1 alone (stage2/17-extraction.md:143 is a different, correct use: JWKS key resolver).
- Fix: Use "recovery job" everywhere (step 6, quiz 1.6-q10, R2 pages, 07-payments, 15-operate), keep `ChargeResolver` only as a code identifier and add it to `allow`.
- Acceptance: variant-name flags for recovery-job drop to 0 (today audit.json shows only 4 sample passages, the real count is higher); make variant-name a test failure once cleaned.

### F6: Glossary says the reference code has no pledge key; divergence and Stage 2 say the key half is built
- Severity: P1
- Where: content/source/data/glossary.yaml:84 vs data/divergence.yaml:94 (Double-click protection, "Partly built") and stage2/04-one-request.md:518-526.
- Evidence: glossary: "The reference code has no pledge key yet." Divergence: "The key half is built. POST /api/campaigns/{id}/pledges reads an optional Idempotency-Key header ...". docs/reference-pin.md itself lists "rewrite the 'no pledge key' statements" as a to-do.
- Fix: Replace the glossary sentence with "The reference code has the key half (optional header, 409 on a different body); it has no one-live-pledge rule."
- Acceptance: grep for `no pledge key` / `no idempotency key` across content/source returns 0; tie the sentence to the divergence entry id.

### F7: Walkthrough page still describes "labs" that were removed, and the status table prints raw lab ids
- Severity: P2
- Where: src/app/p/[problem]/1/walkthrough/page.tsx:19; src/features/lesson/PledgeStatusMap.tsx:17 (renders `l.lab`); content/source/data/walkthrough.yaml:344-402 (`labs:`, `lab: step-1`); content/source/concepts/campaign-status-machine.md:25.
- Evidence: page text "Each lab runs one rule on its own database, so no single lab runs this whole path, and the SQL here is simplified from them." Card: "The labs run four: LIVE, then ENDED ...". Table cell shows `step-1` under "Design step 1.1". 2026-10-23 audit README: "The Docker labs are gone."
- Fix: Say "Each design step" and "the steps"; drop the `lab` id column; rename `labs` to `steps` in walkthrough.yaml (update content.test.ts:379-394).
- Acceptance: extend the existing "no longer send the reader to a lab" test (content.test.ts:160) to scan data/*.yaml, concept cards and src/app strings for `\blabs?\b`.

### F8: Status table lists CANCELLED as "used" in steps whose own row says nothing sets it
- Severity: P2
- Where: content/source/data/walkthrough.yaml:345-354 (step-1) and :374-385 (step-6)
- Evidence: step-1 `runs: ... Nothing here sets CANCELLED.` yet `allows:` contains CANCELLED; same for step-6 (`allows` includes CANCELLED, `runs` ends "Nothing here sets CANCELLED").
- Fix: Remove CANCELLED from those two `allows` lists, or reword `runs`. Note content.test.ts:392 requires the union of `allows` to equal all nine loop statuses, so CANCELLED must stay in step 1.2 or 1.4 (it does).
- Acceptance: a test that every status in `allows` appears in that step's SQL or state diagram (the existing diagram test only checks the reverse direction).

### F9: Cast "Reminder job" makes claims no lesson teaches and points at a section that does not mention it
- Severity: P2
- Where: content/source/data/cast.yaml:166-182 (`lesson: {step: 6, anchor: cost}`) vs stage1/26-step-6.md (no "reminder"), stage1/12-r2-notifications.md (no reminder job), data/requirements.yaml:35.
- Evidence: cast: "Send everyone a notice about three days before the charge date.", "Find pledges whose saved card expires before the deadline". Requirements only says "A reminder before the deadline ... are Round 2". The 2026-10-23 review removed "reminder-card claims" from the pages but not from cast.yaml. No stage1 text contains "three days" about reminders.
- Fix: Delete the card, or shrink it to the requirements sentence and link to r2-notifications.
- Acceptance: every cast card's `lesson.anchor` section must contain the card's name or a keyword from it.

### F10: Three new concept cards say they are taught in step 3 but the teaching is in Stage 2 (lesson 11)
- Severity: P2
- Where: content/source/concepts/change-data-capture.md, outbox-delivery-options.md, outbox-wake-up-signal.md (`taught: {step: 3}`) vs their own body links to /p/crowdfunding/2/delivery#approach-d, #approaches, #signal.
- Evidence: `taught:\n  step: 3` (Stage 1 step 3 is the ledger/snapshot lesson); bodies say "See the delivery lesson".
- Fix: The card schema only supports `step`, `page` or `decision`; add a `lesson: stage2/delivery` form to audit.mjs (its PAGES list at audit.mjs:~35 holds only Stage 1 pages) and use it, or mark `taught: {decision: event-delivery}`.
- Acceptance: audit.mjs should fail when a card's body links to a lesson other than its `taught` target.

### F11: Quiz 1.4-q09 has a second defensible answer
- Severity: P2
- Where: content/source/data/quizzes/step-1.4.yaml, 1.4-q09
- Evidence: correct: "The old pledge stays EXPIRED. The backer pledges again with the saved card." Option C: "It ignores the late event, and the backer has to start over and type the card again." Option C is true in its first half (the late event changes nothing) and only the second half is wrong, which is not obvious because the stem does not say the provider keeps the card.
- Fix: Make option C wrong in its first half too (for example "It refunds the card and then lets the backer start over") or add "The card is now saved at the provider" to the stem.
- Acceptance: manual review rule: no distractor may contain the correct outcome verbatim; a test can flag distractors sharing >60% of tokens with the correct choice.

### F12: Step 1.3 quiz numbers are not found by a literal search of the lesson text
- Severity: P3
- Where: content/source/data/quizzes/step-1.3.yaml 1.3-q01 (`'100000'`), 1.3-q06 (`'35818'`, `'4790'`) vs 23-step-3.md:23,359 ("100,000", "35,818", "4,790").
- Evidence: quiz writes `100000`; the lesson writes `100,000`. The test in quiz.test.ts must strip commas to pass; confirm it does, otherwise these three `numbers` entries are decorative.
- Fix: Document the comma-stripping in the test, or write the numbers as in the lesson.
- Acceptance: test that `numbers[].value` normalised (commas removed) occurs in the named section, and fails on a miss.

### F13: Two sources for glossary definitions; the old one has drifted
- Severity: P3
- Where: tools/glossary.json (32 terms) vs content/source/data/glossary.yaml (64 terms); tools/glossary.py and tools/stagebar.py write into `wireframes/` pages only.
- Evidence: `modular monolith`, `schema`, `idempotency key`, `message broker`, `token bucket` differ (for example tools/glossary.json says "one schema per module, here each gets its own" while the yaml adds the "schema marks ownership and does not enforce it" caveat). tools/glossary.py PAGES still lists Mono-Hops etc.
- Fix: Mark tools/glossary.json and the two scripts as legacy (README or delete), so nobody edits the wrong file.
- Acceptance: a check that tools/glossary.json is a subset of the yaml with identical `plain` text, or that it no longer exists.

### F14: Learner-facing provenance dates in lessons
- Severity: P3
- Where: 20 output blocks "# recorded 2026-10-23, PostgreSQL 16.15 ..." in stage2/10-outbox.md (89,117,232,325,429,447,511,520,549,641 and others) and stage2/12-schema.md (58,147,178,261,290,324,337,347,363,404); "checked 2026-09-28" in stage1/21-step-1.md:23,27 and stage1/09-r2-failed-cards.md:44.
- Evidence: docs/reference-pin.md: "Learner-facing text must not mention commit ids, pins, review dates or branch names." The version strings (PostgreSQL 16.15, EF Core 10.0.13) are useful; the dates are provenance.
- Fix: Drop the date from the `# recorded ...` header lines and the "checked <date>" clauses (keep "checked against the provider's docs" if wanted).
- Acceptance: lint regex `20\d\d-\d\d-\d\d` over learner Markdown outside code that is data (the 2027-04-01 deadline and the 2026-12-01 example payload are fine).

### F15: A localhost URL in an operations lesson
- Severity: P3
- Where: content/source/stage2/15-operate.md:55 ("on `http://localhost:5076`")
- Evidence: also "port 5432 or 6379 is already taken, the README says ..." The port is the reference repo's dev setting and can drift.
- Fix: Say "on a local port (the reference README lists it)" or keep with a "default" qualifier.
- Acceptance: lint for `localhost|127\.0\.0\.1|:\d{4}\b` in learner text.

### F16: "Projects in the solution" is 30, 29, 28 or 27 depending on the page
- Severity: P3
- Where: stage2/02-project-map.md:10,296 and stage2/01-start.md:154 ("30 projects") vs stage2/19-audit.md:52 ("27 projects under `src` ... and 28 with the Moderation service"; essay "29").
- Evidence: arithmetic works (22 module + 3 building blocks + host + sample = 27 in `src`; plus 3 test projects = 30) but 02-project-map and 01-start never say that 30 includes the tests.
- Fix: Add "(27 under src plus 3 test projects)" at first mention.
- Acceptance: fact registry entry "project count: 30 total / 27 src".

### F17: Round 1 pledge retry rule is described three ways
- Severity: P3
- Where: stage1/26-step-6.md:70 ("retried a few times over the next day ... We assume three attempts"), 26-step-6.md:184 (`charge_attempt < 3`), stage2/06-jobs.md:386 ("fewer than three attempts"); glossary.yaml `dunning` ("the first round only marks the pledge failed"); stage1/09-r2-failed-cards.md:20.
- Evidence: all agree on 3 attempts; only "over the next day" is not enforced by any SQL (no delay between attempts is modelled, the pledge goes straight back to TO_CHARGE).
- Fix: Drop "over the next day" or say the delay between attempts is not designed in Round 1.
- Acceptance: none automatic; covered by the fact registry (attempts = 3).

### F18: Concept `allow`/`avoid` lists do not cover legitimate uses, so audit flags are noise
- Severity: P3
- Where: concepts/payment-provider.md (`avoid: payment gateway`, no allow) vs data/divergence.yaml:57, stage2/05-guarantees.md:201, stage2/19-audit.md:64 (these describe the reference code's own wording); concepts/slot-hold.md (`avoid: reservation`, allow `divergence`) vs stage2/06-jobs.md:12,274,318 and stage1/24-step-4.md:36 (hotel loop quote), data/errors.yaml:74.
- Evidence: audit.json flags `payment gateway in divergence ...` and `reservation in step-4 ..., errors.http[6].meaning`.
- Fix: Add the reference-code surfaces (`reference`, `stage2`, `errors`) to `allow`, or reword the course-design sentences.
- Acceptance: audit.mjs prints zero variant-name flags; then promote variant-name to a hard error.

### F19: Concept index still carries known gaps the 2026-10-22 status lists as open
- Severity: P3
- Where: content/audit.json (25 used-before-taught, 21 no-quiz, 5 variant-name flags; all 47 cards status "never" audited); docs/audit/2026-10-22/STATUS.md ("15 concepts have no quiz question", "31 used-before-taught"; "Lab 1.1 still previews ideas").
- Evidence: no-quiz list: all-or-nothing, save-card-charge-later, write-skew, change-data-capture, charge-key, declined-card-retry, outbox, outbox-delivery-options, outbox-wake-up-signal, worker-claim, campaign-status-machine, pledge-status-machine, modular-monolith, module-owns-its-tables, on-conflict-do-nothing, clean-architecture, module-contracts, module-data-isolation, orm-role, real-database-tests, schema-authoring. Several of these (write-skew, charge-key, worker-claim) are taught in steps that have a 12-question bank.
- Fix: Add questions for the step-taught ones; sign off cards once clean. Update STATUS.md (it still mentions labs).
- Acceptance: `npm run concepts:check` plus a policy test "every concept with taught.step has a quiz question".

### F20: README says the repo is wireframes only
- Severity: P3
- Where: README.md:5-7 ("Status: wireframes only. No application code yet.") vs README.md:118 and the whole src/ tree.
- Evidence: same file later documents the Next.js app, `npm run dev`, tests.
- Fix: Replace the status block.
- Acceptance: none needed beyond review.

### F21: Walkthrough SQL page and cast/ownership: the close job is shown touching Pledges' tables while "Pledges never decides"
- Severity: P3 (ambiguity, acknowledged elsewhere)
- Where: content/source/data/cast.yaml (close job: `touches: [campaigns, pledge_ledger, pledges]`; webhook: `touches ... pledge_ledger, reward_tiers`) vs data/ownership.yaml (`pledges` owned by Pledges, `pledge_ledger`/`reward_tiers` by Campaigns) and stage1/26-step-6.md:379 ("The ownership page does not list it yet").
- Evidence: the cross-module writes are real in the step SQL and ownership.yaml `splitCosts` explains three of them, but the close job's write of `pledges` and the sentence "We assume the settlement job belongs to the Campaigns module" are only in step 6, and 26-step-6.md:379 says the ownership page does not list it while cast.yaml lists the close job.
- Fix: Add a `jobs` list with the owning module to ownership.yaml and delete the "does not list it yet" sentence.
- Acceptance: a test that every actor of kind "code" in cast.yaml appears in ownership.yaml with a module.

## Checks the repo does not automate yet (but could)

1. Fact registry: one YAML of design constants (hold 15 min, sweeper 10 s, close-job poll 30 s, attempts 3, key retention days, two modules, nine statuses, 22 arch tests, 21 Stage 2 lessons) and a test that greps every lesson/quiz/cast/animation string near the key word and fails on a different number (would have caught F1, F3).
2. Cross-check `aliases` against `avoid` inside concept cards, and make variant-name a test failure (F4, F5, F18).
3. Run the "no labs/Docker/terminal" scan over data/*.yaml, concept cards, content/animations and the strings in src/app and src/features (today content.test.ts:160 covers only step pages and reference.test.ts:39 the reference pages) (F7).
4. Provenance lint on learner text: dates `20\d\d-\d\d-\d\d` outside data samples, `localhost`, ports, 7-40 hex ids, "pin", "branch" (F14, F15).
5. Animation JSON: add content/animations/architecture.json to the DATA table (or a test) so numbers in `intro`/`detail`/`label`/`metrics` are cross-checked and the file has a source of truth (F3).
6. Stale-claim detector for "reference code has no X" statements against divergence.yaml statuses (a status of "Partly built"/"Built" must not coexist with "no X yet" in glossary/lessons) (F6).
7. `taught` target must be the lesson the card body links to; extend audit.mjs PAGES to include Stage 2 slugs and cast-primer (F10).
8. Cast/ownership coverage: every cast actor's lesson anchor mentions the actor; every code actor has an owning module (F9, F21).
9. Quiz hygiene: no distractor that restates the correct outcome; every `numbers.value` matched after comma normalisation; every concept taught in a step has at least one question (F11, F12, F19).
10. Legacy tooling: fail CI if tools/glossary.json diverges from glossary.yaml, or delete the wireframe-era scripts (F13).
