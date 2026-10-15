GATE: ARCHITECTURE
HANDOVER: HO-RPT-001
COMMITS: 01c3f77b e9d56daa 033791ae 082b5684 2709ea13 16fc8a7a 2d1d5ba9 fdd776bd 28c59e18 c6826939 283b3968 3fcff556 425f0939 (branch ho/RPT-001, range b12fd39d..425f0939, 201 files)
DATE: 2026-10-13
STATUS: FAIL

This is the first ARCHITECTURE pass. Owner gate policy (2026-09-27): only BLOCKER and HIGH findings fail a gate. MEDIUM and LOW findings
are proposed below as docs/backlog/RPT.md rows, prefixed "ARCHITECTURE:".

The gate fails on **two HIGH findings and no BLOCKER**.
1. The schedule read side rehydrates aggregates. CAT, NTF, SHP, EMR, BIL, IMP and SRG were failed for the same thing.
2. The scheduled-run command (C5) mutates a second aggregate, HO-FIL-001's stored file, by dispatching another context's command from
   inside the handler.

The reporting core complies with the approved shape: read-only views, the READ ONLY scope, one pipeline, explicit registration, the worker
and the NTF amendment. A delta re-check of the two HIGH sites is enough.

What this pass reviewed:
- HO-RPT-001 §1, including the 2026-10-13 build Amended row (1)-(11), and §2-§20 in full.
- The Step 0 Amended rows on HO-NTF-001, HO-ADM-001 and HO-ACC-001.
- HO-FIL-001 §1 and §10, for the consumer contract.
- `.ai/`: architecture, cqrs, bounded-contexts, namespace-conventions, di-registration-guide, command-handler-pattern, read-side-pattern,
  persistence-ef-core, domain-events-and-outbox, enumeration-pattern and host-composition. Also ADR-341 and the appended ADR-277 to
  ADR-292.
- `git diff b12fd39d..425f0939`:
  - every non-generated source file;
  - all 26 view SQL files;
  - the eight migrations' `Up`/`Down`;
  - the model snapshot, checked by its table set.
- Earlier rulings, for consistent severities: SRG-001, FIL-001, BIL-001 and APT-001 ARCHITECTURE.
- The concurrent CODE_REVIEW (8e52b7c1) and SECURITY (1f2eaf4e) records for this handover, so their findings are not duplicated.

Executed: `dotnet test tests/CatEcommerce.IntegrationTests --filter ReportingConventionTests|ReportingWorkerTests|AttachmentLoggingTests`
gave 10/10 passed (plain unit tests, no database). The solution builds. Nothing else was run. Runtime claims are marked for TEST.

RULINGS ON THE BUILDER'S ADAPTED CONTRACT MISMATCHES
1. **BusinessLineRules over BIL's `business_line`/`item_kind`, with no `InvoiceLineKind`: COMPLIANT.**
   - ADR-283's intent is kept. Lines are classified per line, not per invoice, and an unknown value is `Other`, never dropped.
   - `BusinessLine` is HO-BIL-001's existing enumeration, and Reporting adds no member or duplicate type.
   - Deposits and vouchers do not exist in v1 (backlog 9). Recorded in Amended (1) and ADR-283.
2. **R13 is derived: COMPLIANT.**
   - It is a pure computation over committed payments, remittances and cash-ups, inside the read-only scope. Reporting persists no status
     and does not become a second owner of reconciliation.
   - The "mismatches resolved" figure is omitted, not faked. Recorded in Amended (2) and backlog 10.
3. **R7 reuses `ReorderReads`/`ReorderSuggestionCalculator`: ACCEPTED, with a MEDIUM (row 41).**
   - The calculator is exactly the §8.4 contract: a pure HO-IMP-001 service, called and not re-implemented.
   - `ReorderReads` is different. It is an `internal` helper inside IMP's query-handler file, not a published query. It reads
     `inventory.*` base tables directly.
   - `StockReportSources.cs:130` also reads `db.Suppliers` directly.
   - Both bypass the `report.rv_*` view contract (§8.5 step 3, §12.6, ADR-278/279 "view DbSets only"). Writes are still impossible
     (READ ONLY scope).
   - Recorded for `ReorderReads` in Amended (3), ADR-278 and backlog 11. `Suppliers` is not recorded.
4. **Landed cost from `consignment_landed_cost_line`: COMPLIANT** with ADR-284. It deliberately avoids `v_batch_unit_cost`'s purchase-cost
   fallback (backlog 12). TEST: confirm there is at most one landed-cost line per batch. Otherwise `rv_batch.v1.sql`'s `LEFT JOIN`
   duplicates batch rows and inflates R5/R6 and the margin figures.
5. **`rv_customer_activity` resolves merged customers: COMPLIANT** as an adaptation. It is view SQL, read-only, and recorded in ADR-291
   and backlog 13. The one-hop limit is CODE_REVIEW row 28; I agree and do not repeat it.
6. **Extra views `rv_branch`, `rv_staff_branch`, `rv_credit_note`, `rv_refund`, `rv_order_return`: COMPLIANT.** Each is `CREATE VIEW report.*`
   only, with no DDL elsewhere. Recorded in Amended (6) and ADR-278.
7. **Report files through HO-FIL-001 `ReportExport`, with no `report_file` table: COMPLIANT as a destination.** README C-5 closed OQ-12 and
   amended ADR-289. The *mechanism* inside C5 is HIGH 2.
8. **ReportingModule registers `INotificationAttachmentResolver`: COMPLIANT.**
   - §12.7 lists `NotificationAttachmentResolver` (Scoped) among this slice's registrations.
   - It is registered exactly once, and `NotificationsModule` is not edited.
   - LOW (row 44): the module's own comment at ReportingModule.cs:134 says the opposite. Also, HO-NTF-001's deliver handler now resolves a
     dependency that only ReportingModule registers. `ValidateOnBuild` would catch a removal.
9. **U-G9 in `IntegrationTests/Unit`: COMPLIANT.** §15 says "Infrastructure and Api classes as plain unit tests in IntegrationTests", and
   `DevelopmentChannelSender` is Infrastructure.

STEP 0 INTEGRATOR AMENDED ROWS (OQ-15)
- **Append-only: verified.** For HO-ACC-001, HO-ADM-001, HO-NTF-001 and HO-RPT-001's own row, the old cell text is a byte-for-byte prefix
  of the new one. Nothing was removed or reworded.
