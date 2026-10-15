GATE: ARCHITECTURE
HANDOVER: HO-XCT-001
COMMITS: c68d4bbe 61805d27 c9050c84 b5ade66e e8f685e4 d38ba82e 202241b4 (branch ho/XCT-001-build, `git diff a8761829..202241b4`, 190 files; phases A, B, C, D1, E, F)
DATE: 2026-10-12
STATUS: FAIL

This is the first pass of the ARCHITECTURE gate. Under the owner's gate policy (2026-09-27), only BLOCKER and HIGH findings fail the gate. MEDIUM and
LOW findings are proposed below as rows 15-25 of docs/backlog/XCT.md (the file's highest row is 14). This gate did not append them; that is the
integrator's job.

What this pass reviewed:
- `git diff a8761829..202241b4`: every non-generated source file, the five migrations' `Up`/`Down` and `RetentionGuardSql`, the model snapshot by
  table set, `deploy/**` for shape only (secrets and script safety belong to SECURITY), and the changed tests for scope.
- HO-XCT-001 in full, including §1 Amended (README C-5, the 2026-09-26 HO-ADM-001 row), §6, §11–§14 and §20.
- `.ai/`: architecture, cqrs, bounded-contexts, namespace-conventions, di-registration-guide, host-composition, command-handler-pattern,
  read-side-pattern, persistence-ef-core, domain-events-and-outbox, enumeration-pattern and modules/persistence.
- In `.ai/decisions/adr.md`: ADR-341 (per-area partials and modules) and the new ADR-297…316, including the ADR-303 amendment and the ADR-311 and ADR-314 notes.
- Earlier gates' rulings (PHM-001 above all), so that severities stay consistent with them.

Nothing was built or run. The integration suite and the backup drill were not run (instruction: machine memory). Every runtime claim is marked for TEST.

VERIFIED (no finding)
- **No superseded architecture.** None of these appear in the diff: `IDispatcher`, `IBusMessageDispatcher`, `IEventPublisher`, MassTransit, RabbitMQ,
  Mongo/Bson, `QueuePublishOnly`, `202`, rejection events, `IReadRepository`, projection handlers, `[Authorize]`, a CommandServices namespace,
  or a second host. The only matches are the existing warning comments in Program.cs.
- **Dispatch.**
  - All seven controllers inject `IInMemoryDispatcher` (plus `IUserContextProvider`, and `IActiveBranchAccessor` for the snapshot).
  - Every command is stamped `ValidateCommand = true`, in the controllers, the work cycle and the ledger-opening handler.
  - Every aggregate `SaveAsync`/`UpdateAsync` passes `InMemoryPublishOnly`. Two exceptions are deliberate and match ACC precedent: audit rows use
    `DoNotPublish`, and so do web-vital batches (§11.4 C11: no event).
- **One dispatch per action.** Each controller action dispatches one command or one query. The one exception is the snapshot (a query, then
  C10 on a non-304 result), and that is the shape ADR-307 approved.
- **Registration (ADR-341 module).**
  - `CrossCuttingModule` registers the following, all Scoped, by hand: 14 command handlers, each with an `IValidator<T>` (C1–C12, C6 being three);
    11 query handlers (Q1–Q8); and 3 domain event handlers.
  - The Account eraser is registered here. The Notification, Administration, Billing, Ordering and Files erasers are registered in their own modules,
    so §12.7's "×3" is met and exceeded.
  - Also registered: `IRetentionReviewSource` ×1 (with more from other modules), `IFrontDeskSnapshotSource` ×1, `IRetentionPurger` and
    `RetentionWorkQueue` (Scoped), `RetentionWorkCycle` (Scoped), and `AddHostedService<RetentionWorker>()`.
  - `DatabaseRoleGuard` is a Singleton whose dependencies are all Singletons (options, environment, logger), run by an `IHostedService`. It never
    migrates.
  - `OperationsOptions` uses `ValidateOnStart`.
  - Nothing that `AddInMemoryBusServices()` registers is hand-registered. `ValidateScopes` and `ValidateOnBuild` are untouched. No `*.csproj`
    changed and no package was added.
- **Layers.**
  - Domain references no EF Core, Application, Read, Infrastructure or Api type. `IOperationsClock`, `IRetentionPurger` and `MainBranchTime` are
    Domain ports with Infrastructure implementations (the ADR-025 placement the handover names).
  - Application does not reference Read, and Read does not reference Application.
  - No controller holds a `DbContext`, a repository or an aggregate.
- **Aggregates.**
  - The four new roots mutate only through their own methods, with private setters and collections exposed as `IReadOnlyList` over private lists.
  - Every mutating method calls `UpdateVersion()` and raises its specified event. `WebVitalBatchAggregate` raises none (§8.6), and
    `NotificationAggregate.Redact(Retention)` raises none, matching the existing Anonymisation branch.
  - Owned collections are bounded: scopes ≤ 32, passes ≤ 16, checks (one-shot), samples ≤ 10.
  - Invariants that depend only on the aggregate's own state live in the aggregate: hold date strictly in the future, status guards, the one-shot
    restore record, and the RPO/RTO/outcome computation (ADR-313). Unbounded history (runs, batches, restore tests) is held as separate roots
    (bounded-contexts rule 3).
- **Rule-1 exceptions match their ADRs.**
  - C2: the eraser saves its own context's aggregates, then the ledger (ADR-302).
  - C4: a loop over accounts, one scope and one `UpdateAsync` per account (ADR-314).
  - C3: the account plus one directly written `BusinessEvent` row, as §11.4 and HO-ACC-001 C11 specify.
  - Every other command mutates at most one root. C9 is mutated one id at a time by the work cycle; see LOW 8 for the command shape.
- **Persistence.**
  - Every root maps `xmin` as a row version, `ValueGeneratedNever` on root and owned ids, `Events`/`Metadata` ignored, and the ADR-021 optional
    `Tags`/`Language`/`ServiceId`.
  - Children use `OwnsMany`, never `HasMany`. Enumerations are stored by `Value`. Instants are `timestamptz` and dates are `date`.
  - `numeric(8,2)` and `numeric(12,4)` are used exactly as §12.1 states. There is no money column in this slice.
  - Every §12.1 index exists.
  - `BackupRunReadModel` is mapped read-only (`ToTable("backup_run","ops")`, no navigation), and the only code that touches it is the
    `AsNoTracking` read and the purger.
  - Migrations: five, named as in §12.1. Each touches `ops` and `retention` only and attaches the two triggers to the new Forbidden ops tables in
    its own migration (C-ALL-1). The snapshot only adds.
- **Guards (ADR-299).**
  - `retention.refuse_hard_delete()` plus row and TRUNCATE triggers.
  - `HardDeleteGuardInterceptor` sits before change capture.
  - `RetentionPurger` is the only `ExecuteDelete` in `src/`. Each of its methods names its table, and each asserts the table is `Purgeable`.
  - No `DISABLE TRIGGER` and no `session_replication_role` anywhere under `src/`.
  - The exceptions to this list are HIGH 1 and MEDIUM 2–3.
- **Read side.**
  - Every handler projects with `Select` under `AsNoTracking()`, and none writes. Q2 is one parameterised `percentile_cont` statement (ADR-310).
  - Q4, Q6 and Q8 are paged through `PagingHelper`.
  - The snapshot sources are read-side composition across contexts, which read-side-pattern §5 licenses.
  - `IFrontDeskSnapshotSource` sits in `Read.QueryHandlers.Operations`, as §8.11 says.
- **Events.**
  - All new events are past tense and carry ids, enumerations, dates, counts and codes only.
  - No event handler calls `SaveChangesAsync` or reaches outside the process.
  - The Operations handler subscribes to Account's `CustomerAccountAnonymisedEvent` and writes only its own ledger. This is the specified §11.6
    shape, and it is the same one NTF's redaction handler uses.
- **Worker (ADR-303).**
  - A `BackgroundService` in the one host, with a fresh scope and one command per unit, run as `SystemActors.RetentionWorker`.
  - It never throws out of `ExecuteAsync`, and it is disabled in `appsettings.Testing.json`.
  - Statutory classes are never deleted. Purge sources with no rule are not scheduled (the ADR-303 amendment, consistent with ADR-301).
- **Namespaces.**
  - `Operations` is used as the `[Feature]` segment throughout (ADR-297). The context's absence from bounded-contexts.md and
    namespace-conventions.md is OQ-2 and is recorded.
  - `Infrastructure.Retention`, `Infrastructure.Retention.Erasers` (pre-existing, from HO-ADM-001, and permitted by §12.2) and
    `Infrastructure.Operations` are used.
  - Validators are flat. ViewModels and DTOs are flat, in grouped files, following the established grouped-file precedent.
- **Shared-file edits are append-only.**
  - `AuditCaptureExclusions`: the two ops roots only.
  - `NotificationRedactionReason.Retention`; `SystemActors.RetentionWorker`; `RateLimitPolicies` `ping`/`telemetry`.
  - `appsettings.json`: the `Operations` section, with no secret.
  - The `UniqueConstraintExceptionFilter` `RecordIsRetained` mapping.
  - The `AdministrationPersonalDataEraser` rewire to the setting, which that file's own comment named as owed.
  - ADR-297…316 appended, with no deletions.
  - Nine settings, exactly the §10.2 keys.
- **Deferrals are correct (§20.3).**
  - D2 is not built, and `boardingTasks` is reported `NotAvailable`. No boarding table, mock or guessed schema exists in `src/`; the only reference
    is the section-key string.
  - The `boarding-media` purge rule is wired, waiting for HO-BRD-001's source. Backlog rows 1–3 record both.

DECLARED DEVIATIONS, judged
- **ADR-303 amendment** (purge sources with no retention rule here, such as the `stored-file:<purpose>` sources from HO-FIL-001, are not scheduled).
  **Compliant.** It is the ADR-301 reading: no statutory or clinical file is ever purged by the worker. The open question has an owner row
  (backlog 4).
- **ADR-314 note** (the journal is uploaded beside both repositories by the script, using `curl --aws-sigv4` for S3, because pgBackRest 2.59 has no
  file-put). **Compliant.** It stays provider-neutral over the S3 API (NFR-CMP-3) and lives in `deploy/backup/`. That the S3 path is unexercised is
  ADR-311's recorded limit (backlog 5), for TEST and ACCEPTANCE.
- **ADR-297…316 recorded.** All twenty are present and match §9's decisions.
- **D2 / boarding purge deferred** (owner decision 2026-10-10). Compliant, as above.

FINDINGS
- [HIGH] 1. **Six tables classified `Forbidden` are downgraded to `AggregateOnly` by an undeclared override. This is a §20.5 work-around, not a
  report.**
  - **Where:**
    - `src/Shared/CatEcommerce.Shared/Common/Constants/RetentionGuardExemptions.cs:21-31` lists six tables: `billing.invoice_line`,
      `inventory.purchase_order_line`, `inventory.consignment_cost`, `consignment_exchange_rate`, `consignment_landed_cost_line` and
      `consignment_hs_code`.
    - `HardDeleteGuardInterceptor.cs:90-92` treats them as `AggregateOnly`.
    - `RetentionGuardSql.cs:908` leaves them out of the trigger list, so they get no retention trigger.
  - **What the handover requires.** §20.5 says that when a shipped command breaks because of a trigger, "the table is mis-classified or the command
    deletes a record. **Report which.** Never drop the trigger and never reclassify silently". §20 adds that working around a stop instead of
    reporting it is an automatic acceptance FAIL.
  - **Why this is a work-around and not a report.**
    - The developer's diagnosis is probably right: these are draft or re-costable lines, which is rule 4 (`CurrentState`).
    - But the decision is recorded only in a code comment. It is in no ADR, no Amended row and no backlog row, and it is not among the deviations
      declared for this gate.
    - The comment's phrase "The owner decision: Billing and Inventory reclassify…" describes a decision that has no record anywhere.
  - **Architectural effect.**
    - ADR-298 makes `RecordClassification` the single statement of each table's policy. These six now have two policies: `Forbidden` in the
      registry, and `AggregateOnly` in enforcement.
    - So Q3 (`GET /api/admin/retention/schedule`) tells an administrator that six deletable tables are `Forbidden`.
    - The pattern ("add an exemption") will be copied by HO-BRD-001 and HO-SUB-001 the first time their own classification meets a shipped command.
  - **What clears it** (an owner decision; this gate does not choose):
    - (a) Billing and Inventory reclassify the six tables `CurrentState`/`AggregateOnly` in their own `RecordClassification.*.cs` partials,
      through an integrator-authorised edit, and `RetentionGuardExemptions` is deleted. Or:
    - (b) the owner accepts the override as an interim measure. That needs a dated amendment line under ADR-298/ADR-299 naming the six tables, and
      backlog rows against BIL and IMP. Q3 should then report the effective policy.
- [MEDIUM] 2. **A second undeclared exception, which cites an amendment that does not exist.**
  - **Where:** `src/Infrastructure/CatEcommerce.Infrastructure/Retention/HardDeleteGuardInterceptor.cs:40-50`. `ChangeTrackedPurgeAllowList` allows
    a change-tracked delete of `files.stored_file`.
  - **The deviation.** §12.1 says a Purgeable root deleted through change tracking must throw.
  - **The false citation.** The doc comment says the exception is "recorded in ADR-299's amendment". ADR-299 (adr.md:4118) has no amendment.
  - **Why MEDIUM.** The exception is grounded in this handover's own §1 Amended row (README C-5: HO-FIL-001 deletes the object first and the row
    second). It is limited to one Purgeable table and is self-contained.
  - **Fix:** add the amendment line the comment claims exists.
