# Cross-repo parity audit: crowdfunding-reclaim vs hitechies-backend vs hicleanarch
Read-only. Paths: CF = crowdfunding-reclaim/src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure ; HT = hitechies-backend/src/BuildingBlocks/HiTechies.BuildingBlocks.Infrastructure (all under projects-active/). Nothing was run; every finding is from reading both files.

## Component table
| Component | CF | HT | More complete |
|---|---|---|---|
| OutboxClaimQuery | app clock, schema from EF model, reclaim + attempt count | DB now(), hardcoded schema, reclaim without count | CF |
| ModuleOutboxProcessor | batch gate, trace restore, reclaim dead-letter, per-module signal; no loop try/catch | loop try/catch, DeadLetterHandler, linear backoff; shared signal | CF on delivery, HT on host survival |
| OutboxMessage | Headers (traceparent), Create(event), MarkFailed/MarkDeadLetter | Create(raw json), RecordFailure, no headers | CF |
| Outbox indexes | pending + processing partial indexes | pending partial only | CF |
| Outbox signal | per module IOutboxSignal<TModule> | one global singleton for 4 modules | CF |
| EventTypeRegistry | Type-based, simple-name discriminator + version attribute | explicit Register<T>(name, version) | different design, equal |
| Inbox / idempotent consumer | EfInbox (ON CONFLICT DO NOTHING), wired in publisher, tested | IdempotentConsumer (check then add), unused in src | CF |
| DeadLetterHandler / DeadLetterEvent | table + inline code in processor, no metric, no log helper | handler + metric counter + log, has SourceModule | HT slightly (observability) |
| GlobalExceptionHandler | maps concurrency, unique violation, conflict; no 503 for DB outage; InvalidOperation -> 400 | maps DB overload to 503 + Retry-After; no concurrency/unique mapping | split |
| Broker consumer host | none (publish-only RabbitMQ) | BrokerConsumerHost (Kafka), has loss bugs | HT exists, defective |
| Debezium / CDC | none | 4 connector json files, but README says removed | HT files, docs contradict |
| Health checks | /health/live + /health/ready, per-DbContext | /health, Postgres + Broker check, timeout tests | HT (broker, timeout) / CF (live vs ready) |
| Observability | ActivitySource only, no exporter registered | full OpenTelemetry + OTLP + collector | HT |
| Concurrency token | xmin on Pledge, RefreshToken, WebhookSubscription + ConcurrencyConflictException | IsRowVersion on StudentBalance, retry loops in EfEconomyStore | CF (translated to 409) |
| Tests for outbox | OutboxReclaimE2E (4), WakeUpLatencyE2E, LeanTable, InboxRetry, OutboxTests, OutboxSignalTests | LiveTests (4), EventDeliveryReliability (1000 deliveries), InProcessDispatchReliability, ChannelOutboxSignalTests | different strengths |

## Findings (ranked)

### F1: HT outbox claim compares app-clock ScheduledAtUtc with DB now()
- Severity: P1 (rows invisible to a sweep, delivery delayed by up to the sweep interval; no loss)
- Repos and files: HT Persistence/OutboxClaimQuery.cs:38 (`"ScheduledAtUtc" <= now()`), :47 (`now() + ({2} || ' milliseconds')::interval`); HT Persistence/OutboxMessage.cs:62,73 (`DateTimeOffset.UtcNow` -> ScheduledAtUtc). CF: Persistence/OutboxClaimQuery.cs:44,50,67 (`nowUtc = DateTime.UtcNow`, `<= {3}`).
- Evidence: HT writes ScheduledAtUtc from the app clock and judges it against the DB clock; a container DB a few ms behind makes just-queued rows unclaimable on the sweep that the signal triggers, so the row waits for the 5 s fallback (ModuleOutboxProcessor.cs:48). Retry schedule is also app clock (ModuleOutboxProcessor.cs:128, OutboxMessage.cs:105) while the lease is DB clock, so the three timestamps use two clocks.
- Direction: port CF to HT
- Fix: pass `DateTime.UtcNow` as a parameter for the Pending comparison, the Processing comparison and the new lease, as CF does.
- Acceptance: port CF's OutboxReclaimE2ETests.AClaim_WritesItsLeaseOnTheApplicationClock to HT's ModuleOutboxProcessorLiveTests, plus a test that seeds a row with ScheduledAtUtc = UtcNow + 50 ms and a faked skew and asserts it is claimed once due.