- **Numbering continues** with no gaps or reuse: ACC (63)-(64) after (62), ADM (51) after (50), NTF (68)-(73) after (67).
- **HO-ACC-001: accurate.**
  - The ten actions and their role sets match `ActionRoles.Reports.cs` row for row.
  - `ReportExported` is a `BusinessEvent` on entity type `Report`, matching `RecordReportExportCommandHandler`.
- **HO-ADM-001: accurate.** The three `reporting.*` keys, the `Reporting` group, the bounds, the defaults and "no branch override" all match
  `SettingDefinitions.Reporting.cs`.
- **HO-NTF-001: accurate except for three points that the build contradicts** (MEDIUM, row 42):
  - (68) says `AttachmentId` identifies the HO-FIL-001 stored file. As built, it is the **run id**, and `ReportRunAttachmentSource`
    resolves run to file (RPT Amended (8), ADR-288).
  - (71) says the guard checks that the recipient account "is active". No such flag exists. The guard checks roles and that the file
    still exists (ADR-288, backlog 18).
  - The preamble says the amendment "appends inside the lines HO-RPT-001 §13 lists". But `EfNotificationOutbox.cs`, which §13 does not
    list, was also changed (see SCOPE).

  The coordinator should append a correction row. The existing text must not be edited.

VERIFIED (no finding)
- **No superseded architecture.** A sweep of every changed `src/**` file found none of these:
  - `IDispatcher`, `IBusMessageDispatcher` or `IEventPublisher`;
  - MassTransit, RabbitMQ, Mongo/Bson or `QueuePublishOnly`;
  - `202`, `CommandServices`, `ErrorEvents`, `IReadRepository` or `ViewModelBase`;
  - a projection `IEventHandlerAsync`, `[Authorize]`, `HasMany` or `AddServiceBusProvider`;
  - `ExecuteUpdate`, `ExecuteDelete`, `FromSql`/`ExecuteSql` or `SaveChanges` in reporting code;
  - a second host.
- **Layers.**
  - Domain references no EF Core, Application, Read or Api type.
  - No changed Application file references Read or EF Core.
  - No changed Read file references Application.
  - The Api runner orchestrates query-then-command, as §6 directs.
  - Namespaces use the `Reporting` segment.
- **Read-only reporting scope.**
  - `ReportingReadScope` runs `SET TRANSACTION READ ONLY; SET LOCAL statement_timeout` on the context's own transaction and always rolls
    back. 57014 maps to `ReportTimedOutException`; 25006 is logged and rethrown.
  - Every report source and all 18 tile sources read only keyless `Rv*Row` sets, except R7 (ruling 3).
  - The catalogue, run and dashboard handlers run inside the scope.
- **Views and migrations.**
  - All 26 SQL files are `CREATE VIEW report.*` with nothing else. Enumeration values are substituted from the enumerations
    (`{{TOKEN}}`), never typed as literals.
  - The `Down` methods drop the views.
  - The only DDL outside `report` is `AddNotificationAttachments`: four nullable columns in `notify.*`, the §11.7 amendment.
  - `AddReportSchedules` creates only `report.report_schedule` and `report.scheduled_report_run`.
  - The snapshot gains exactly those two tables plus keyless `ToView` entries. No `rv_*` table was created (F-RPT-3).
  - The extra migration `AddDashboardReportViews` and the missing `AddOperationsReportViews` are recorded in Amended (7) and backlog 15.
    That follows the SRG precedent: not §14.
- **Aggregates.**
  - Every state change is inside a `ReportScheduleAggregate`/`ScheduledReportRunAggregate` method that ends with `UpdateVersion()`.
  - Schedule invariants live in the aggregate. Recipient rules are in the handler, as §8.9 says.
  - Roots reference each other by id. `recipient_ids uuid[]` and `columns_included` are bounded primitive collections.
  - `xmin` is mapped via `ConfigureAggregateBase`, instants are `timestamptz`, and enumerations are stored by `Value`.
  - The slot index filter is built from the enumerations.
  - No domain events, and no domain event handlers (§11.5/§11.6).
- **Controllers.**
  - Every action dispatches through `IInMemoryDispatcher`, and no controller holds a `DbContext`, repository or aggregate.
  - The extra dispatches are the ones the handover specifies:
    - an export is the query, then C1 (the audit before streaming, ADR-285);
    - a category denial is the query, then HO-ACC-001's denial-audit command (§12.3);
    - `run-now` is the §8.9 sequence (§12.4).
- **Host (ADR-341).** `Program.cs` is untouched. `ReportingModule` registers everything explicitly:
  - 21 `IReportSource`s, 1:1 with the 21 delivered definitions;
  - 18 `IDashboardTileSource`s;
  - 9 command handlers, with 9 validators as `IValidator<T>`;
  - 11 query handlers, the guard, the attachment source and resolver, the run store, the runner, `ReportingWorkCycle` (Scoped) and
    `AddHostedService<ReportingWorker>()`;
  - options with `ValidateOnStart`, the two rate-limit policies and the timeout filter.

  Renderers and the catalogue are Singletons (§12.7); everything else is Scoped. Nothing on the never-register list is registered.
- **ADR-341 partials.**
  - `Actions`, `ActionRoles` (discovered through the `*Rows` reflection), `BusinessViolationCodes`, `SettingDefinitions`, `SettingKeys`,
    `NotificationTemplateCatalogue` and `ReportCatalogue` (discovered through `*Definitions`) are per-area `.Reports`/`.Reporting`/
    `.<Category>` partials.
  - `NotificationTemplateCatalogue.cs` gains only the one spread line.
  - No package was added. The only `csproj` change is the `EmbeddedResource` line §13 allows.
- **Worker shape.** `ReportingWorker` is a `BackgroundService` that matches `NotificationWorker`:
  - it creates a scope per pass, and `ReportingWorkCycle` creates a scope per schedule;
  - it never throws out of `ExecuteAsync`, and it is disabled in `Testing`;
  - schedules run sequentially, at most `MaxSchedulesPerPass` per pass (ADR-290);
  - the cycle is callable from tests.

  The purge pass is deferred (backlog 5).