- [MEDIUM] 3. **The `AddRetentionGuards` trigger list is not inlined in the migration.**
  - **Where:** `RetentionGuardSql.cs:909-1024` and `Migrations/20261010194057_AddRetentionGuards.cs:18-24`.
  - **The deviation.** §12.1 says the list "is inlined as literals in the migration … because migrations must be reproducible". Here the migration
    calls `RetentionGuardSql.Up`, which iterates `ForbiddenTablesAtAddRetentionGuards`, a public list in a mutable Infrastructure class.
  - **The risk.**
    - Any later edit to that list silently changes what a historical migration does. Resolving HIGH 1 by route (a) is exactly such an edit.
    - The later migrations call the same class's `AttachTo`/`DetachFrom`. Sharing those helpers is fine; sharing the frozen list is not.
  - **Fix:** move the literal list into the migration file, or pin it with a test (count plus hash) and a do-not-edit comment.
- [MEDIUM] 4. **Scope: 9 files fall outside §13's literal list.**
  - None of them is in §14 forbidden scope. Each needs a §1 Amended record (the PHM-001 and EMR-001 practice).
  - The list is in SCOPE CHECK. `RetentionGuardExemptions.cs` is also the subject of HIGH 1.
- [LOW] 5. **Interceptor order differs from §12.7, and the change touches other areas' lines.**
  - **Where:** `src/Api/CatEcommerce.Api/Program.cs:101-112`.
  - **What §12.7 says.** `HardDeleteGuardInterceptor` goes **first** in the list.
  - **What the code does.**
    - It is third, after ACC's `AuditEntryImmutabilityInterceptor` and PHM's `ControlledDrugRegisterImmutabilityInterceptor`.
    - Those two were moved from after change capture to before it. Those are lines beyond §12.7.
  - **Why only LOW.** The stated intent (before change capture) is met, and the comment explains the change.
  - Record the reorder in an Amended row. TEST should confirm the ACC and PHM immutability tests are still green.
