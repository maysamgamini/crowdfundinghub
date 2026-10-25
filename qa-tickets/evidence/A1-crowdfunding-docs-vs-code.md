# Docs vs code audit (crowdfunding-reclaim, branch feat/outbox-reclaim)

Read-only audit. Repo root below is `/Users/maysamgamini/maysam-brain/contents/projects/projects-active/crowdfunding-reclaim`; all paths are relative to it. Test counts are static counts (attribute grep), nothing was built or run.

Result of the doc tests, grouped: broken links = F9, F10; missing backtick files and test classes = F11, F12; unclosed fence = F2; box drawings = F13.

---

### F1: Removed `ICampaignPledgeAvailabilityReader` is taught as the live design
- Severity: P1
- Where: docs/TRADE_OFFS_AND_LIMITATIONS.md:84, 360-385 (section 2.5 says "The architecture solves this via Synchronous In-Process Read Delegation", with three links to files that do not exist), 774; docs/PLATFORM_SHORTCOMINGS.md:396, 429; docs/educational-evaluation.md:539-555, 617, 665; educational/README.md:69; README.md:131; educational/01-architecture-paradigms/monolith-to-microservices-blueprint.md:50
- Evidence: `grep -rn PledgeAvailability src` finds nothing in production code. Only `tests/ArchitectureTests/.../ReplicatedReadModelBoundaryTests.cs:15` says "The anti-pattern this ticket eliminated". `under-engineering-audit.md:55` and `transformation-spec.md:100` say it was eliminated. `MakePledgeCommandHandler` reads the replicated cache (design-to-code-mapping-round1.md:79).
- Fix: In TRADE_OFFS 2.5, rewrite the "Current" text to describe the replicated read model (`pledges.campaign_read_models`) and keep the reader only as "the earlier design". Add a "removed" note to the other places; drop the three dead links. README.md:131 and educational/README.md:69 should say "was" not "is".
- Acceptance: `grep -rn "ICampaignPledgeAvailabilityReader" docs educational README.md` hits only sentences that say it was removed; DocumentationTests link test no longer lists the three `.cs` paths.

### F2: Unclosed code fence in ARCHITECTURE_AND_LIBRARIES leaks a NetArchTest snippet that contradicts the line above it
- Severity: P1
- Where: docs/ARCHITECTURE_AND_LIBRARIES.md:630 vs 639-649
- Evidence: line 630 says "`xunit` only. The `NetArchTest.Rules` library is not referenced". The xUnit snippet at 639-643 has no closing fence and a second block opens at 643 with `Types.InAssembly(...).ShouldNot().HaveDependencyOnAny(...)...Should().BeTrue()`, which is NetArchTest/FluentAssertions code. `grep -rn NetArchTest --include=*.csproj .` finds nothing. The leftover also swallows the following text into a code block.
- Fix: Close the fence after the `Assert.DoesNotContain` line and delete the second (NetArchTest) block.
- Acceptance: DocumentationTests `EveryCodeFenceIsClosed` passes; `grep -n "Types.InAssembly" docs/ARCHITECTURE_AND_LIBRARIES.md` is empty.