- **The HO-NTF-001 amendment is within §11.7.**
  - The attachment fields are on the request, the notification and their DTOs, and are copied by Expand and by Deliver's fallback.
  - Email-only load before send. A missing file cancels the notification as `SubjectNoLongerApplies`.
  - `DevelopmentChannelSender` logs the name and size only.
  - `ReportLink`, the template, `ScheduledReportGuard` and `NotificationSubjectKind.ScheduledReport (10)`.
  - No bytes enter `notify.*`.
- **Governing documents.** In `.ai/**`, only `decisions/adr.md` changed. That change is append-only: one hunk at the end of the file,
  0 removed lines, LF line endings. `web/**`, the SRS and the user stories are untouched.
- **ADR numbering (for the integrator): no collision.**
  - ADR-277 to ADR-292 lie inside RPT's README-assigned 277-296.
  - Master defines none of 277-296, and no other local branch defines any number in 277-296.
  - XCT-001-build uses 297-316 (plus the shared 317-320), POR-001 uses 257-268 and SRG-001 uses 157-173. None overlaps.
  - Separately, and not caused by RPT: master already has duplicate `## ADR-116` headings, and several branches carry duplicate 179,
    204 and 216 headings. The integrator may want to renumber them.

FINDINGS
- [HIGH] src/Read/CatEcommerce.Read/QueryHandlers/Reporting/ReportScheduleQueryHandlers.cs — **the schedule read side materialises whole
  aggregates.** This breaks ADR-012 and `.ai/read-side-pattern.md` §4 rules 1-2 ("project, never rehydrate"; "`Select` before
  `ToListAsync`"). CAT, NTF, SHP, EMR, BIL, IMP and SRG were failed for this same class of finding. Sites with no `Select` before
  materialising:
  - :67 — `GetReportSchedulesQuery`: the page of `ReportScheduleAggregate`.
  - :95, :100, :103 — `GetReportScheduleQuery`: the schedule, then whole `StaffAccountAggregate` rows, then
    `StaffBranchAssignmentAggregate` rows. Those are the Account context's roots, including credential state the read does not need.
  - :138 — `GetReportScheduleRunsQuery`: the page of `ScheduledReportRunAggregate`.
  - :169, :171 — `GetEligibleReportRecipientsQuery`: **every** `StaffAccountAggregate` and **every** `StaffBranchAssignmentAggregate`,
    unfiltered and unpaged. Role filtering then happens in memory. This is also the UI-4 unpaginated-list rule.
  - :246, :250 — `GetReportSchedulePlanQuery` (used by the worker and run-now): the schedule and the staff roots.

  Fix: project the needed columns (ids, names, `RoleIds`, `EmailAddress is not null`, `BranchIds`) into anonymous types or ViewModels
  before materialising. Push the branch-assignment filter into SQL. The report sources and tiles are clean: they read keyless view rows,
  not aggregates. Nothing here writes, and every read is `AsNoTracking`.
- [HIGH] src/Application/CatEcommerce.Application/CommandHandlers/Reporting/ScheduledReportRunCommandHandlers.cs:34, :104-133, :114,
  :148, :91 — **C5 `RecordScheduledReportRunCommandHandler` mutates two aggregates by dispatching another context's commands from inside
  the handler.** This breaks `.ai/bounded-contexts.md` rule 1 and command-handler-pattern rule 7.
  - The handler injects `IInMemoryDispatcher` and sends HO-FIL-001's `RegisterStoredFileCommand`. That command creates and commits a
    `StoredFileAggregate` in its own save, before the handler creates and saves the `ScheduledReportRunAggregate` (:91).
  - On the duplicate-slot path and on a domain refusal, it sends `DeleteStoredFileCommand` as compensation (:148). A failure between
    the two saves leaves an orphan file (no compensation on a thrown save).
  - So the two roots are not committed together. The compensation logic is the symptom.
  - §6 of this handover states the rule this slice must follow: "the scheduled-run sequence uses two commands, not one two-aggregate
    command". FIL-001 ARCHITECTURE failed `AddCatPhotoCommandHandler` as HIGH for mutating a stored file and a second root in one command.
  - ADR-289 (amended) fixes the *destination*, HO-FIL-001. It does not record a one-command, two-root exception. The builder-written
    ADR-277/ADR-289 text widens "writes nothing outside `report` except audit and notifications" to include the stored file. A builder's
    own ADR is not an approved exception (the SRG ConsentRules precedent).
  - This will propagate: Reporting is the first server-side producer of FIL files, so the next producer copies this shape.
  - The compliant shape is the one the handover already uses for this sequence: the Api runner (`ScheduledReportRunner`) orchestrates
    one-aggregate commands in order. Store and register the file as its own step (`IFileStore.PutAsync` plus `RegisterStoredFileCommand`,
    as `FilesController` does), then dispatch C5 with the resulting `FileId`. C5 then creates only the run and enlists the notification
    requests (F23).

  If the owner prefers to keep the current shape, record it as an owner or integrator exception in §1 Amended, with the orphan-file
  consequence stated.
- [MEDIUM] src/Application/CatEcommerce.Application/CommandHandlers/Reporting/ReportScheduleCommandHandlers.cs:44, :84, :107, :136, :151,
  :173; ScheduledReportRunCommandHandlers.cs:232; src/Infrastructure/CatEcommerce.Infrastructure/Reporting/EfScheduledReportRunStore.cs:20 —
  **every non-audit Reporting save passes `EventPublishOption.DoNotPublish`.** command-handler-pattern "Summary of rules" 1 says "Always pass
  `InMemoryPublishOnly`". The precedent allows `DoNotPublish` only for audit-row writes (C1 at RecordReportExportCommandHandler.cs:44 is
  fine). This is harmless today, because no Reporting root raises an event. But the first event anyone adds would be silently dropped.
  Backlog row 40.
- [MEDIUM] src/Read/CatEcommerce.Read/QueryHandlers/Reporting/Sources/StockReportSources.cs:120, :127, :130 — **R7 reads Inventory base
  tables inside a report source.** It goes through `ReorderReads` (an `internal` IMP helper, not a published contract) and through
  `db.Suppliers`. That bypasses the `report.rv_*` contract (§8.5 step 3, §12.6, ADR-278/279). IMP can then change R7's figures or break
  its compile without touching a view. `ReorderReads` is recorded (Amended (3), backlog 11); `Suppliers` is not. Either have HO-IMP-001
  publish the reorder read (or a view), or record both in §1 Amended. Ruling 3 explains why this is not HIGH. Backlog row 41.