- [LOW] 6. **Two pharmacy tables are excluded from the retention triggers without a declaration.**
  - **Where:** `RetentionGuardSql.cs:901-906`. `DedicatedlyGuardedTables` excludes `pharmacy.cd_register_entry` and `cd_register_seal`.
  - **The deviation.** F33 excludes only `audit.audit_entry`. This extension is undeclared.
  - **Why only LOW.** The protection is equivalent, because PHM ADR-144 has its own DELETE and TRUNCATE triggers.
  - Add a line under ADR-299. I-A3 must accept the PHM triggers for these two tables (TEST).
- [LOW] 7. **The ledger-opening handler dispatches C1 in a new scope.**
  - **Where:** `src/Application/CatEcommerce.Application/DomainEventHandlers/Operations/ErasureEventHandlers.cs:820`.
  - **The deviation.** §11.6 says C1 is dispatched "in the same scope". The handler creates a new one.
  - **Why only LOW.** The behaviour is safe under ADR-021: the anonymisation has already committed. There is precedent in Billing's
    `CaptureChargesOnAppointmentCompletedHandler`.
  - It is undeclared. Record it beside ADR-302.
- [LOW] 8. **C9's command shape allows two aggregates.**
  - **Where:** `RetentionPurgeCommandHandlers.cs:631-662`. `RedactExpiredNotificationContentCommand` carries both `NotificationId` and
    `RequestId`.
  - **The problem.** One command can therefore mutate two roots, against bounded-contexts rule 1. The work cycle sends one id per command
    (`RetentionWorkCycle.RedactAsync`), so no current path does so.
  - **Fix:** make the two ids mutually exclusive in `RedactExpiredNotificationContentCommandValidator`.