### F2: HT reclaims crashed rows but never counts the reclaim, so a crash-causing message loops forever
- Severity: P0/P1 (poison event that kills the worker is retried forever, no dead letter; blocks 50-row batches repeatedly)
- Repos and files: HT OutboxClaimQuery.cs:44-50 (UPDATE sets Status/LockedBy/LockedUntilUtc only; no Attempts change); HT ModuleOutboxProcessor.cs:131 loop has no attempts-at-claim check. CF OutboxClaimQuery.cs:60 (`Attempts = CASE WHEN m."Status" = Processing THEN Attempts + 1`), ModuleOutboxProcessor.cs:29-37 (dead-letter at `Attempts >= MaxAttempts`).
- Evidence: HT's doc comment (OutboxClaimQuery.cs:11-15) advertises reclaim; only RecordFailure (OutboxMessage.cs:93) increments, and that needs the process to survive.
- Direction: port CF to HT
- Fix: add the CASE increment to the UPDATE and the pre-publish dead-letter guard (reason "Claimed N times without finishing") using HT's DeadLetterHandler.
- Acceptance: port OutboxReclaimE2ETests.AMessageThatKeepsKillingItsWorker_EndsInTheDeadLetterTable and ARowWhoseLeaseHasRunOut_IsReclaimedAndDelivered / ...StillRunning_IsLeftAlone. HT tests grep for LockedUntil/Processing found zero hits, so HT's reclaim is untested.