- [MEDIUM] docs/handover/HO-NTF-001.md:§1 Amended (68), (71) and its preamble — **inaccurate against the build**: run id versus file id,
  the "active" flag, and the unlisted `EfNotificationOutbox.cs` (see STEP 0). The rows are append-only, so the coordinator should append a
  correction rather than edit them. src/Infrastructure/CatEcommerce.Infrastructure/Notifications/EfNotificationOutbox.cs:41-43 is an
  HO-NTF-001 file outside §13's line list. It is counted in scope by the SRG/EMR precedent (it carries out §11.7 item 1: without it,
  `Enqueue` drops the two fields), but it must be recorded. Backlog row 42.
- [MEDIUM] Scope record — **four files outside §13's literal list are not named in §1 Amended.** None of them is §14.
  - src/Api/CatEcommerce.Api/Background/ScheduledReportRunner.cs: the §8.9 steps 2-7, shared by the worker and run-now.
  - src/Domain/CatEcommerce.Domain/DomainServices/IScheduledReportRunStore.cs: replaces `IReportFileStore`.
  - src/Shared/CatEcommerce.Shared/Common/Enums/ReportParameterType.cs.
  - src/Api/CatEcommerce.Api/appsettings.Testing.json: implied by §10.2 "false in Testing".

  The BIL-001 precedent makes this MEDIUM. Add them to Amended (11) or a new item. Backlog row 43.
- [LOW] src/Api/CatEcommerce.Api/Modules/ReportingModule.cs:134 — **stale comment.** It says the resolver "is registered by the
  Notifications module", but line 142 registers it here, and that registration is correct (ruling 8). Also record that HO-NTF-001's deliver
  handler now depends on this registration. Backlog row 44.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Notifications/Guards/ScheduledReportGuard.cs:23, :32 — the guard rehydrates the whole
  run and the whole `StaffAccountAggregate`; it should project `Status`, `FileId`, `ScheduleId`, `ColumnsIncluded` and `RoleIds`. Both the
  guard (:26) and ReportRunAttachmentSource.cs:26 also read `files.stored_file` directly, with no HO-FIL-001 read port. Earlier FIL and EMR
  code does the same, so this is LOW, as for SRG's port rehydration. Backlog row 45.

Concurrence with the parallel gates (not repeated as rows):
- SECURITY HIGH (R3 clinical text has no NFR-SEC-8 access log) is a security finding, not a layering one. Its fix (a)
  (`RecordClinicalRecordAccessCommand` before a Clinical report runs) fits the established PHM/SRG query-then-access-log controller shape.
- SECURITY row 28 / CODE_REVIEW row 29 (recipient branch scope at send time) is the same issue raised twice. CODE_REVIEW row 28 is the
  one-hop merge issue (ruling 5). Both are agreed.
- **Backlog numbering collision.** SECURITY proposed rows 28-31 and CODE_REVIEW proposed rows 28-39 on this branch. They collide with each
  other. To stay clear of both, this record proposes rows 40-45. The integrator must renumber when appending.

FOR TEST (runtime, not observable here)
- C5's failure path. If the file is registered and the run save then throws something other than 23505, the runner's `catch` calls
  `FailAsync` in the **same scope**, with the same `runId`. Check that the tracked Generated run and its notification requests are not
  flushed by the Failed-run save. Also check that `RecordFailedReportRunCommandHandler`'s `GetByIdAsync(runId)` does not find the tracked
  entity and return early, and that the stored file is not orphaned.
- `rv_batch`: no duplicate rows when a batch has more than one `consignment_landed_cost_line` (ruling 4).
- I-A3: a write inside `IReportingReadScope` fails with 25006. I-A4: a timeout gives `400 ReportTimedOut`.
- `dotnet ef migrations has-pending-model-changes` is clean after the integrator regenerates the eight migrations in order.
- `ValidateOnBuild` succeeds with `INotificationAttachmentResolver` registered only by ReportingModule.

PROPOSED docs/backlog/RPT.md ROWS (numbered 40+ because of the 28-39 collision above; renumber on append)
| 40 | MEDIUM | Reporting command handlers / EfScheduledReportRunStore | ARCHITECTURE: every non-audit save passes `DoNotPublish`; the convention is `InMemoryPublishOnly`. A future event would be dropped silently. | ARCHITECTURE |
| 41 | MEDIUM | R7 `StockLowReorderSource` | ARCHITECTURE: reads `inventory.*` through IMP's internal `ReorderReads` and `db.Suppliers`, not through `report.rv_*` views. Either IMP publishes the read (or a view), or record both in §1 Amended. | ARCHITECTURE |
| 42 | MEDIUM | HO-NTF-001 §1 Amended (68)/(71); EfNotificationOutbox.cs | ARCHITECTURE: the coordinator appends a correction. `AttachmentId` is the run id, the guard checks roles (no active flag), and `EfNotificationOutbox.cs` was also changed. | ARCHITECTURE |
| 43 | MEDIUM | HO-RPT-001 §1 Amended | ARCHITECTURE: record the files outside §13: `ScheduledReportRunner.cs`, `IScheduledReportRunStore.cs`, `ReportParameterType.cs`, `appsettings.Testing.json`. | ARCHITECTURE |
| 44 | LOW | ReportingModule.cs:134 | ARCHITECTURE: the stale comment says the attachment resolver is registered by Notifications. Record that NTF's deliver handler depends on ReportingModule's registration. | ARCHITECTURE |
| 45 | LOW | ScheduledReportGuard / ReportRunAttachmentSource | ARCHITECTURE: the guard rehydrates the run and `StaffAccountAggregate`; project instead. Both read `files.stored_file` directly, with no FIL read port. | ARCHITECTURE |