### F3: TRADE_OFFS section 2.3 / 3.1 / 3.2 / 4.2 describe an outbox worker that no longer exists
- Severity: P1
- Where: docs/TRADE_OFFS_AND_LIMITATIONS.md:225-249 (sequence diagram), 275-280, 502-531, 538-541, 562, 649-660 (roadmap item 1), 799-805 (checklist), 822-830; docs/ARCHITECTURE_AND_LIBRARIES.md:62, 299 ("PeriodicTimer"); docs/outbox-architecture.md:20-26 (update note); src/BuildingBlocks/README.md:57
- Evidence: Code (`src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Outbox/ModuleOutboxProcessor.cs`): the loop `continue`s at once when a batch is full (BatchSize 20), sleeps on `IOutboxSignal.WaitAsync(pollInterval)` otherwise, uses `Task.Delay` as fallback, and has no `PeriodicTimer`, no `PollInterval` constant and no `MarkProcessed` call (success does `Remove`). Docs still say: ceiling "4 events/s", "300 seconds to drain", "events wait up to 5s", "UPDATE Status = Processed" (TRADE_OFFS:244 and BuildingBlocks README:57 "mark Processed"), 3x MVCC amplification and "purge Processed rows older than 7 days" (rows are deleted on success), and roadmap/checklist items "eliminate the fixed 5-second sleep" and "drain batches immediately" as TODO (already done). outbox-architecture.md:20 still says "independent `PeriodicTimer`s".
- Fix: Rewrite the sequence diagram and the ceiling maths (a full batch is followed at once; latency is milliseconds with the signal, up to 5 s only as the safety net). Replace "Processed" with "row deleted"; MVCC model becomes insert + claim update + delete. Delete the completed roadmap and checklist items. Replace `PeriodicTimer` with "wake-up signal plus 5 s timer".
- Acceptance: `grep -n "PeriodicTimer" docs/TRADE_OFFS_AND_LIMITATIONS.md docs/ARCHITECTURE_AND_LIBRARIES.md docs/outbox-architecture.md` returns nothing for the outbox; `grep -n "4 events\|300 seconds\|Status = Processed" docs` empty.

### F4: Leftover "outbox rows are never reclaimed / lease not read / lease on DB clock" text after the reclaim commit
- Severity: P1
- Where: docs/ARCHITECTURE_AND_LIBRARIES.md:300 ("LockedUntilUtc written, never read"); docs/TRADE_OFFS_AND_LIMITATIONS.md:237 ("LockedUntilUtc = now()+30s"), 249 ("`LockedUntilUtc` is still set from the database `now()`"), 280 ("nothing reads it for the outbox ... stay in Processing"), 579 (links `OutboxClaimQuery.cs#L41`), 830 ("outbox crash recovery are untested"); docs/PLATFORM_SHORTCOMINGS.md:318-336 ("The current outbox implementation ... `now() + interval '30 seconds'`, Status = 1 only"); educational/03-engineering-spec-and-roadmap/design-to-code-mapping-round1.md (gap-table last row "Scavenger and outbox crash recovery untested", last bullet "Still untested: ... outbox crash recovery")
- Evidence: `OutboxClaimQuery.cs` selects `Status = Pending AND ScheduledAtUtc <= {3}` OR `Status = Processing AND LockedUntilUtc < {3}`, writes `LockedUntilUtc = nowUtc + lockDuration` (application clock), and `OutboxReclaimE2ETests` (6 tests incl. `AClaim_WritesItsLeaseOnTheApplicationClock`) covers it. The same files contradict their own fixed text a few lines away (for example TRADE_OFFS:571 and :580 are correct).
- Fix: Update each listed line to the reclaim behaviour (claim also takes expired `Processing` rows, lease on app clock, reclaim counts as an attempt, tested by `OutboxReclaimE2ETests`). Replace the PLATFORM_SHORTCOMINGS SQL with the real two-branch WHERE or mark it "before reclaim".
- Acceptance: `grep -rn "never read\|nothing reads\|still set from the database\|outbox crash recovery" docs educational` returns only history notes.

### F5: Retry/backoff numbers do not match the code
- Severity: P2
- Where: docs/ARCHITECTURE_AND_LIBRARIES.md:307, 323 ("Exponential Backoff"), 369 ("Exponential Backoff + Jitter (Max 5 Attempts)"); contradicts educational/.../design-to-code-mapping-round1.md ("three retries")
- Evidence: Outbox: `ModuleOutboxProcessor.RetryDelay = TimeSpan.FromSeconds(10)` fixed, `MarkFailed` does `ScheduledAtUtc = nowUtc.Add(retryDelay)`. Campaign credit handler: `AddPledgeToCampaignCommandHandler.MaxConcurrencyRetries = 3`, delays 20/50 ms + up to 25 ms jitter.
- Fix: Say "fixed 10 s delay, five attempts" for the outbox and "three attempts with short jittered delays" for the xmin retry.
- Acceptance: `grep -n "Exponential" docs/ARCHITECTURE_AND_LIBRARIES.md` empty or only describing alternatives.