- [LOW] 9. **The telemetry controller depends on a handler class.**
  - **Where:** `src/Api/CatEcommerce.Api/Controllers/TelemetryController.cs:5, :51`. The controller imports `Application.CommandHandlers.Operations`
    to read `RecordWebVitalsCommandHandler.Anonymous`.
  - **Fix:** move the anonymous `UserContext` to Shared (beside `SystemActors`), so that the Api depends on commands and not on handlers.
- [LOW] 10. **A violation code is a bare literal.**
  - **Where:** `RetentionPurgeCommandHandlers.cs:610`. `"PurgeSourceNotRegistered"` is a literal and is not in `BusinessViolationCodes.Operations.cs`.
- [LOW] 11. **ADR-297 overstates schema ownership.**
  - ADR-297 says the Operations context owns schema `ops`. But Ordering's `ops.idempotency_record` and `ops.inbound_webhook_receipt` already live
    there, as `RecordClassification.Operations.cs:7` acknowledges.
  - ADR-297 should say that the schema is shared.
- [LOW] 12. **Q3 gives `Regulatory` an end-of-period action of `Redact`.**
  - **Where:** `RetentionQueryHandlers.cs:629`. Q3 reports `Redact` over `retention.financialYears` for `Regulatory`.
  - **The conflict.** ADR-301 says a regulatory record yields a review count only at the end of its period. Only *held personal data* inside it is
    erased (`ReviewOnly` plus a hold).
  - This is wording on a read, so it goes to ACCEPTANCE.

FOR TEST (runtime, not observable here)
- The branch carries five generated migrations, which ADR-341 rule 5 reserves to the integrator. That is established practice: the integrator
  regenerates them. `dotnet ef migrations has-pending-model-changes` must be clean afterwards.
- I-A1: the host builds with `ValidateOnBuild`, covering the hosted `DatabaseRoleGuardHostedService`, `RetentionWorker` and the Scoped cycle and queue.
- I-A3: the expected trigger set is every Forbidden table except the three dedicatedly guarded tables and the six HIGH-1 exemptions. Re-run it after
  HIGH 1 is resolved.
- The interceptor reorder (LOW 5): the ACC audit-immutability and PHM register-immutability tests must stay green, and I-A5 must return
  `409 RecordIsRetained` with no audit row.
- The C2 fresh-scope failure path, where an eraser throws after a partial save: the ledger must still record `UnhandledEraserException`.
- U-A2 (the source scan) must pass with `RetentionPurger.cs` as the only `ExecuteDelete` site.

PROPOSED BACKLOG ROWS (docs/backlog/XCT.md, continuing from row 14)
- 15 MEDIUM — record the `files.stored_file` change-tracked purge exception as the ADR-299 amendment its comment claims exists (finding 2).
- 16 MEDIUM — inline or pin the `AddRetentionGuards` table list so the historical migration cannot change (finding 3).
- 17 MEDIUM — add a §1 Amended record for the 9 out-of-literal-scope files (finding 4).
- 18 LOW — record the interceptor reorder (finding 5).
- 19 LOW — record `DedicatedlyGuardedTables` under ADR-299 (finding 6).
- 20 LOW — record the C1 fresh scope beside ADR-302 (finding 7).
- 21 LOW — make C9's `NotificationId` and `RequestId` mutually exclusive (finding 8).
- 22 LOW — move the anonymous user context out of the handler class (finding 9).
- 23 LOW — move `PurgeSourceNotRegistered` into `BusinessViolationCodes` (finding 10).
- 24 LOW — make ADR-297 say the `ops` schema is shared with Ordering (finding 11).
- 25 LOW — change Q3's Regulatory action-at-end wording (finding 12).