SCOPE CHECK
- files in allowed scope: 197/201. Files counted in scope:
  - files inside §13's globs and lists;
  - the ADR-341 translations of §13's `Program.cs` and shared-constant entries:
    - `Modules/ReportingModule.cs` and `RateLimiting/ReportRateLimitPolicies.cs` (the per-area policy file, FIL/NTF precedent);
    - the `.Reports`/`.Reporting`/category partials of `Actions`, `ActionRoles`, `BusinessViolationCodes`, `SettingDefinitions`,
      `SettingKeys`, `NotificationTemplateCatalogue` and `ReportCatalogue`;
  - `Domain/DomainServices/ReportFilterValidator.cs`, specified by §12.6 and U-A4;
  - grouped validator and configuration files in §13's folders;
  - `EfNotificationOutbox.cs`, which carries out §11.7 item 1 (SRG/EMR precedent; MEDIUM to record);
  - `docs/handover/HO-RPT-001.md`, Amended row only (the established convention);
  - the HO-NTF-001, HO-ADM-001 and HO-ACC-001 Amended rows, written by the integrator under OQ-15/§5 in 01c3f77b (append-only, verified);
  - `docs/backlog/RPT.md`, under the owner's 2026-09-27 rule;
  - `docs/test-cases/TC-HO-RPT-001.md` and `.ai/decisions/adr.md` (ADR-277 to ADR-292, append only).
- files outside allowed scope: 4. None is in §14 forbidden scope; see MEDIUM row 43.
  - src/Api/CatEcommerce.Api/Background/ScheduledReportRunner.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/IScheduledReportRunStore.cs
  - src/Shared/CatEcommerce.Shared/Common/Enums/ReportParameterType.cs
  - src/Api/CatEcommerce.Api/appsettings.Testing.json
- Not touched: `web/**`, the SRS, the user stories, `.ai/**` other than `adr.md`, `.claude/**`, `NuGet.Config`, `global.json`, `*.sln`,
  `Program.cs`, any other `csproj` line, and any other area's aggregate, configuration, migration or handler beyond the §11.7 lines. No
  package was added.

---

## Combined re-check (architecture + security + code review) -- 2026-10-13

GATE: ARCHITECTURE (combined re-check of the gate fix round; also covers the SECURITY and CODE_REVIEW findings)
HANDOVER: HO-RPT-001
COMMITS: 126b21f3 (range 94ac1894..126b21f3, 45 files)
DATE: 2026-10-13
STATUS: FAIL

The fix round closes all three earlier HIGH findings. It **introduces one new HIGH**: the per-account `ChainedLimiter` defeats run-now's
"5 per 10 minutes per account" window. Owner gate policy (2026-09-27) applies: only BLOCKER/HIGH fail. A delta re-check of
`ChainedLimiter.IdleDuration` plus one test is enough to close it.

What this pass reviewed:
- The earlier ARCHITECTURE, SECURITY and CODE_REVIEW records for HO-RPT-001, and the owner decisions of 2026-10-13: R3 "Log access",
  Accountant "Yes, allow Sales", run-now "Add a per-user limit".
- `git diff 94ac1894..126b21f3`: every non-generated source and test file, the four view SQL files, the migration `Up`/`Down`, the
  model-snapshot delta (only three keyless `ToView` entries), `.ai/decisions/adr.md`, the HO-ACC-001, HO-NTF-001 and HO-RPT-001 §1 rows,
  `docs/backlog/RPT.md` and `docs/test-cases/TC-HO-RPT-001.md`.
- Context read: `ClinicalRecordKind`, `ClinicalRecordAccessFilter`, `SurgeryControllerBase`, `RecordClinicalRecordAccessCommandHandler`
  and its validator, IMP `ReorderReads`, `ResolvedCaller`, `SessionAuthenticationMiddleware`/`Program.cs` order (session middleware runs
  before `UseRateLimiter`), `AdminReportSchedulesController.RunNow` and `EfNotificationOutbox`.

Executed:
- On `catecommerce_rpt`, using the full connection string:
  - `--filter ReportingGateFixEndpointTests|StockReportBuilderTests|ReportingConventionTests`: 16/16 passed.
  - `--filter ReportingScheduleEndpointTests|ReportingStock|ReportingGrowth`: 22/22 passed.
- A scratch console app (`rpt-recheck-limiter`, outside the repo) ran the branch's `ChainedLimiter` code verbatim inside
  `PartitionedRateLimiter`, with run-now's chain. It made six acquisitions per round for one account, with a 12 s pause between rounds:
  - Results: **15 acquired, where the correct figure is 5.**
  - With the one-line fix below: 5 acquired.
- Not run: the full suite, `BillingTestGateDatabaseTests.XI_9`. That test fails before this round and also on master: an audit-timestamp
  false positive, backlog 38.

### 1. The earlier HIGH findings

**HIGH 1 (schedule read side rehydrates aggregates): CLOSED.**
- Every handler in `ReportScheduleQueryHandlers.cs` now runs `Select` before it materialises:
  - list: lines 64-73;
  - detail: lines 101-115;
  - runs: lines 150-165;
  - eligible: lines 186-205;
  - due: line 260;
  - plan: lines 273-283.
- The eligible-recipients read is bounded, with its filters in SQL:
  - it filters by the category action's roles, and by `CrossBranchRoles` for an all-branches schedule;
  - the branch-assignment filter is an `IQueryable` subquery, so it also runs in SQL;
  - `Take(200)` bounds it (`EligibleRecipientLimit`).
- `ScheduledReportGuard` also projects now (earlier LOW, row 49).

**HIGH 2 (C5 mutates two aggregates): CLOSED.**
- File step:
  - `ScheduledReportRunner.StoreFileAsync` does `IFileStore.PutAsync` and then dispatches `RegisterStoredFileCommand` as its own step.
    This is the `FilesController` shape.
  - `RetainUntil` is the branch-local date.
- C5 step:
  - `RecordScheduledReportRunCommand` no longer carries bytes. It takes `FileId`, `AttachmentOmitted`, `ContentLength` and
    `ContentSha256`, and the validator requires `FileId` unless `AttachmentOmitted` is set.
  - `RecordScheduledReportRunCommandHandler` no longer injects `IInMemoryDispatcher` or `IFileStore`. It creates only the run and enlists
    the requests in that run's single save (F23).
- Cleanup is a separate `DeleteStoredFileCommand` step (`DiscardFileAsync`) on each path that does not keep the run:
  - duplicate (`Created = false`);
  - refused (`!IsSuccess`);
  - thrown (`catch { Discard; throw; }`).
- `EfScheduledReportRunStore` clears the change tracker on a 23505 slot conflict and on any other failure, then rethrows. The Failed-run
  save that follows therefore cannot flush the run or its requests. This closes the earlier FOR-TEST item.