### F6: "Every module has its own outbox/worker/inbox" is false; only 3 of 6 have an outbox and 5 of 6 an inbox
- Severity: P2
- Where: docs/LOCAL_DISTRIBUTION_AND_HA_PLAN.md:24 ("Each module has its own outbox and inbox"); docs/EVENT_DELIVERY_APPROACHES.md:146 ("One outbox table and one worker per module"); docs/TRADE_OFFS_AND_LIMITATIONS.md:296 ("Each module keeps a `<module>_inbox_messages` table"); README.md:150-157 diagram B2 "Per-module schema, inbox"; design-to-code-mapping-round1.md ("one worker per module")
- Evidence: Outbox tables/workers exist only for Campaigns, Pledges, Moderation (`ConfigureOutbox` and `ModuleOutboxProcessor<>` subclasses). `ConfigureInbox` is in Campaigns, Pledges, Moderation, Notifications, CampaignUpdates, not Identity. src/Modules/README.md:19-26 and the module READMEs are correct ("Outbox: none", "Inbox: none" for Identity).
- Fix: Say "each module that publishes events (Campaigns, Pledges, Moderation) has its own outbox and worker; every module except Identity has an inbox".
- Acceptance: grep of the four phrases above shows the qualified wording; consistent with src/Modules/README.md table.

### F7: Project counts and "each module has Contracts" are wrong
- Severity: P2
- Where: docs/educational-evaluation.md:30, 65, 99, 115-120, 337, 676; educational/README.md:67; educational/02-gap-analysis-and-tradeoffs/over-engineering-audit.md:62-64; README.md:45 (mermaid: "Six modules, each with Domain / Application / Infrastructure / Contracts")
- Evidence: Notifications and CampaignUpdates have no Contracts project (`ls src/Modules/Notifications`). Modules = 4+4+4+4+3+3 = 22 projects, not 6x4 = 24. CrowdFunding.slnx lists 30 projects (22 modules + 3 BuildingBlocks + API + Rosetta + 3 test projects); 32 `.csproj` exist on disk (adds `services/CrowdFunding.Moderation.Service` and `CrowdFunding.ModerationService.IntegrationTests`, both outside the slnx). "29" matches neither.
- Fix: Use "30 projects in the solution (32 with the extracted Moderation service and its test)", show 22 module projects, and change the README diagram caption to "Domain / Application / Infrastructure, plus Contracts where other modules consume it".
- Acceptance: `grep -rn "29 \(projects\|assemblies\|.csproj\)" docs educational README.md` returns nothing.

### F8: Test counts disagree between documents and with the code; one test project is invisible to `dotnet test CrowdFunding.slnx`
- Severity: P2
- Where: README.md:236-238 and tests/README.md:11-15 and docs/TESTS_THAT_TEACH.md:10-11 (284 / 22 / 92); docs/TESTS_THAT_TEACH.md:3 ("400-odd"); docs/educational-evaluation.md:335 ("156 passing tests"); README.md:224 ("All of them pass")
- Evidence: Static count today: UnitTests 278 `[Fact]` + 10 `[Theory]` with 27 `[InlineData]` rows (about 305 cases); ArchitectureTests 28 `[Fact]` (22 earlier + 6 in the new untracked DocumentationTests.cs); IntegrationTests 100 `[Fact]` + 2 `[Theory]` (4 rows) (about 104; the reclaim commit added 6 in OutboxReclaimE2ETests alone, README still says 92). `tests/IntegrationTests/CrowdFunding.ModerationService.IntegrationTests` (2 tests) is not in CrowdFunding.slnx and is mentioned nowhere. 284+22+92 = 398 vs "400-odd" vs 156.
- Fix: Run the suite once, put one authoritative number per project in README.md, derive the other three places from it (or drop the numbers and say "see README"). Add the Moderation service test project to the slnx or document the separate command.
- Acceptance: the three documents show identical numbers equal to `dotnet test` output; `grep -rn "156 passing" docs` empty.