### F3: HT shares one IOutboxSignal across four processors (wake-ups go to the wrong module)
- Severity: P1 (the advertised sub-5ms latency is not delivered; also violates the channel's SingleReader=true contract)
- Repos and files: HT API/HiTechies.Api/Program.cs:196 (one `AddSingleton<IOutboxSignal, ChannelOutboxSignal>`); HT Outbox/ChannelOutboxSignal.cs:16-21 (`SingleReader = true`); processors in Modules/*/Infrastructure/Outbox/*OutboxProcessor.cs all take the same IOutboxSignal. CF: Application/Outbox/IOutboxSignal.cs:9-13 (one per module, TModule), Infrastructure/Outbox/ChannelOutboxSignal.cs:52-56.
- Evidence: Curriculum commits and calls Trigger() (CurriculumEventPublisher.cs:40); the single slot is consumed by whichever of the four loops reads first, usually not Curriculum, which then sleeps up to 5 s. Four concurrent readers on a SingleReader channel is undefined behaviour. Identity and Economy never call Trigger at all.
- Direction: port CF to HT
- Fix: make the signal generic per module (CF's shape), register one per DbContext, inject into publishers.
- Acceptance: two-processor test: trigger signal A, assert only processor A wakes within 100 ms (CF OutboxWakeUpLatencyE2ETests as the model).

### F4: HT signals before the commit (Coaching) and after SaveChanges without a transaction guarantee
- Severity: P2
- Repos and files: HT Modules/Coaching/.../Events/CoachingEventPublisher.cs:25-32 (adds row, calls Trigger() immediately; caller saves later); HT Curriculum CurriculumEventPublisher.cs:39-40 (signals after SaveChanges; OK only if no outer transaction). CF PledgeTransactionExecutor.cs:62-72 ("Waking before the commit would let the worker look, find nothing").
- Evidence: Coaching's trigger fires before the row is visible; the worker sweeps an empty table and sleeps 5 s.
- Direction: port CF to HT
- Fix: signal from the unit-of-work after commit, not from the publisher.
- Acceptance: unit test with a fake signal asserting Trigger is called after SaveChanges completed.

### F5: HT BrokerConsumerHost commits (drops) a message after 3 failures, and on shutdown
- Severity: P0 when Broker:Enabled=true (silent event loss)
- Repos and files: HT API/HiTechies.Api/EventConsumption/BrokerConsumerHost.cs:142-192, esp. :176 (`catch (OperationCanceledException) { break; }`) then :183-192 commit in both branches; comment :189 says "Dead-letter log" but only logs. Nothing in CF (no consumer host).
- Evidence: poison message -> LogError + `consumer.Commit(result)` (no DeadLetterEvent row); cancellation during backoff `break`s the retry loop, `processedSuccessfully` stays false and the offset is committed for an unprocessed message. Contradicts HT's own claim of "never silently dropped" (DeadLetterHandler.cs:68-72).
- Direction: leave host off in HT until fixed; fix in HT; CF should not adopt until then
- Fix: on exhausted retries write DeadLetterEvent via DeadLetterHandler then commit; on cancellation do not commit; wrap consumers in the inbox (F8).
- Acceptance: unit test of the loop with a throwing consumer asserting a DeadLetterEvent row exists; test that cancel mid-retry does not call Commit.

### F6: CF outbox loop has no exception guard; a database blip stops the host
- Severity: P1
- Repos and files: CF Outbox/ModuleOutboxProcessor.cs:56-80 (no try/catch around ProcessBatchCoreAsync; ClaimPendingBatchAsync and SaveChangesAsync are inside); no BackgroundServiceExceptionBehavior setting found in src. HT ModuleOutboxProcessor.cs:79-107 logs and continues.
- Evidence: a transient Npgsql error in ExecuteAsync faults the BackgroundService, and the .NET default (StopHost) takes the whole API down; lease-reclaim then makes this recoverable only after restart.
- Direction: port HT to CF
- Fix: catch non-cancellation exceptions in the loop, log, and fall through to the wait (keep OperationCanceledException propagating).
- Acceptance: integration test that stops the Postgres connection (or a faulting DbContext decorator) for one sweep and asserts the service is still running and delivers the next row.

### F7: HT unqualified/hardcoded table identity vs CF model-derived qualification
- Severity: P3 (HT is correct today; fragile)
- Repos and files: HT OutboxClaimQuery.cs:37,44 (`"{schemaName}"."OutboxMessages"`, name hardcoded; schema passed as string by each processor, e.g. EconomyOutboxProcessor.cs:21). CF OutboxClaimQuery.cs:41-42 (`Model.FindEntityType(...).GetSchema()`).
- Evidence: HT's schema string and the DbContext's `HasDefaultSchema` (CoachingDbContext.cs:25) are two sources of truth. CF's table name is still a string constant passed in (processor ctor), only the schema comes from the model; ideally both from the model.
- Direction: port CF to HT, then both derive table too
- Fix: read schema and table from the EF entity type in both repos.
- Acceptance: test that renames the schema in a test DbContext and still claims.

### F8: HT IdempotentConsumer is unused and not race-safe; CF inbox is wired and ON CONFLICT-safe
- Severity: P1 (HT docs promise idempotent consumption; code relies on ad hoc natural keys)
- Repos and files: HT Events/IdempotentConsumer.cs:39-54 (AnyAsync then Add; two concurrent deliveries both pass the check, one then fails on the PK as DbUpdateException); no usages in src (grep: only the definition, IEventConsumer.cs doc, EconomyEventConsumer.cs:15 which says it is deliberately not used). CF Persistence/EfInbox.cs:24-43 (INSERT ... ON CONFLICT DO NOTHING), Events/ServiceProviderEventPublisher.cs:38-56.
- Evidence: in CF the inbox row is written AFTER the handler, by a separate statement and not in the handler's transaction (ServiceProviderEventPublisher.cs:51-55): a crash between handler and MarkProcessed re-runs the handler. That is a gap in CF too, partly mitigated by handlers' own idempotency. HT's docs (IdempotentConsumer.cs:45-50) say mark in the same SaveChanges as the effect, which is the correct form but is not enforced.
- Direction: port CF EfInbox to HT (ON CONFLICT), and in CF document or fix the post-handler write (write inside the handler transaction)
- Fix: HT: use CF's EfInbox shape or delete IdempotentConsumer. CF: either document "handlers must be idempotent" or run the inbox insert in the same transaction as the handler.
- Acceptance: CF InboxRetryTests plus a crash-between-handler-and-mark test; HT test with two concurrent deliveries of one EventId asserting one effect and no exception.

### F9: HT docs claim no Debezium, repo ships and registers connectors; course says Debezium reads the table
- Severity: P2 (docs conflict; course states something HT's own docs deny)
- Repos and files: HT README.md:114 and docs/HITECHIES_ARCHITECTURE_SPEC.md:192 ("no Debezium/CDC connector reading OutboxMessages ... removed by TICKET-DEV-025"); HT OutboxMessage.cs:12-15 repeats it; but HT infra/debezium/connectors/*.json (4 files), infra/debezium/README.md ("tails each module's OutboxMessages table"), start.sh:198-220,533 (registers them). hicleanarch content/source/stage2/11-delivery.md:345-381 and content/problems/crowdfunding/stage2/delivery.json: "Who reads the table | the module worker, and Debezium", "B running, D set up as the option".
- Evidence: if the connectors run, the worker deletes rows and Debezium publishes the inserts to Redpanda; with Broker:Enabled the same event is handled twice (once in process, once from the broker). HT docs also say no code publishes to Kafka; the connectors are the only possible publisher.
- Direction: align docs (HT) and fix course wording
- Fix: HT: either delete connectors or mark them as unsupported experiment in README/start.sh. Course: say "connector configs exist in infra, but the repo's own README says CDC was removed from the delivery path; we did not run it".
- Acceptance: grep HT docs for a single consistent statement; course delivery.md has the caveat and matches delivery.json.

### F10: HT DeadLetterEvent record has no reason for reclaim; CF dead-letter lacks metric/log helper
- Severity: P3
- Repos and files: HT Events/DeadLetterHandler.cs:74-119 (log warning + DeadLetterMetrics counter, SourceModule column); CF ModuleOutboxProcessor.cs:140,175 (inline Add, LogWarning/LogError only, no counter), CF Persistence/DeadLetterEvent.cs.
- Evidence: HT can alert on a counter (still process-local, no OTel meter, DeadLetterHandler.cs:106-112); CF only logs.
- Direction: port HT to CF (small)
- Fix: extract a DeadLetterRecorder in CF BuildingBlocks that logs + increments a System.Diagnostics.Metrics counter.
- Acceptance: unit test that recording increments the meter.

### F11: Same class name OutboxMessage, different semantics (ctor, headers, backoff)
- Severity: P2
- Repos and files: CF OutboxMessage.cs:70 (Create(object event, DateTime)), :151-167 (MarkFailed: constant `retryDelay`, 10 s, ModuleOutboxProcessor.cs:31); HT OutboxMessage.cs:60 (Create(eventId, type, version, json, module, DateTimeOffset?)), :91-107 (RecordFailure returns bool, linear `delay * Attempts`, 2 s unit, MaxAttempts 8 at ModuleOutboxProcessor.cs:45, batch 50 vs CF 5 attempts, batch 20). Time types differ: CF DateTime, HT DateTimeOffset. CF stores `Headers` (trace context), HT does not.
- Evidence: ported docs in HT say "ported from CrowdFundingHub" but values differ; the course's "Attempts 5" statements apply to CF only.
- Direction: leave as is and document; if code is later shared as a package, unify
- Fix: add a short "differences from sibling" table to each repo's outbox doc.
- Acceptance: doc check only.

### F12: Same class name ModuleOutboxProcessor, different public contract
- Severity: P2
- Repos and files: CF ModuleOutboxProcessor.cs:26 (implements IOutboxDispatcher, `ProcessBatchAsync` returns Task, `_batchGate` serialises manual and background drains, :44), :83. HT ModuleOutboxProcessor.cs:36,112 (public `ProcessBatchAsync` returns int, no gate).
- Evidence: in HT a test-driven ProcessBatchAsync can overlap the background loop and double-claim is prevented only by SKIP LOCKED; CF's gate exists so tests wait for the in-flight batch. HT returns processed (published) count not claimed count (:191), so a batch of all failures returns 0 and sleeps instead of draining the next rows; CF returns claimed count (:187).
- Direction: port CF to HT (return claimed count; add gate)
- Fix: return `claimed.Count`; add SemaphoreSlim.
- Acceptance: HT test with 60 rows of which the first 50 fail: all 10 healthy rows delivered without waiting for the 5 s sweep.

### F13: Lease (30 s) is not renewed in either repo; slow batch gets reclaimed while running
- Severity: P2 (both)
- Repos and files: CF ModuleOutboxProcessor.cs:30 (LockDuration 30 s, batch 20, SaveChanges once at :186); HT ModuleOutboxProcessor.cs:47 (30 s, batch 50, :190).
- Evidence: rows are only released/deleted at the end of the batch, so a batch whose total publish time exceeds 30 s lets another instance reclaim and republish them (and in CF each reclaim also counts an attempt, pushing healthy slow messages toward dead letter). HT with 50 rows and synchronous in-process handlers (InProcessEventDispatchBus) is more exposed.
- Direction: leave as is and document, or add per-row save in both
- Fix: SaveChanges after each row, or size the lease from batch size x timeout.
- Acceptance: test with a handler delay of 2 s x 20 rows and two workers: each row delivered once.

### F14: Concurrency conflict translation exists only in CF; unique-violation mapping only in CF; DB-outage 503 only in HT
- Severity: P1 (HT unhandled 500 on races), P2 (CF 500/400 on DB outage)
- Repos and files: CF API/Observability/GlobalExceptionHandler.cs:50 (ConcurrencyConflictException -> 409), :54-55 (unique violation -> 409), :41 (`InvalidOperationException => 400`, which also catches EF/Npgsql pool exhaustion wrapped in InvalidOperationException), no 503 path. HT API/Diagnostics/GlobalExceptionHandler.cs:44-54,153-167 (IsDatabaseOverloaded -> 503 + Retry-After:2, :131); no mapping for DbUpdateConcurrencyException or unique violation (falls to 500, :117); EfEconomyStore retries concurrency at :160,266,384... only inside Economy.
- Evidence: in CF a pool timeout surfaces as 400 "Bad Request" with the raw message returned to the client (:40-41 echo exception.Message), which hides an outage and leaks text. In HT a racing second writer outside Economy returns 500.
- Direction: port HT IsDatabaseOverloaded to CF (and drop the InvalidOperationException -> 400 mapping or restrict it); port CF concurrency/unique mapping to HT
- Fix: as above; do not echo message for non-domain exceptions.
- Acceptance: CF unit test GlobalExceptionHandlerTests for NpgsqlException{IsTransient} -> 503 and for InvalidOperationException wrapping a TimeoutException -> 503; HT test for DbUpdateConcurrencyException -> 409. CF has no dedicated handler unit test file today (only ApiPlatformE2ETests/CrowdFundingApiFactory reference it); HT has GlobalExceptionHandlerTests.

### F15: Observability: HT has OpenTelemetry export, CF creates spans nobody listens to
- Severity: P2
- Repos and files: HT API/HiTechies.Api/Observability/OpenTelemetryConfiguration.cs, Program.cs:247, infra/debezium/otel-collector-config.yaml. CF Outbox/ModuleOutboxProcessor.cs:29 (`ActivitySource("CrowdFunding.Outbox")`), no AddOpenTelemetry/AddSource anywhere in src (grep for "opentelemetry" in CF src: no hits).
- Evidence: CF's trace-context-in-outbox-headers work (OutboxMessage.cs:79-88, ModuleOutboxProcessor.cs:197-221) produces null Activities unless something subscribes; StartActivity returns null with no listener, so the restored parent context is dropped.
- Direction: port HT to CF
- Fix: add AddOpenTelemetry with AddSource("CrowdFunding.Outbox") and OTLP exporter; .NET-native (matches the memory preference).
- Acceptance: integration test with an in-memory ActivityListener asserting an `Outbox.Process <EventType>` span whose ParentId equals the request's traceparent (OutboxTests only covers header capture, :205).

### F16: Outbox processing index only in CF
- Severity: P3
- Repos and files: CF Persistence/ModelBuilderExtensions.cs:35-43 (ix_*_pending and ix_*_processing partial indexes, migration 20261023163342_AddOutboxProcessingIndex). HT CoachingDbContext.cs:34 (pending partial index only; same in other three modules).
- Evidence: HT's OR-condition reclaim scan has no index for `Status = 1`; with SKIP LOCKED and lean table this is small, so cosmetic until backlog.
- Direction: port CF to HT
- Fix: add filtered index on LockedUntilUtc where Status = 1 plus migration.
- Acceptance: migration snapshot test / EXPLAIN in an integration test is optional.

### F17: Health checks differ: CF has live/ready split, HT has broker and Postgres-timeout checks
- Severity: P3
- Repos and files: CF API/CrowdFunding.API/Program.cs:70-77,276-290 (/health/live empty predicate, /health/ready tagged); HT Program.cs:238-243,436 (single /health, BrokerHealthCheck only if Broker:Enabled, PostgresHealthCheck with timeout tests UnitTests/HealthChecks/*).
- Evidence: neither checks outbox lag or dead-letter count.
- Direction: port both ways: HT adopts live/ready split; CF adopts the Postgres timeout test; both add an outbox-lag check (oldest Pending ScheduledAtUtc age).
- Acceptance: readiness test returning Degraded when the oldest Pending row is older than N seconds.

### F18: EventTypeRegistry same name, different semantics
- Severity: P3
- Repos and files: CF Application/Events/EventTypeRegistry.cs:28-48 (Register(Type) keyed by simple type name + [EventVersion]; TryResolve(string,int,out Type)); HT Application/Events/EventTypeRegistry.cs:18-43 (Register<T>(string name, int version), TryResolve(EventEnvelope, out object) deserializes inside).
- Evidence: CF fails at dispatch time on rename (needs old name kept registered, doc :14-17); HT names are explicit constants (e.g. SubVariantCompletedV1.TypeName) and survive a class rename. CF's approach silently couples the wire name to the class name.
- Direction: leave as is and document; consider an optional explicit name attribute in CF
- Fix: `[EventName("...")]` override in CF.
- Acceptance: unit test renaming a class keeps old rows resolvable.

### F19: HT docs reference docs/DECISIONS.md that does not exist; stale log messages
- Severity: P3
- Repos and files: HT ChannelMessageBus.cs:20-21, README.md/spec cite "docs/DECISIONS.md section 22"; HT docs/ listing has no DECISIONS.md. HT BrokerConsumerHost.cs:38 logs "In-process channel bus active" though ChannelMessageBus is not registered (Program.cs:197 registers InProcessEventDispatchBus). Spec text says BrokerConsumerHost "was removed as dead weight" while the file exists and is registered (Program.cs:216).
- Direction: align docs (HT)
- Acceptance: link check script over README/spec/comments.

### F20: Course says both designs have an "idempotent receiver"; HT does not
- Severity: P2 (course accuracy)
- Repos and files: hicleanarch content/source/stage2/11-delivery.md ("The two designs agree on the important part: one outbox table per module, an idempotent receiver, a signal that is only a speed-up"); HT EconomyEventConsumer.cs:15-17 (relies on natural keys, IdempotentConsumer deliberately unused), IdempotentConsumer.cs unused (F8); signal is shared (F3).
- Direction: align docs (course)
- Fix: change to "Economy's consumer is idempotent through unique keys; the generic IdempotentConsumer is unused" and note the shared signal means the wake-up is only partly effective. Also "Moving to a broker later ... make each consumer record handled message ids": matches CF EfInbox, fine.
- Acceptance: re-read of the paragraph against HT files.

### F21: Course statements about HT that I verified as true
- Severity: n/a (record)
- Four connectors named identity/economy/coaching/curriculum: true (infra/debezium/connectors). Broker consumer host off by default: true (BrokerConsumerOptions.Enabled=false, BrokerConsumerHost.cs:11,35-39). SKIP LOCKED worker per module with signal and 5 s fallback: true (ModuleOutboxProcessor.cs:48; OutboxClaimQuery.cs:41). Notes say Docker stack was never run in the sandbox: true (infra/debezium/README.md "Sandbox note"). "In-process bus is the default": true (Program.cs:197). The course caveat "we did not run" is adequate. Only F9/F20 wording needs changing; "B running, D set up" overstates D because the README says CDC was removed.

### F22: Delete-on-success and head-of-line handling: parity
- Severity: n/a (record)
- Both delete on success (CF :162, HT :167) and dead-letter then delete (CF :148,178; HT :145,182); neither uses `break` on failure, so no head-of-line blocking in either (CF OutboxMessage.cs:144-150 documents the old defect). Parity confirmed; the difference is HT unparseable payload check only (JsonDocument.Parse) versus CF checking registry membership (TryResolve), so an unregistered type in HT is published and relies on consumers' DeadLetterHandler.HandleUnrecognized (Economy returns early for other types at EconomyEventConsumer.cs:25-28, i.e. an event no module consumes is silently deleted, by design).

### F23: Test coverage differences for the same component
- Severity: P2
- Claim query / reclaim: CF OutboxReclaimE2ETests (4 tests: expired lease reclaimed, live lease left alone, repeat killer dead-lettered, lease on app clock); HT none (grep "LockedUntil|Processing" in tests: 0 hits).
- Concurrency of two processors: HT ProcessBatchAsync_TwoConcurrentProcessors_NeverBothDispatchTheSameMessage (LiveTests:105); CF: no equivalent two-worker test found by name (PledgeAndOutboxE2ETests/OutboxLeanTable not checked line by line).
- Soak: HT EventDeliveryReliabilityTests.OneThousandDeliveries... (:47) and unrecognized-type dead-letter (:101); CF none of that scale.
- Signal: CF OutboxSignalTests (5) + OutboxWakeUpLatencyE2ETests (latency); HT ChannelOutboxSignalTests (6, incl. concurrent bursts) but no latency/per-module test.
- Inbox: CF InboxRetryTests (3); HT none for IdempotentConsumer.
- Exception handler: HT GlobalExceptionHandlerTests (10 facts incl. DB overload); CF none dedicated.
- Direction: port tests across as listed in F1-F3, F8, F14; add a CF two-worker SKIP LOCKED test and a CF 1000-delivery soak.

### F24: Dead-letter reason recorded for unresolvable rows differs
- Severity: P3
- Repos and files: CF ModuleOutboxProcessor.cs:140 (Add DeadLetterEvent with reason), HT DeadLetterHandler.HandleOutboxDispatchFailure (:94-103, FailureReason set, nullable column) vs consumer-side HandleUnrecognized (:76-86, FailureReason null).
- Evidence: HT DeadLetterEvent.FailureReason is nullable (:33) and null for consumer-side rows, so operators cannot distinguish "who dead-lettered it"; CF column is required (ModelBuilderExtensions.cs:64) and always set.
- Direction: port CF to HT (set a reason in HandleUnrecognized)
- Acceptance: unit test that HandleUnrecognized sets non-empty FailureReason.

## Port plan (direction summary)
CF -> HT: F1, F2, F3, F4, F7, F8 (EfInbox), F12, F16, F24, tests of OutboxReclaimE2E.
HT -> CF: F6 (loop guard), F10 (dead-letter metric), F14 (503 mapping), F15 (OpenTelemetry), F17 (Postgres timeout test, outbox-lag check).
Docs only: F9, F11, F18, F19, F20 (course + HT README).
Fix first in HT before enabling: F5 (BrokerConsumerHost loss).
Note: F13 applies to both, unresolved in either.