- The ADR-277/289 revision is a new heading appended at the end of `adr.md` (12 lines added, 0 removed, LF). It withdraws the
  "C5 stores the file" text and does not widen the rule.

**SECURITY HIGH (R3 clinical text has no access log): CLOSED.**
- Interactive reads: `StaffReportsController.TryLogClinicalAccessAsync` runs on view (line 65) and on CSV/PDF export (line 87).
  - It covers every report whose catalogue category is `Clinical`, dispatches `RecordClinicalRecordAccessCommand`, and fails closed to a
    bare 500 on a refusal or an exception. The rows are released only after the log is written.
  - On export, the clinical row comes before the C1 export audit.
  - This is the PHM/SRG query-then-access-log controller shape, so the added dispatch is not a finding.
- Scheduled runs: `ScheduledReportRunner.LogClinicalAccessAsync` writes one row per plan recipient, with the recipient as actor.
  - It runs after rendering and before anything is stored or enqueued.
  - Any failure records a Failed run with `ClinicalAccessLogFailed`.
- Tests: `R3_AViewAndBothExports_...`, `R3_IfTheAccessRowCannotBeWritten_...` and `R3_AScheduledRun_...` all pass.
- **Ruling on `ClinicalRecordKind.VisitList`: accurate enough for the audit trail.**
  - `VisitList` (6) is HO-EMR-001's existing kind for a read that spans many clinical records with no single record or cat. The visit
    list (`StaffVisitsController`) and the diagnostic list (`StaffDiagnosticsController`) already log under it.
  - R3 is exactly that: a register across many cats. The row still says who read, when, from where (IP on the interactive path) and what
    (`Path` = `/api/staff/reports/surgery-register...` or `scheduled-report/{key}/{scheduleId}`).
  - Adding a `SurgeryRegister` kind would change HO-EMR-001's enumeration, which is outside §13. Not adding one is the correct call, and
    it is recorded in §1 Amended (12).
  - Two refinements are LOW (row 54): `RecordId` is the report-key hash on the interactive path but the run id on the scheduled path, and a
    scheduled row is written even when the slot turns out to be a duplicate or the guard later cancels that recipient's email. Over-logging
    is the fail-safe direction.

### 2. The owner decisions

- **Accountant gets Sales: IMPLEMENTED and RECORDED.**
  - `ActionRoles.Reports.cs:16`: `SalesView` = ShopManager, Accountant, Administrator. The cost and margin columns still need
    `Reports.Financial.View`.
  - HO-ACC-001 §1 Amended (65) is append-only (the old cell is a byte-exact prefix) and matches the code.
  - Also recorded in ADR-281 (revision), RPT Amended (13) and backlog 8. I-G6 is re-proved with an Accountant recipient, and the test passes.
- **Per-user limit: IMPLEMENTED, BUT RUN-NOW'S WINDOW IS DEFEATED. This is the new HIGH (below).**
  - What is correct:
    - The partition key is the staff account. `SessionAuthenticationMiddleware` (inside `UseAccountsModule`) runs before
      `UseRateLimiter`, so `ResolvedCaller` is set when the limiter runs.
    - `reports` = a per-account `ConcurrencyLimiter` (`MaxConcurrentPerAccount`, default 2, `[Range(1,16)]`), then the single shared
      host-wide `RetryAfterConcurrencyLimiter`. That limiter is created once, is never owned by a partition and is never disposed.
    - `report-dashboard` = per account only, and is applied to `StaffDashboardController.Get`.
    - `report-run-now` = a per-account fixed window (5 per 10 minutes) plus one run in flight per account, then host-wide. This replaces
      "per IP", recorded in Amended (14) and ADR-290.
    - A rejection releases the leases already taken and carries `Retry-After`. `OneAccountsSlowReports_...` passes.
  - What is wrong: the window can be bypassed (HIGH below).
  - Also noted: the IP fallback in `AccountKey` is dead code. `ResolvedCaller.AccountId` is a non-nullable `Guid`, so `{ } id` always
    matches. Anonymous callers share the partition `acct:000...`, which is harmless because those endpoints refuse anonymous callers. LOW,
    folded into row 53.

### 3. The MEDIUM fixes

All of these are real, except the two caveats noted.
- **`CanReceive`:**
  - It is a pure Domain method.
  - The detail read and the guard both call it. The guard now projects the schedule's `BranchId` and the recipient's `BranchIds`.
  - At save, `ReportScheduleSupport.CheckRecipientsAsync` applies the same rule inline (`CanOpen`, then `CanUseAllBranches` for null,
    else the assignment) so that it can return distinct violation codes. It is semantically identical, so this is not a finding. The
    drift risk is LOW (row 55), together with the picker gap there.
- **Merge chain:**
  - `rv_customer_activity.v2.sql` uses `WITH RECURSIVE` with `depth < 20`, and `DISTINCT ON (start_id) ... ORDER BY depth DESC` picks
    the final survivor. Both the invoice and the credit-note branches join it.
  - R15's `CustomerOf` follows the chain in code, with a 20-hop cap.
  - `I_F7_AMergeChain_...` passes.
- **R7 views:**
  - `StockLowReorderSource` reads only `RvStockLocationRow`, `RvStockPositionRow`, `RvReorderPolicyRow`, `RvOnOrderRow`, `RvBatchRow`,
    `RvStockLedgerRow` and `RvBranchRow`.
  - `ReorderReads` and `db.Suppliers` are gone, and the `Read.QueryHandlers.Inventory` using is removed.
  - IMP's pure `StockUsability` and `ReorderSuggestionCalculator` are still called.
  - Caveat: the pair-selection rule and the on-order formula are now **copied** from IMP's `ReorderReads`. They are equivalent today (I
    compared them line by line), but that is a drift risk against U-C4: MEDIUM, row 56.
- **Boarding tile:** the figure is added only when a `Boarding` aggregate row exists, so it is absent rather than 0.
  `TheRevenueTile_HasNoBoardingFigure_...` passes.
- **Test DB guard:** `ReportingTestDatabase.Validate` refuses:
  - an unset or blank value;
  - a value that is not a connection string;
  - `catecommerce`, `catecommerce_dev` and `catecommerce_test*`.
  It runs in the factory constructor and in `RetireOtherSchedulesAsync`. The NRE is gone.