### F9: About 55 links point into a `qa-tickets/` folder that was removed
- Severity: P2
- Where: docs/ARCHITECTURE_AND_LIBRARIES.md (TICKET-023, 024, 030 at ~432, 685); docs/PLATFORM_SHORTCOMINGS.md (about 30 links, TICKET-023..040); educational/02-gap-analysis-and-tradeoffs/under-engineering-audit.md (023, 024, 025, 027); educational/03-engineering-spec-and-roadmap/transformation-spec.md (023, 027, 028, 029, 040); educational/04-architect-gold-notes/pedagogical-benefits-and-learning-matrix.md (10 links)
- Evidence: `ls qa-tickets` fails; `git log --all -- qa-tickets` shows commit 3092e16 "remove out-of-scope qa-tickets directory". README.md:166 already says the folder "is not part of this repository" and src/Samples/RosettaStone/README.md:~118 says "the ticket text is not kept".
- Fix: Replace each `[TICKET-0xx](../qa-tickets/...md)` link with plain text `TICKET-0xx` (the ID is still useful), and add one sentence near the top of each affected file saying ticket text is not in the repo.
- Acceptance: DocumentationTests `EveryRelativeLink_PointsAtSomethingThatExists` has no `qa-tickets` entries; `grep -rn "qa-tickets/" docs educational` empty.

### F10: Links to code files that were moved, renamed or deleted
- Severity: P2
- Where: docs/PLATFORM_SHORTCOMINGS.md ("../src/Modules/Campaigns/Contracts/Events/CampaignCancelled/CampaignCancelledApplicationEvent.cs" x2, "../Dockerfile"); docs/TRADE_OFFS_AND_LIMITATIONS.md:366-367, 774 (see F1); docs/educational-evaluation.md:566 and text at 461, 519 (`OutboxProcessorBackgroundService`); improvement.md (OutboxProcessorBackgroundService.cs, GlobalExceptionMiddleware.cs, UnitTest1.cs, DeadLetterHandler.cs, CoachingHub.cs, CrowdFundingWebAppFactory.cs, `logs/hitechies-.json`)
- Evidence and correct targets: `src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Contracts/Events/CampaignCancelled/CampaignCancelledApplicationEvent.cs`; Dockerfiles are `src/API/CrowdFunding.API/Dockerfile` and `services/CrowdFunding.Moderation.Service/Dockerfile`; the worker is `src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Outbox/ModuleOutboxProcessor.cs`; exception handling is `src/API/CrowdFunding.API/Observability/GlobalExceptionHandler.cs`. `UnitTest1.cs`, `DeadLetterHandler.cs`, `CoachingHub.cs`, `CrowdFundingWebAppFactory.cs`, `hitechies` belong to the benchmark project or to the pre-hardening template and do not exist here.
- Fix: Retarget the live ones; in improvement.md (declared historical at line 12) wrap the removed names in plain text rather than links, or add the files to an allow-list in DocumentationTests for improvement.md.
- Acceptance: DocumentationTests link test passes with no `.cs` or `Dockerfile` entries.

### F11: Wrong file paths and test names in backticks
- Severity: P2
- Where: src/Samples/RosettaStone/README.md (`tests/IntegrationTests/CrowdFunding.IntegrationTests/RosettaStone/RosettaStoneParityTests.cs`); educational/03-engineering-spec-and-roadmap/deconstruction-walkthrough.md (`src/Services/CrowdFunding.Moderation.Service/Program.cs`); educational/03-engineering-spec-and-roadmap/design-to-code-mapping-round1.md (`OutboxTests` x2); improvement.md (`CampaignsArchitectureTests`, `PledgesArchitectureTests`, `IdentityArchitectureTests`, `ModerationArchitectureTests`, `NotificationsArchitectureTests`, `CampaignUpdatesArchitectureTests`); src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Infrastructure/Services/README.md (`CampaignPledgeAvailabilityReader.cs`)
- Evidence: the parity tests live at `tests/IntegrationTests/CrowdFunding.IntegrationTests/RosettaStoneParityTests.cs` (no `RosettaStone/` subfolder); the service is under `services/`, not `src/Services/`; `OutboxTests.cs` contains classes `AdvisoryLockKeyTests` and `EventTypeRegistryTests` only, so no class `OutboxTests` exists; the real classes are `CampaignsModuleDependencyTests`, `PledgesModuleDependencyTests`, etc. (DEV_GUIDELINES.md:30 already uses that pattern); the reader file was deleted (F1).
- Fix: Correct each path or name; in the mapping doc cite `EventTypeRegistryTests` and `OutboxMessage` serialization tests by their real class names.
- Acceptance: DocumentationTests `EveryFileNamedInBackticks_ExistsInTheRepository` and `EveryTestClassNamedInBackticks_ExistsInTheTestProjects` list none of these.