SCOPE CHECK
- files in allowed scope: 181/190. Counted in scope:
  - files inside §13;
  - ADR-341 per-area translations:
    - `Modules/CrossCuttingModule.cs` for `Program.cs` §12.7;
    - `CatEcommerceDbContext.Operations.cs`;
    - the `Actions`, `ActionRoles`, `BusinessViolationCodes`, `SettingDefinitions`, `SettingKeys`, `RecordClassification` and `ErasureExemptions`
      partials (`.Operations.cs` / `.CatProfiles.cs`);
  - grouped files in §13 folders: validators, DTOs, ViewModels, events and the query and handler groups;
  - `appsettings.Testing.json`, which §12.7's text permits;
  - `docs/backlog/XCT.md`, a deferral record under the owner's backlog rule of 2026-09-27;
  - edits to other areas' integration tests, which fall under §13 `tests/CatEcommerce.IntegrationTests/**`. They replace cleanup deletes of rows
    that are now retained, as §16.1 rule 10 expects.
- files outside allowed scope (9, none in §14 forbidden scope — MEDIUM 4):
  - deploy/.gitattributes (§13 allows only `deploy/database/**` and `deploy/backup/**`)
  - src/Domain/CatEcommerce.Domain/DomainServices/IOperationsClock.cs (not among §13's named DomainServices files)
  - src/Shared/CatEcommerce.Shared/Common/Constants/RetentionGuardExemptions.cs (not among §13's named constants; HIGH 1)
  - tests/CatEcommerce.Infrastructure.Tests/Auditing/AuditCaptureExclusionsTests.cs
  - tests/CatEcommerce.Infrastructure.Tests/Retention/HardDeleteGuardInterceptorTests.cs
  - tests/CatEcommerce.Infrastructure.Tests/Retention/HardDeleteSourceScanTests.cs
  - tests/CatEcommerce.Infrastructure.Tests/Retention/RecordClassificationConventionTests.cs
  - tests/CatEcommerce.Infrastructure.Tests/Retention/RetentionGuardExemptionTests.cs
  - tests/CatEcommerce.Infrastructure.Tests/Retention/RetentionGuardMigrationTests.cs
    (§13 lists Domain.Tests, Application.Tests and IntegrationTests only)
- How the out-of-scope files were weighed: the gate brief says that any file outside allowed scope is an automatic FAIL. This record follows the
  PHM-001 and EMR-001 precedent instead, which treats non-§14 files outside the literal list as a MEDIUM needing an Amended record. The verdict is
  FAIL either way, on HIGH 1.
- None of these was touched:
  - `web/**`, `docs/SRS.md`, `docs/USER_STORIES.md` or `docs/handover/**`;
  - `.ai/**` other than an append to adr.md, or `.claude/**`;
  - any `*.csproj`, `*.sln`, `NuGet.Config` or `global.json`;
  - `CustomerAccountAggregate`;
  - `audit.audit_entry`'s triggers or columns;
  - any BRD, EMR, PHM, SRG, BIL, ORD, IMP or SUB aggregate, eraser or purge source.

  No package was added.


---

## Combined re-check (2026-10-12): architecture + security + code-review fix round

GATE: ARCHITECTURE (combined re-check, covering the SECURITY and CODE_REVIEW fix claims; owner gate-scope rule: follow-ups get one combined review)
HANDOVER: HO-XCT-001
COMMITS: cc6510a1 (range 32eedf42..cc6510a1, branch ho/XCT-001-build, 45 files)
DATE: 2026-10-12
STATUS: PASS

**Overall HO-XCT-001 status after this re-check: ARCHITECTURE PASS, SECURITY PASS (b8d75f5d, both mediums now fixed), CODE_REVIEW PASS (7f916e7b).**
ACCEPTANCE has one precondition: the backup drill must be re-run first (see "Backup drill ruling" below).

Governing owner decisions (2026-10-12, verbatim):
- ARCHITECTURE HIGH 1: "Reclassify them properly".
- The two SECURITY mediums: "Fix both before merge".

What this re-check did:
- Read the full diff `32eedf42..cc6510a1`, the new ADR amendments (adr.md:4176-4199), the 2026-10-12 §1 Amended row and backlog rows 15-41.
- Compared the frozen migration list with the pre-fix list.
- Recomputed the pinned hash.
- Checked `restore-mode.env` against the real option sections.
- Checked who owns the 0600 config in the image.
- Tests run (`catecommerce_dev` was not touched; no full suite and no drill were run):
  - Infrastructure.Tests, filtered (classification, migration, interceptor and source-scan tests): 15/15 pass.
  - Domain.Tests, `BillingConventionTests|Inventory`: 320/320 pass.
  - Application.Tests, `Operations`: 54/54 pass.
  - IntegrationTests on `catecommerce_xct`, `XctGuardEndpointTests|XctDatabaseRoleAndSchemaTests|XctErasureEndpointTests|XctRetentionWorkerTests`: 33/33 pass.

### HIGH 1: CLOSED
- **The six tables are reclassified.** `billing.invoice_line`, `inventory.purchase_order_line` and `inventory.consignment_{cost,exchange_rate,landed_cost_line,hs_code}` are now `CurrentState`/`AggregateOnly` in their own partials (`RecordClassification.Billing.cs:27`, `RecordClassification.Inventory.cs:23,26,29-31`).
- **No hidden exemption remains.**
  - `RetentionGuardExemptions.cs` and its test are deleted.
  - The interceptor's override branch is removed (`HardDeleteGuardInterceptor.cs`, formerly :77-79).
  - `grep RetentionGuardExemption` over `src/` and `tests/` finds nothing.
  - The only remaining exceptions are declared ones: `DedicatedlyGuardedTables` (3 tables) and `ChangeTrackedPurgeAllowList` (`files.stored_file`). Both are now recorded under ADR-299.
- **Classification and enforcement agree.**
  - `ClassificationEnforcementAgreementTests` pins the six tables.
  - No non-Forbidden table is in the frozen trigger list.
  - I-A3 (`XctGuardEndpointTests`) now iterates every `Forbidden` table with no filter, and it passes.
  - Q3 reports the six under `CurrentState`, so the schedule endpoint now tells the truth.
- **The BIL/IMP edits are recorded.** They appear in the §1 Amended row, item (1), and in the ADR-298 and ADR-299 amendments (adr.md:4184, :4186-4191). See LOW 44 for two files the record does not name.

### Earlier findings: verified fixed
- **ARCH 2 / row 31.** The ADR-299 amendment for `files.stored_file` now exists, so the interceptor comment's citation is true.
- **ARCH 3 / row 32.** `AddRetentionGuards.TablesAtMigration` is inlined in the migration and marked DO NOT EDIT. `Up` and `Down` no longer read any shared list.
  - It is identical to the pre-fix `ForbiddenTablesAtAddRetentionGuards`: 113 entries, the same order, no duplicates. So the historical migration's effect is unchanged and no migration regeneration is needed.
  - The pinned SHA-256 `b0729284...3986` was recomputed independently and matches.
  - `CreateFunction`, `AttachTo` and `DetachFrom` are still shared helpers. That is acceptable, as the original finding said.
- **ARCH 4 / row 33.** The 9 files are listed in the Amended row, item (2).
- **ARCH 5-7 / rows 34-36.** The interceptor order, `DedicatedlyGuardedTables` and the C1 new scope are recorded under ADR-299 and ADR-302.
- **ARCH 8 / row 37.** True as stated: `RedactExpiredNotificationContentCommandValidator` (`RetentionCommandValidators.cs:64`) already requires exactly one of the two ids.
- **ARCH 9 / row 38.** `TelemetryController` no longer imports `Application.CommandHandlers`; it uses `SystemActors.Anonymous`.
- **ARCH 10 / row 39.** `BusinessViolationCodes.PurgeSourceNotRegistered`.
- **ARCH 11 / row 40.** The ADR-297 amendment says the `ops` schema is shared.
- **ARCH 12 / row 41.** Q3 reports `Regulatory` as `ReviewOnly` (`RetentionQueryHandlers.cs:44`).
- **SEC medium / row 24 (restore re-exposure). Fixed.**
  - The journal is exported every 10 minutes (`schedule.cron`, `5-55/10`, offset from the archive check).
  - Runbook section 4 adds step 0, which exports the journal from the current database before the `--delta` restore.
  - Steps 4-6 are: start with the workers off, reapply through the API, then restart without `restore-mode.env`. The earlier "reconcile before start" contradiction is gone.
  - All seven keys in `restore-mode.env` match a real hosted worker and its bound section (`Notifications`, `Operations:RetentionWorker`, `Clinic`, `Orders`, `Inventory`, `Pharmacy`, `Shop`). No other hosted service acts on customers; the rest are startup guards.
- **SEC medium / row 25 (free text on the immutable audit row). Fixed.**
  - `ErasureIdentityCheck` is an `Enumeration` with 4 codes, and `Reason` is removed from the command, the contract and the controller.
  - The validator is `NotEmpty().MustBeValidEnumeration<..., ErasureIdentityCheck>()`.
  - The audit `reason` and `afterValue` now contain only the two validated enumeration names.
  - The endpoint test proves that a string carrying identifiers is rejected with 400.
  - The contract change for HO-XCT-002 is recorded in the Amended row, item (4). No `web/` consumer exists yet, so nothing is broken.
- **SEC low / row 26.**
  - `curl --config -` with a heredoc `user = "..."`: the secret no longer appears in argv.
  - `render-config.sh` uses `umask 077` and `chmod 600`.
  - The image runs `USER postgres` and every drill entrypoint runs as `postgres`, so the 0600 file is readable by pgBackRest.
  - See LOW 48 for one residual.
- **SEC low / row 28.** `pg_has_role(...,'MEMBER')`. A new integration test creates a NOINHERIT member of the migrator, sees the owned tables, and is refused in Production. The test drops its role in `finally`.
- **CODE_REVIEW 15.** The acceptance date is computed with `clock.LocalDateOfAsync`, and I-B2 asserts that the local acceptance date plus 6 years equals `heldUntil`.
- **CODE_REVIEW 17.** The lost-race retry is implemented (see MEDIUM 42 for its shape).
- **CODE_REVIEW 18.** I-C9 asserts only on its own cat and owner, and has a P0001 negative control.
- **CODE_REVIEW 19.**
  - The SQL pre-filter is a correct superset, with 2 days of slack: the anchor is never earlier than the death or creation date.
  - The exact decision is made on main-branch local dates.
  - `SelectAnchor` is extracted and covered by unit tests.
- **Rows 16, 20-22, 27 and 29** are honestly left open, with reasons.

### New findings (none blocking)
- [MEDIUM] 42. `src/Application/CatEcommerce.Application/CommandHandlers/Operations/OpenErasureRequestCommandHandler.cs:65-75`: **the C1 lost-race retry resolves a command handler from the container and calls it directly.**
  - It resolves `ICommandHandlerAsync<OpenErasureRequestCommand>` in a fresh scope and calls `HandleAsync`. That bypasses `IInMemoryDispatcher`, so no validation pipeline runs.
  - The nested instance runs again with `allowRetry: true`. Recursion is bounded only because `ExistAsync` returned true, which sends the nested call down the existing-ledger branch.
  - The `catch (Exception)` also catches `OperationCanceledException`.
  - Neither existing precedent works this way:
    - `ExecuteErasureScopeCommandHandler.RecordInFreshScopeAsync` re-resolves the repository and finishes inline.
    - The ledger-opening handler dispatches through `IInMemoryDispatcher`.
  - No `.ai/` rule names handler-to-handler calls, so this is not HIGH. But a "handler calls handler" pattern will be copied.
  - **Fix:** in the fresh scope, `GetByIdAsync` the ledger, then `AddMissingScopes` and `UpdateAsync` inline. Exclude `OperationCanceledException` from the catch.
- [MEDIUM] 43. `.ai/decisions/adr.md:4184` (ADR-298 amendment): **the amendment claims a protection that Inventory does not have.**
  - The amendment says "the issued-document immutability stays with their areas' own triggers (for example `trg_invoice_line_immutable`)".
  - That is true for `billing.invoice_line`: `BillingMigrationSql.cs:58-95` allows DELETE only for a draft, non-frozen line.
  - It is false for the five inventory tables. No `CREATE TRIGGER` exists in `src/` on `purchase_order_line` or `consignment_{cost,exchange_rate,landed_cost_line,hs_code}`.
  - So the lines of a submitted or received purchase order (formerly "D-9: money owed to a supplier") and a closed consignment's cost basis (formerly ADR-106) can now be deleted by raw SQL under the app role. Their protection rests on the aggregate methods, and their history on `DataChange` capture.
  - The reclassification itself follows the owner's decision and is correct. Before this fix round these tables had no trigger either, so enforcement is unchanged.
  - The amendment text overstates the protection, though. And C-ALL-4 ("no owned child that is itself a record") holds for these tables only while the parent is a draft. `ConsignmentAggregate.RecordLandedCostAllocation` re-allocates "at any time", and that behaviour belongs to IMP.
  - **Fix:** correct the amendment wording, and open an IMP backlog row for status-conditional DELETE triggers modelled on `fn_invoice_line_immutable`. This needs an owner or IMP decision.
- [LOW] 44. §1 Amended row (2026-10-12): **two fix-round files are not named.**
  - `src/Shared/CatEcommerce.Shared/Common/Enums/ErasureIdentityCheck.cs` is a 13th enumeration beyond §10.1's twelve.
  - `tests/CatEcommerce.Domain.Tests/Common/BillingConventionTests.cs:183-191` is BIL's own convention test. Its D-6 assertion now admits `invoice_line` as `AggregateOnly` and `CurrentState`. The change is correct and consistent with the owner decision.
  - Both are inside §13's paths. This is a record gap only.
- [LOW] 45. `tests/CatEcommerce.Infrastructure.Tests/Retention/RetentionGuardMigrationTests.cs:36-44` and `ClassificationEnforcementAgreementTests.cs:36-41`: **both tests compare the frozen historical list with the current classification.**
  - The next proper reclassification of one of the 113 tables away from `Forbidden` (done correctly, with a later `DetachFrom` migration) will turn both tests red.
  - The only edit that would then make them pass is the one the hash test forbids: editing the frozen list.
  - **Fix:** subtract the tables detached by later migrations, mirroring `LaterMigrationsAttaching`.
- [LOW] 46. `EraseCustomerOnRequestCommandHandler.cs:90-91`: **non-canonical codes reach the audit row.**
  - `MustBeValidEnumeration` parses case-insensitively (`EnumerationParsing.TryParse`, `OrdinalIgnoreCase`), but the handler writes `command.IdentityCheck` and `command.Channel` to the immutable row exactly as received.
  - So `inpersonphotoid` is stored in that form. It is not free text, and the SECURITY fix holds.
  - **Fix:** write each value's canonical `DisplayName`.
- [LOW] 47. `src/Read/CatEcommerce.Read/QueryHandlers/Operations/OperationsStatusQueryHandlers.cs:24-29`: **the backup status read loads too many rows.**
  - With the journal now exported every 10 minutes, `ops.backup_run` receives about 288 rows a day: 144 journal rows plus 144 archive-check rows.
  - The status query loads 60 days of rows into memory, about 17,000, only to pick the latest row per kind.
  - **Fix:** do "latest per kind" in SQL.
- [LOW] 48. `deploy/backup/scripts/render-config.sh:11-12`: **secrets can briefly sit in a world-readable file.**
  - `envsubst > "$target"` followed by `chmod 600` does not narrow a target that already exists with a wider mode until after the secrets are written. That can happen with a file from an earlier plain `envsubst` render, which the runbook allowed before this fix round.
  - **Fix:** write to a `mktemp` file in the same directory and then `mv` it over the target, or `chmod` the target before writing.

### Backup drill ruling: a re-run is REQUIRED before ACCEPTANCE
- **Why it is required:**
  - §16.2 makes the drill mandatory before ACCEPTANCE, and it is the only evidence for I-G1 to I-G8.
  - Two of the scripts changed after the recorded run are on the drill's own path:
    - `test/entrypoint-drill.sh` runs on every drill container start.
    - `test/in-container.sh` runs on every one-off restore or query container.
  - Both now set `umask 077`, and the setting is inherited by `docker-entrypoint.sh`, `initdb`, the postgres server, `archive-push`, `pgbackrest restore` and `pg_ctl`.
  - Those programs almost certainly set their own file modes. But only the drill can show that WAL archiving, the restore and the repo2 journal still work under the new umask.
  - I-G8 runs `export-erasure-journal.sh`, which also changed (only in its S3 branch).
  - The backlog note's "no change that only the drill can verify" does not hold for the entrypoint umask.
  - The recorded RPO and RTO figures should come from the scripts that ship.
- **What the drill does not cover** (ACCEPTANCE should judge these by reading the code and recorded residuals; the drill cannot help):
  - `schedule.cron`: the drill runs no cron.
  - `restore-mode.env`: the application side.
  - The S3 curl path: backlog row 5.
- **Recommendation:** re-run `bash deploy/backup/test/run-drill-test.sh` (I-G1 to I-G8) on `cc6510a1` or later before ACCEPTANCE, and update TC section G with the new measured RPO and RTO. This gate did not run it.

### Proposed backlog rows (docs/backlog/XCT.md, continuing from row 41)
- 42 MEDIUM: C1 retry: finish inline in the fresh scope through the repository, not by resolving the handler; exclude `OperationCanceledException` (finding 42).
- 43 MEDIUM: correct the ADR-298 amendment's "areas' own triggers" claim for the five inventory tables; raise an IMP row for status-conditional DELETE triggers (owner/IMP) (finding 43).
- 44 LOW: name `ErasureIdentityCheck.cs` and the `BillingConventionTests` edit in the 2026-10-12 Amended row (finding 44).
- 45 LOW: make the U-A7 and agreement tests subtract tables detached by later migrations (finding 45).
- 46 LOW: write the canonical `DisplayName` of channel and identity check to the C3 audit row (finding 46).
- 47 LOW: compute backup status "latest per kind" in SQL (finding 47).
- 48 LOW: `render-config.sh`: write to a temp file and move it into place (finding 48).
- Also needed: a TEST/ACCEPTANCE precondition that the backup drill is re-run on the final scripts (drill ruling above).

### SCOPE CHECK
- files in allowed scope: 45/45
  - **Inside §13's paths:** `src/Application/**/Operations/**`, the validators, `src/Api/Contracts/Operations/**`, the §13 controllers, `src/Infrastructure/**/Retention/**`, `src/Infrastructure/**/Operations/**`, the AddRetentionGuards migration (one of the five; its effect is unchanged), `src/Read/**/Operations/**`, `BusinessViolationCodes` (append), `tests/{Domain,Application,Integration}Tests/**`, `deploy/backup/**`, the two runbooks, the TC sheet, and `.ai/decisions/adr.md` (append-only section).
  - **Integrator-authorised under the 2026-10-12 owner decisions and recorded in the Amended row:** `RecordClassification.Billing.cs` and `.Inventory.cs`, `SystemActors.Anonymous`, the Infrastructure.Tests files, `docs/backlog/XCT.md`, and the deletion of `RetentionGuardExemptions.cs`.
  - **`ErasureIdentityCheck.cs`:** owner-decided (SECURITY fix), but not named in the Amended row (LOW 44).
  - **`docs/handover/HO-XCT-001.md`** falls under §14 (`docs/handover/**`). The change is an append-only addition to the §1 Amended row, whose cell says "Append rows here". It was made under integrator authority on the owner decision, following the PHM-001 and EMR-001 precedent. It is counted as an authorised record, not a builder edit.
- files outside allowed scope: none
- Nothing was touched in: `web/**`, `.claude/**`, `.ai/**` other than `adr.md`, `*.csproj`, `*.sln`, `NuGet.Config`, `global.json`, `appsettings*`, `docs/SRS.md` or `docs/USER_STORIES.md`. No BIL or IMP aggregate, eraser or purge source was edited (only their classification partials and BIL's convention test). No package was added.