- **`InMemoryPublishOnly`:** used by all six schedule handlers, C6 and `EfScheduledReportRunStore`. C1 (the audit write) keeps
  `DoNotPublish`, which matches the precedent.
- **HO-NTF-001 rows (74)-(76):** append-only (the old cell is a byte-exact prefix) and accurate against the build:
  - the run id;
  - roles plus branch rights plus the file, with no active flag;
  - `EfNotificationOutbox.cs` and the `ReportingModule` resolver dependency.
- **Out-of-§13 files:** all four, plus `EfNotificationOutbox.cs`, are recorded in RPT §1 Amended (23). The HO-RPT-001 row is
  append-only. The stale `ReportingModule` comment is fixed (row 48).
- **Migration `AddReorderViewsAndCustomerMergeChain`:**
  - `Up` drops and recreates `report.rv_customer_activity` (v2) and creates `report.rv_stock_position`, `report.rv_reorder_policy` and
    `report.rv_on_order`.
  - `Down` reverses this and restores v1.
  - It contains no table DDL. The snapshot gains only the three keyless `ToView(..., "report")` entries.
  - `{{ISSUED}}` is substituted from `PurchaseOrderStatus.Issued.Value`.

### 4. New defects, and whether the backlog is honest

- **No superseded architecture** in the delta. None of these appears: `IDispatcher`, `IBusMessageDispatcher`, Mongo/Bson,
  `QueuePublishOnly`, `202`, `CommandServices`, a projection, `HasMany`, a second host, or a new package.
- **No registration change was needed.** C5 lost dependencies, and the runner (Scoped) gained `IFileStore` and `IBusinessSettings`, both
  already registered. `ValidateOnBuild` is exercised by the passing endpoint tests.
- **Backlog rows 28-52** are renumbered into one sequence from 28, and the RESOLVED notes match the code, with two exceptions:
  - row 17 and row 42 claim run-now is "5 per 10 minutes per account". That is not true at runtime (HIGH below). Unintended, but it must
    be corrected when the fix lands.
  - row 29 says `CanReceive` is applied "at save". It is the same rule inline (row 55).

  Rows 33 (partly), 43 (orphan on crash) and 49 (partly) state their open remainder honestly. Row 38 describes XI_9 accurately.

FINDINGS
- [HIGH] src/Api/CatEcommerce.Api/RateLimiting/ReportRateLimitPolicies.cs:90 — **`ChainedLimiter.IdleDuration` defeats run-now's fixed
  window, so the owner's "per-user limit" and §12.3's 5-per-10-minutes limit do not hold.**
  - The cause: `owned.Select(l => l.IdleDuration).Min()` ignores nulls, but a null means "in use".
    - On the run-now chain, `[FixedWindowRateLimiter, ConcurrencyLimiter(1)]`, the concurrency limiter goes idle as soon as a run ends.
      The fixed window, with 4 of 5 permits left, reports null. `Min` then returns the concurrency limiter's idle time.
    - `PartitionedRateLimiter`'s heartbeat disposes any partition idle for more than 10 s and creates a fresh one, which resets the window.
  - Effect: one account can run-now about every 10 s instead of 5 times in 10 minutes. Reproduced: 15 acquisitions in three rounds 12 s
    apart, against 5 expected.
  - Why it matters: manual runs are not subject to the slot index, so each run-now emails every recipient (up to 20) and stores a
    `ReportExport` file. Those files are not purged until HO-XCT-001 (row 43).
  - This is a regression. The previous per-IP `GetFixedWindowLimiter` held.
  - Severity: `ChainedLimiter` is a public, general-purpose type, so other areas will copy it.
  - Fix (verified in the scratch app: 5/5):

    ```csharp
    public override TimeSpan? IdleDuration =>
        owned.Count == 0 || owned.Any(l => l.IdleDuration is null) ? null : owned.Min(l => l.IdleDuration!.Value);
    ```

  - Add a test: one account calls run-now 5 times, waits more than 10 s, and the 6th call is 429. Then correct backlog rows 17 and 42 if
    their wording changes.
- [MEDIUM] src/Read/CatEcommerce.Read/QueryHandlers/Reporting/Sources/StockReportSources.cs:115-147;
  src/Infrastructure/.../ReportingViews/Sql/rv_on_order.v1.sql — **R7 now copies HO-IMP-001's reorder pair rule and on-order formula
  instead of calling them.**
  - Copied: non-quarantine active locations, positions plus policy pairs, the `defaultReorderPoint` fallback, the name fallback via
    batches, and `max(0, ordered - accepted)` in place of `PurchaseOrderProgress.ForLine`.
  - It is equivalent to `ReorderReads` today, and moving to views was what the earlier MEDIUM asked for. But if IMP changes its rule, R7
    will silently disagree with IMP's own suggestion list (U-C4, "called, not re-implemented").
  - Fix: have HO-IMP-001 own the rule as a pure function over the view rows, or add a parity test between R7 and IMP's suggestion list.
    Backlog row 56.
- [LOW] src/Api/CatEcommerce.Api/Controllers/StaffReportsController.cs:181; src/Api/CatEcommerce.Api/Background/ScheduledReportRunner.cs:229 —
  **the clinical-access row's `RecordId` differs by path.** It is the report-key hash on the interactive path and the run id on the
  scheduled path. Scheduled rows are also written for duplicate slots and for recipients the guard later cancels. The `VisitList` kind
  itself is ruled accurate enough (above). Backlog row 54.
- [LOW] src/Read/CatEcommerce.Read/QueryHandlers/Reporting/ReportScheduleQueryHandlers.cs:192-193;
  src/Application/.../Reporting/ReportScheduleSupport.cs:86-101 — **the eligible-recipients picker and the save check disagree.**
  - For an all-branches schedule, the picker needs one role that is both in the category's role set and cross-branch. The save rule
    (`CanReceive`) accepts the action and the cross-branch right from two different roles.
  - So a Vet+Accountant would be accepted for R3 all-branches but is not offered in the picker.
  - Also, the save check inlines `CanReceive` rather than calling it.
  - Backlog row 55.