### F12: Doc-test failures that are false positives or intended design references (fix the test, not the docs)
- Severity: P3
- Where: `/.well-known/jwks.json` in README.md, ARCHITECTURE_AND_LIBRARIES.md, educational-evaluation.md, key-mental-models-for-students.md, monolith-to-microservices-blueprint.md, over-engineering-audit.md, improvement.md, five src/ and services/ READMEs; `UnitTests`/`ArchitectureTests`/`IntegrationTests` in docs/educational-evaluation.md; `CONT.Domain/...`, `CAMP.Domain/...`, `BB.Infrastructure/...`, `NOTIF.Application/...` and `hispeedsystem/...` in design-to-code-mapping-round1.md; `.gitlab-ci.yml`, `azure-pipelines.yml` in docs/PLATFORM_SHORTCOMINGS.md
- Evidence: the JWKS route is real (`src/API/CrowdFunding.API/Program.cs:288`); the three names are project folders; `CONT`, `CAMP`, `NOTIF`, `BB` are defined as path prefixes at the top of the mapping doc and every expanded path exists (checked all five); `hispeedsystem/...` is an external design file by design; the two CI files are proposals (the real CI is `.github/workflows/ci.yml`).
- Fix: In DocumentationTests skip tokens that start with `/` or contain no dot-extension after a slash, skip a configured abbreviation list, allow-list `hispeedsystem/`, and treat `*Tests` tokens that match a folder name as folders. For the CI proposals write "(proposed)" next to the names.
- Acceptance: DocumentationTests reports only real defects after F1-F11 are fixed.

### F13: Seven documents draw diagrams with box characters
- Severity: P3
- Where: docs/TRADE_OFFS_AND_LIMITATIONS.md, docs/educational-evaluation.md (about 461), educational/02-gap-analysis-and-tradeoffs/over-engineering-audit.md, educational/03-engineering-spec-and-roadmap/deconstruction-walkthrough.md, educational/03-engineering-spec-and-roadmap/transformation-spec.md, educational/04-architect-gold-notes/key-mental-models-for-students.md, improvement.md
- Evidence: DocumentationTests `NoDocumentDrawsADiagramWithBoxCharacters` output; repo rule is Mermaid only.
- Fix: Redraw each as Mermaid (flowchart or sequenceDiagram) or, for directory trees, use plain indented lists.
- Acceptance: that test passes.

### F14: educational/README.md says poly-pattern-architecture.md describes 4 patterns; it now describes 3 tiers
- Severity: P1
- Where: educational/README.md:106 ("3 tiers in code; this folder describes 4") and 109 ("the four-pattern description in `poly-pattern-architecture.md` adds a read-only fast path and event choreography")
- Evidence: educational/01-architecture-paradigms/poly-pattern-architecture.md:48 "Detailed Breakdown of the 3 Tiers", and its note "Reads are not a tier". educational/README.md:59 itself says 3 tiers. docs/POLY_PATTERN_TIER_ASSIGNMENT.md and src/Samples/RosettaStone/README.md also use 3 tiers (tier definitions agree across the three documents).
- Fix: Delete section 4 of educational/README.md or change it to "the three documents agree on three tiers and the same two questions".
- Acceptance: `grep -n "four-pattern\|describes 4" educational/README.md` empty.

### F15: POLY_PATTERN_TIER_ASSIGNMENT says three xmin tokens, then contradicts itself
- Severity: P2
- Where: docs/POLY_PATTERN_TIER_ASSIGNMENT.md:29-30 vs :40 (CampaignUpdates row: "It now has an `xmin` token")
- Evidence: xmin concurrency tokens exist on Campaign, Pledge, RefreshToken, WebhookSubscription, RewardTier and RewardTierReservation (`grep -rn '"xmin"' src` in *Configuration.cs; six files). Events: only Campaign, Pledge and CampaignReview call `AddDomainEvent` (this part is correct).
- Fix: "Six entities carry an `xmin` token: ..." (list all six).
- Acceptance: line 30 lists six; no sentence says "Three ... carry".

### F16: Poly-pattern Tier 3 sample puts the campaign advisory lock in Pledges
- Severity: P2
- Where: educational/01-architecture-paradigms/poly-pattern-architecture.md:139-150 ("// Inside Pledges Application & Domain" ... `pg_advisory_xact_lock` on the campaign id)
- Evidence: `grep -rn pg_advisory src` finds the lock only in Campaigns (`CampaignTransactionExecutor.cs:88`, reward tier handler). Pledges confirm uses an xmin token and a unique webhook key; the campaign balance is credited by the Campaigns handler after the outbox publishes (README.md sequence diagram).
- Fix: Retitle the snippet "Inside Campaigns, crediting a confirmed pledge" or split into the two real steps.
- Acceptance: the snippet's comment names the module that really holds the lock.

### F17: docs/LOCAL_DISTRIBUTION_AND_HA_PLAN.md still reads as an unbuilt proposal with the wrong container counts
- Severity: P2
- Where: docs/LOCAL_DISTRIBUTION_AND_HA_PLAN.md:3 ("Status: proposal. Nothing ... implemented yet"), 21 ("`appsettings.json` has only `DefaultConnection`; ... one PostgreSQL and one Redis"), 43 ("12 PostgreSQL containers (or 14 ...) and 3 etcd"), 45, 119 ("Six PostgreSQL containers"), 146 ("roughly 15 database containers"), 135-147 (open decisions); README.md:19 and the compose headers
- Evidence: `docker-compose.yml` runs one PostgreSQL container and `deploy/postgres/init-module-databases.sh` creates 8 databases; `appsettings.Development.json` has 8 per-module connection strings; `appsettings.HA.json` has 8 multi-host strings; `docker-compose.ha.yml` has 16 Patroni nodes + 3 etcd + Redis. README.md:19 says the same as the code ("16 database nodes"). Memory figures also disagree: README "~3.5 GB" and "Docker Desktop: give it 8 GB", docker-compose.ha.yml:5 "roughly 6 GB", Colima 8.
- Fix: Change the status to "Implemented (P0-P3); drills in TESTS_THAT_TEACH.md", fix the table to what exists (lite = 1 container with 8 databases; HA = 16 + 3 + Redis), mark the owner decisions as taken, and use one memory statement everywhere.
- Acceptance: `grep -n "Nothing in this document\|Six PostgreSQL\|15 database" docs/LOCAL_DISTRIBUTION_AND_HA_PLAN.md` empty; memory number identical in README.md and docker-compose.ha.yml.