- [LOW] src/Api/CatEcommerce.Api/Background/ScheduledReportRunner.cs (`catch { DiscardFileAsync; throw; }`) — **TEST: a throw from
  `EnqueueDeliveriesAsync` before `TrySaveAsync` is not cleared.**
  - The change tracker is cleared only inside `TrySaveAsync`. If a throw comes earlier, for example from the branch lookup or the link
    builder in the middle of the loop, the requests already enlisted stay tracked.
  - `DeleteStoredFileCommand`'s save would then flush them, followed by a `Failed` run with the same id.
  - The guard cancels them (the run is not `Generated`), so no email is sent, but the rows exist. A `ChangeTracker.Clear()` in the handler
    on failure, or doing the enlist inside the store's try block, would close it.
  - Folded into row 53's area, proposed as row 57.

PROPOSED docs/backlog/RPT.md ROWS (next free number 53)
| 53 | HIGH | ChainedLimiter.IdleDuration | ARCHITECTURE re-check: `Min()` ignores the null ("in use") of an owned limiter, so a run-now partition is disposed after 10 s idle and the 5-per-10-minutes window resets; reproduced 15/5. Return null if any owned limiter is in use; add a >10 s-idle test. (Also: `AccountKey`'s IP fallback is dead, AccountId is a non-null Guid.) | ARCHITECTURE |
| 54 | LOW | Clinical-access rows (R3) | `RecordId` is the report-key hash interactively but the run id when scheduled; scheduled rows are written for duplicate slots and for recipients the guard later cancels. `VisitList` ruled adequate; a dedicated kind needs an HO-EMR-001 amendment. | ARCHITECTURE |
| 55 | LOW | Eligible recipients / CheckRecipientsAsync | The picker requires one role that both holds the category action and is cross-branch (all-branches); `CanReceive` accepts the two from different roles. The save path inlines `CanReceive` instead of calling it. | ARCHITECTURE |
| 56 | MEDIUM | R7 StockLowReorderSource / rv_on_order | IMP's pair rule and on-order formula are copied, not called (U-C4). Have IMP own a pure function over the view rows, or add an R7-vs-IMP parity test. | ARCHITECTURE |
| 57 | LOW | ScheduledReportRunner / RecordScheduledReportRunCommandHandler | A throw in `EnqueueDeliveriesAsync` before `TrySaveAsync` leaves enlisted requests tracked; the later `DeleteStoredFileCommand`/Failed-run save flushes them (cancelled by the guard, but persisted). Clear on failure. TEST to confirm. | ARCHITECTURE |

SCOPE CHECK
- files in allowed scope: 45/45.
  - Every changed `src/**` and `tests/**` file is a Reporting file created on this branch, except two:
    `Modules/ReportingModule.cs` and the model snapshot, both counted in scope by the first pass.
  - The docs changes are scoped as follows:
    - the three handover §1 cells are append-only;
    - `docs/backlog/RPT.md`;
    - `docs/test-cases/TC-HO-RPT-001.md`;
    - `.ai/decisions/adr.md` is append-only.
- files outside allowed scope: none. No §14 file was touched, `web/**` and the SRS were not touched, and no package was added.

---

## Re-check 2 (follow-up to the combined re-check) -- 2026-10-13

GATE: ARCHITECTURE (combined re-check 2: architecture, security and code review)
HANDOVER: HO-RPT-001
COMMITS: 55204632 (range 81880105..55204632, 12 files)
DATE: 2026-10-13
STATUS: PASS

Executed on `catecommerce_rpt`, using the full connection string:
- `--filter ReportLimiterTests|ReportingGateFixEndpointTests|ReportingStockEndpointTests|ReportingScheduleEndpointTests`: 34/34 passed.
- `tests/CatEcommerce.Domain.Tests` builds. `EfScheduledReportRunStore` is the only implementation of `IScheduledReportRunStore`.
- Not run: the full suite.

Verified:
- **Row 53 (HIGH): CLOSED.**
  - `ChainedLimiter.IdleDuration` now returns null whenever any owned limiter is in use. This is exactly the fix verified earlier in the
    scratch app.
  - `ReportLimiterTests.TheFifthRunNowOfTheWindow_...` builds the run-now chain inside `PartitionedRateLimiter` and takes 5 leases. After a
    23 s idle, the 6th is refused with `Retry-After`, and another account is unaffected.
  - The old `Min()` code would fail this test, because the partition is disposed after 10 s. The test therefore tells the two versions
    apart.
  - Backlog rows 17 and 42 are corrected by appending to the cell (each old cell is a byte-exact prefix).
- **Row 56 (MEDIUM): ADDRESSED by a parity pin.**
  - `I_C3_R7ListsTheSamePairsAndSuggestionsAsHO_IMP_001sOwnList_...` compares R7 with IMP's own `/reorder-suggestions`. It checks the
    pairs, suggested quantity and lead time, and covers on-order, policy-only pairs and quarantine.
  - Any IMP rule change now fails the test instead of drifting silently. This is acceptable inside §13: the alternative, a published IMP
    function, is IMP scope.
- **Row 54 (LOW): RESOLVED.** The scheduled clinical-access `RecordId` is now `EntityIdFor(reportKey)`, the same as the interactive path.
  The run id moved into `Path`. Over-logging on duplicate or cancelled deliveries stays, which is the fail-safe direction and is recorded
  as partly resolved.
- **Row 55 (LOW): RESOLVED.**
  - `CheckRecipientsAsync` now decides with `ReportAccessPolicy.CanReceive`; the branches that follow only choose the message.
  - The picker applies the two conditions separately: a role holding the category action, and, for all-branches, any cross-branch role.
    That matches `CanReceive`.
- **Row 57 (LOW): RESOLVED.**
  - C5 wraps `EnqueueDeliveriesAsync` and calls `IScheduledReportRunStore.ClearPending()` (`ChangeTracker.Clear`) before rethrowing.
  - The new port member is in Domain/DomainServices, alongside the existing contract, and does not add an EF reference to Domain.
  - It is covered by a new `ReportingGateFixEndpointTests` case.
- **No new defects** in the delta:
  - no superseded-architecture types, no new registration and no package;
  - every file is a Reporting file already in scope;
  - backlog rows 53-57 are appended and honest.

FINDINGS
- none (no BLOCKER, HIGH, MEDIUM or LOW).

SCOPE CHECK
- files in allowed scope: 12/12
- files outside allowed scope: none