### F18: Route and endpoint claims that do not match the controllers
- Severity: P2
- Where: docs/ARCHITECTURE_AND_LIBRARIES.md:731 (`POST /api/pledges/{id}/confirm-payment`); src/Modules/Pledges/README.md:28 (`POST /api/pledges`, while :46 has the right route); src/API/CrowdFunding.API/RateLimiting/README.md:11-17; educational/01-architecture-paradigms/justifying-the-outbox-and-complex-crud.md:57, 87, 105, 115; src/API/CrowdFunding.API/Controllers/README.md (correct, use as reference)
- Evidence: `PledgesController` is `[Route("api/campaigns/{campaignId:guid}/[controller]")]`, so pledge = `POST /api/campaigns/{campaignId}/pledges` and confirm = `POST /api/campaigns/{campaignId}/pledges/{pledgeId}/confirm-payment`. `[EnableRateLimiting(PaymentPolicy)]` is on the pledge POST only (PledgesController.cs:106); confirm-payment is not limited; RateLimiting README omits the third policy `WebhookPolicy` (200/min, applied to both webhook controllers). Webhook routes are `/api/webhooks/payments/stripe` and `/api/webhooks/moderation/media-analysis`; justifying-the-outbox.md uses `/api/pledges/stripe-webhook`, `/api/webhooks/media-analysis-result` (:87) and `/api/webhooks/media-analysis` (:105), two different paths for one call in the same file, plus statuses `PendingMediaAnalysis` and `PendingPayment` that do not exist (real: `CampaignStatus` Draft/Published/Successful/Failed/Cancelled, `PledgeStatus` Pending/Succeeded/Failed/Refunded).
- Fix: Correct the routes; document the webhook policy; remove the claim that confirm-payment is rate limited (or add the attribute); mark the invented statuses as illustrative.
- Acceptance: `grep -rn "/api/pledges" docs educational src --include=*.md` finds none.

### F19: Status names that do not exist
- Severity: P2
- Where: docs/ARCHITECTURE_AND_LIBRARIES.md:159 (`Pending`, `Confirmed`, `Failed`), :736 ("Update Status = Confirmed"); docs/TRADE_OFFS_AND_LIMITATIONS.md:362, 380, 405 ("`Active`")
- Evidence: `PledgeStatus` = Pending, Succeeded, Failed, Refunded; `CampaignStatus` = Draft, Published, Successful, Failed, Cancelled (src/Modules/*/Domain/Enums). design-to-code-mapping-round1.md uses the right names.
- Fix: `Confirmed` -> `Succeeded`, `Active` -> `Published`.
- Acceptance: `grep -n "Status = Confirmed\|\`Confirmed\`\|\`Active\`" docs/ARCHITECTURE_AND_LIBRARIES.md docs/TRADE_OFFS_AND_LIMITATIONS.md` empty.

### F20: PLATFORM_SHORTCOMINGS and educational-evaluation present pre-hardening findings in the present tense
- Severity: P2
- Where: docs/PLATFORM_SHORTCOMINGS.md:278-285 ("all five event handlers are empty stubs", "no notification preference model"), 454-475 (centralized `ProcessOutboxBatchAsync`, "does not implement a drain loop"); docs/educational-evaluation.md (no status banner at all; :335 "156 passing tests", :461/:519/:566 `OutboxProcessorBackgroundService`)
- Evidence: Notifications has 4 stub handlers and working receipt/refund handlers plus a preferences feature (`NotificationsController` preferences routes; design-to-code-mapping-round1.md "Tell people"); `ModuleOutboxProcessor` drains when full. improvement.md and PLATFORM_SHORTCOMINGS have a banner (the latter only lists four changes, not these); educational-evaluation.md has none ("Evaluation Date: September 8, 2026" only).
- Fix: Add the same "point-in-time, superseded by" banner to educational-evaluation.md and extend PLATFORM_SHORTCOMINGS' banner to name the notification and outbox sections as superseded, or edit those paragraphs.
- Acceptance: both files start with a banner pointing to TRADE_OFFS Known Gaps and design-to-code-mapping-round1.md.

### F21: design-to-code-mapping-round1.md keeps a paragraph that denies the inbox it just documented
- Severity: P2
- Where: educational/03-engineering-spec-and-roadmap/design-to-code-mapping-round1.md, "Cross-cutting: consumer inbox" section (the paragraph after the blockquote: "There was no inbox type and no per-consumer processed-event table ...")
- Evidence: the blockquote above it describes `EfInbox`, `InboxRegistration`, `InboxRetryTests`; code has all three. The mapping doc also says "the outbox: one worker per module" (see F6).
- Fix: Reword the paragraph as history ("Before the inbox, consumers relied on...") or delete it.
- Acceptance: no sentence in the section says an inbox does not exist.

### F22: Module READMEs list files and folders that were deleted
- Severity: P3
- Where: src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Infrastructure/Services/README.md (`CampaignPledgeAvailabilityReader.cs`); src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Contracts/Queries/README.md and src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Queries/README.md (child folder `GetCampaignPledgeAvailability`)
- Evidence: a script that checks every "Files"/"Child Folders" bullet against disk found exactly these three misses; `Contracts/Queries` now contains only README.md.
- Fix: Remove the three entries (and the now-empty Contracts/Queries folder README if the folder goes).
- Acceptance: re-running the bullet-vs-disk check prints nothing.

### F23: Moderation service README names MediatR
- Severity: P3
- Where: services/CrowdFunding.Moderation.Service/README.md:26 ("ICommandDispatcher / MediatR")
- Evidence: no MediatR package in any csproj (`grep -rn MediatR --include=*.csproj .` empty); ARCHITECTURE_AND_LIBRARIES.md section 2.3 explicitly chose a custom dispatcher.
- Fix: "ICommandDispatcher (custom dispatcher)".
- Acceptance: `grep -rn MediatR services/*/README.md` empty.

### F24: Outbox row description in ARCHITECTURE_AND_LIBRARIES uses names that are not in the code
- Severity: P3
- Where: docs/ARCHITECTURE_AND_LIBRARIES.md:283-286 ("Fully qualified `EventType`", "Creation timestamp `CreatedAtUtc`")
- Evidence: `OutboxMessage.EventType` is the "event class's simple name, not an assembly-qualified CLR type name" (OutboxMessage.cs comment, resolved by `EventTypeRegistry`); the timestamp column is `OccurredOnUtc`.
- Fix: "stable event-name discriminator plus `Version`" and `OccurredOnUtc`.
- Acceptance: grep for `CreatedAtUtc` in that section is empty.

### F25: Rosetta Stone scorecard line counts are off by one
- Severity: P3
- Where: src/Samples/RosettaStone/README.md (scorecard: 48 and 91 lines)
- Evidence: `wc -l` gives 49 for CreateCampaignMinimalEndpoint.cs and 25+31+36 = 92 for the Tier 2 files. The README says the numbers are `wc -l` results.
- Fix: Update to 49 and 92 (or say "about").
- Acceptance: numbers equal `wc -l` output.

### F26: Failure-mode wording in EVENT_DELIVERY_APPROACHES says "a few seconds" while the code uses a fixed 10 s
- Severity: P3
- Where: docs/EVENT_DELIVERY_APPROACHES.md section 5 point 4 ("Failed messages are rescheduled a few seconds into the future")
- Evidence: `RetryDelay = TimeSpan.FromSeconds(10)` in ModuleOutboxProcessor.cs; poll interval 5 s from `*OutboxBackgroundService` constructors; lease 30 s and five attempts match the docs (verified).
- Fix: "10 seconds into the future".
- Acceptance: figure matches the constant.

---
Checked and found consistent (no finding): outbox batch 20 / lease 30 s / poll 5 s / dead-letter after 5 in EVENT_DELIVERY_APPROACHES, outbox-architecture, BuildingBlocks README; the three-tier definitions across POLY_PATTERN_TIER_ASSIGNMENT, poly-pattern-architecture and RosettaStone README; README HA description vs docker-compose.ha.yml and appsettings.HA.json (8 databases, 16 nodes, 3 etcd, remote_apply, strict mode, `-- migrate`); Idempotency-Key details (header, 1-100 chars, 200 + `Idempotent-Replayed`, 409); package versions in ARCHITECTURE_AND_LIBRARIES section 3; module outbox/inbox table in src/Modules/README.md; DEV_GUIDELINES.md; SignalR Redis backplane note in TRADE_OFFS 2.6 (resolved note is accurate, but see F3 for the stale 3.1 item 5 "Absence of a Redis backplane" at TRADE_OFFS:543-547 that still says strictly one container; fix together with F3).
