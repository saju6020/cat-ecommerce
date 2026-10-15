GATE: ARCHITECTURE
HANDOVER: HO-EMR-001
COMMITS: b9c80c5 47612a9 baab800 9d9e2d4 d786ba4 9696dbf 5e4cf63 0805bf1 650556a e1c5e10 3b39c31 75d8c46 (branch ho/EMR-001, range 50f1ab1..75d8c46, 192 files)
DATE: 2026-10-07
STATUS: FAIL

First pass of the ARCHITECTURE gate. Owner gate policy (2026-09-27): only BLOCKER and HIGH findings
fail the gate. MEDIUM and LOW findings are listed for the integrator to file in docs/backlog/EMR.md;
this gate does not edit the backlog. Phase F (treatments and stock, US-CLN-15) is deferred by owner
decision and is not failed for its absence.

What this pass reviewed:
- `git diff 50f1ab1..75d8c46`: every non-generated source file, the six migrations' hand-written SQL,
  and the model snapshot by entity/table set.
- HO-EMR-001 in full (§1–§20).
- docs/handover/CONTRACTS.md, including the cross-cutting duties C-ALL-1…5.
- `.ai/`: architecture, cqrs, bounded-contexts, namespace-conventions, di-registration-guide,
  command-handler-pattern, read-side-pattern, persistence-ef-core, domain-events-and-outbox,
  enumeration-pattern and host-composition.
- In `.ai/decisions/adr.md`: ADR-341 and the new ADR-117…134.
- HO-FIL-001's eraser and purge code (`StoredFileEraser`, `StoredFilePurgeSource`,
  `FileStorageModule`), for deviation 3.
- Earlier gates' rulings in docs/gates/, to keep severities consistent.

Nothing was built or run. Every runtime claim is marked for TEST.

VERIFIED (no finding)
- **ADR-117…134** are appended word-for-word. A script compared each Decision and Reason line
  against the §9 table: 18 of 18 match. The ADR-125 "Narrowed 2026-09-23" note matches §1 Amended
  (C-5). Nothing earlier in adr.md was changed (one `-` line in the diff, the hunk header only), and
  the committed file uses LF line endings.
- **No superseded architecture.** None of these appear:
  - `IDispatcher`, `IBusMessageDispatcher` or `IEventPublisher`;
  - MassTransit, RabbitMQ, Mongo/Bson or `QueuePublishOnly`;
  - `202`, a rejection event, `CommandServices`, `ErrorEvents` or `Read.EventHandlers`;
  - `IReadRepository`, `[Authorize]`, `IFormFile`, `HasMany`, or a second host.

  Every `SaveAsync`/`UpdateAsync` passes `InMemoryPublishOnly`. The two exceptions are the
  access-log and audit rows, which pass `DoNotPublish` as §11.4 and §11.6 specify.
- **Layers.**
  - Domain references no EF Core, Application, Read or Api type.
  - Application does not reference Read, and Read does not reference Application.
  - Every namespace matches `.ai/namespace-conventions.md`, with `Visit` as the feature segment
    (ADR-117).
- **Aggregates.**
  - Every mutation runs inside an aggregate method and ends with `UpdateVersion()`.
  - `EnsureOpen()` is the first statement of every `VisitAggregate` mutation except `Open`.
  - Roots reference each other by id. Owned children use `OwnsMany`, including the nested result
    values (V2).
  - Every root maps `xmin`. Instants are `timestamptz` and enumerations are stored by `Value`.
  - Invariants live in the aggregates. Finalise re-checks the `Vet` role from the caller's own
    `StaffAccountAggregate`.
  - Drafts, addenda, diagnostic orders and consents are separate roots, as §8.1 specifies.
- **Rule-1 exceptions.** Vitals with a weight are companion-committed with
  `CatWeightRecordAggregate` (ADR-121), and the F23 comment is present. No other command mutates two
  aggregates. Outbox enlistment in Finalise and SetFollowUp follows the established HO-NTF-001
  pattern.
- **Messages.**
  - Both messages are enqueued in command handlers, before the save.
  - No event handler enqueues anything or mutates a clinical aggregate. The four audit handlers
    write `BusinessEvent` rows through `SaveAsync(DoNotPublish)`, as §11.6 specifies.
  - Event payloads carry ids, enumerations, numbers and instants only.
- **Host.**
  - Every handler (25) and validator (25, all as `IValidator<T>`), every query handler, event
    handler, section provider, the follow-up guard, the retention source and the PDF renderer is
    registered explicitly in `ConsultationModule` (ADR-341), all Scoped.
  - `Program.cs` is unchanged. It already calls `AddConsultationModule`, and `ValidateScopes` and
    `ValidateOnBuild` are both on.
  - Nothing that `AddInMemoryBusServices()` provides is hand-registered.
  - `ClinicalRecordFrozenFilter` is registered globally and `ClinicalRecordAccessFilter` per action
    through a TypeFilter, as §12.5 and §12.7 specify.
- **ADR-133 edits** to HO-APT-001's Complete and Cancel handlers are the guard lines, their usings
  and one constructor parameter each. Nothing else in those files changed.
- **Phase F contracts are left as specified.**
  - No HO-IMP-001 or HO-SHP-001 file is touched. `IStockLedger`, `StockReferenceKind` and the stock
    DTOs are unchanged.
  - The contracts other areas consume are intact and match the handover:
    - `VisitFinalisedEvent`, the HO-BIL-001 contract, has exactly the §11.5 fields.
    - `IClinicalHistorySectionProvider`, `IDischargeSummaryContributor`, `ConsentRules.IsValidFor`
      and `GetConsentStatusQuery` match §8.7–§8.9.
    - `ClinicalHistorySections.All` declares the three deferred sections with their owners.
  - Only the Phase F types (TreatmentLine, `GetVisitChargeLinesQuery`, the dispensary reads,
    `ClinicalStockShortfallRecordedEvent`) are absent, and the module comment says so.
- **Cross-cutting duties C-ALL-1…5.**
  - C-ALL-1: `RecordClassification.Consultation.cs` classifies all 15 new tables. The care tables are
    Clinical/Forbidden, the consent is Regulatory and the three catalogues are Operational.
  - C-ALL-2: an `ErasureExemptions.Consultation.cs` exemption with a reason.
  - C-ALL-3: `ConsultationRetentionReviewSource` is a flag-only count, registered.
  - C-ALL-4: no aggregate method removes a record or an owned child. Problems are withdrawn
    (deviation 2).
  - C-ALL-5: `ClinicalAuditEntityTypes.Consultation.cs` registers every audit `EntityType` written:
    - the four CLR root names the interceptor and `BusinessEvent` handlers use;
    - the six `ClinicalRecordKind` names the access log uses.

    The draft is excluded from capture and the catalogues are not Clinical.
- **Model snapshot.** The entity set grew from 44 to 52 and no entity or table was removed. The large
  `-` count is EF reordering. Pending model changes are for TEST.
- **Reported deviations, judged against §13, §14 and `.ai/`:**
  - (1) `FilePurposeAccess.cs` is HO-FIL-001 OQ-7's extension point ("named by the consuming
    handover"); HO-SHP-001's ProductImage set the precedent. The other edits in this deviation are
    under `tests/**`, which §13 allows. See the scope MEDIUM.
  - (2) Stricter than §8.4 and consistent with ADR-118 and C-ALL-4. See the MEDIUM on problem
    growth.
  - (3) See the MEDIUM below.
  - (4) Consistent with the APT backlog #8 ruling. See the MEDIUM below.
  - (5) Acceptable.
  - (6) Acceptable. See the LOW below.
  - (7) Acceptable. An attribute argument cannot be an `Enumeration`, and an unknown name throws.
  - (9) A contract-level LOW, below.
  - (10) Acceptable. Two catalogue mappings were added beyond §12.5's list.
  - (11) Acceptable. Mappers are not mandatory. See the LOW below.
  - (12) Correct: §14 forbids `.ai/**` except adr.md. See the LOW below.

FINDINGS
- [BLOCKER] src/Api/CatEcommerce.Api/Controllers/StaffVisitsController.cs:145-154 — **a controller
  takes hold of the `DbContext` and dispatches the same command twice.**
  - On the draft first-write race, `SaveDraft` catches
    `DbUpdateException`/`PostgresException(ix_consultation_draft_visit_author)` and resolves
    `CatEcommerceDbContext` from `HttpContext.RequestServices` to call `ChangeTracker.Clear()`. It
    then calls `dispatcher.SendAsync` a second time.
  - Two checklist rules are broken:
    - "A controller holding a DbContext, a repository or an aggregate" is a BLOCKER. Managing the
      unit of work from the Api layer breaks the boundary that `.ai/architecture.md` §2 and
      `.ai/cqrs.md` §5 rule 1 draw.
    - "A controller dispatching two commands" is a HIGH.
  - HO-EMR-001 §12.5 puts this exact mapping ("`consultation_draft` unique → retry once as an
    overwrite") in `UniqueConstraintExceptionFilter`. The same branch already answers the
    `ix_visit_appointment` race there with one re-dispatch.
  - The CODE_REVIEW gate raised the same lines at HIGH.
- [HIGH] The read side rehydrates aggregates. This breaks `.ai/read-side-pattern.md` rule 1
  ("Project, never rehydrate") and rule 2 ("`Select` before `ToListAsync`"), and ADR-012.
  - Each site below loads whole `VisitAggregate` / `DiagnosticOrderAggregate` /
    `ConsentRecordAggregate` graphs, with their owned collections, through
    `AsNoTracking()[.AsSplitQuery()].FirstOrDefaultAsync/ToListAsync`, then maps in memory:
    - src/Read/CatEcommerce.Read/QueryHandlers/Visit/VisitQueryHandlers.cs:45 (visit detail) and
      :69 (every order of the visit, with all result sets and values, to compute two counts);
    - DiagnosticQueryHandlers.cs:22;
    - DischargeSummaryQueryHandlers.cs:121, :134, :153 (up to 200 full visits for a list) and :184
      (up to 200 full orders);
    - ConsentQueryHandlers.cs:41, :97 and :114;
    - src/Read/CatEcommerce.Read/ClinicalHistory/EmrSectionProviders.cs:59 (Visits page) and :138
      (LabResults page).
  - The history providers are on the NFR-PRF-4 hot path (ADR-128).
  - This is the same fault the CAT-001, NTF-001 and SHP-001 ARCHITECTURE gates each raised at HIGH
    and had fixed with projections.
  - Nothing is written back, and every read is `AsNoTracking`, so this is a pattern violation, not a
    data risk.
- [MEDIUM] Deviation 3: a delete guard on `files.stored_file` instead of an append-only trigger.
  Defined at src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/20261007082117_AddDiagnostics.cs:234-253
  and replaced in 20261007084006_AddConsents.cs:112-127.
  - **The narrowing is justified.** The table and its lifecycle belong to HO-FIL-001 since C-5 and
    ADR-125's narrowing, and FIL legitimately redacts and purges unreferenced rows.
  - **But the guard does not protect the content.**
    - HO-FIL-001's only erase path, `StoredFileEraser.EraseAsync`
      (Infrastructure/Files/StoredFileEraser.cs:32-45), deletes the stored **object first** and the
      row second.
    - `FileStorageModule.cs:93-101` registers a `StoredFilePurgeSource` for **every** `FilePurpose`,
      including `LabResult`, `ClinicalPhoto` and `ConsentForm`.
    - So a purge of a referenced clinical file would destroy the object, then hit CR001 on the row,
      leaving a clinical record that points at a missing file. That is contrary to ADR-118, §4.2 ("no
      delete path exists for any … attached file") and §4.4.
  - **It is latent today:** nothing invokes the purge sources until HO-XCT-001's worker lands, and
    no HTTP route dispatches `DeleteStoredFileCommand`.
  - **ADR-118's text still says "stored files are append-only by trigger"** and has no amendment
    recording this deviation. `ErasureExemptions.Consultation.cs:8` ("protected from deletion by the
    database while they are referenced") overstates the effect.
  - **Must be closed before HO-XCT-001 wires `IRetentionPurgeSource`.** The integrator or owner must
    decide which area closes it. For SECURITY.
- [MEDIUM] Deviation 2's side effect: the `visit_problem` collection is no longer bounded.
  - src/Domain/CatEcommerce.Domain/Aggregates/Visit/VisitAggregate.cs:188 caps only the problems in
    the request (≤ 20).
  - Withdrawn rows are kept, and `ConsultationNoteRequests.cs:17` makes `ProblemId` optional. The
    controller mints a fresh id when it is absent (StaffVisitsController.cs:172).
  - So every explicit save that omits ids withdraws the old problems and inserts new ones. The owned
    collection grows with each save, past §8.1's "≤ 20" bound, which the boundary design relies on
    (`.ai/bounded-contexts.md` rule 3).
  - It is bounded in practice by one consultation's saves, so this is not HIGH. Record it with the
    deviation in §1 Amended.
- [MEDIUM] Deviation 4: another branch's record returns `404` (ClinicalResponses.cs:21-35, every
  handler's `BranchId` check) instead of §11.4's `403 VisitBelongsToAnotherBranch`.
  - It is consistent with the APT backlog #8 ARCHITECTURE/SECURITY ruling, and it does not reveal
    whether the id exists.
  - But it changes the §12.4 contract that HO-EMR-002 consumes. `VisitBelongsToAnotherBranch` is now
    declared and never returned (BusinessViolationCodes.Consultation.cs:11, ClinicalMessages.cs:16).
  - Record it in §1 Amended and tell HO-EMR-002.
- [MEDIUM] Five list queries are capped, not paged (SRS UI-4, read-side rule 4):
  - VisitQueryHandlers.cs:175 (open visits, 500);
  - DiagnosticQueryHandlers.cs:71 (pending diagnostics, 500);
  - ConsentQueryHandlers.cs:76 (a cat's consents, 200);
  - DischargeSummaryQueryHandlers.cs:156 (portal visits, 200) and :187 (portal lab results, 200).

  §12.4 does not specify paging for these routes, so this is a MEDIUM, not a HIGH.
- [MEDIUM] Scope: 7 files fall outside §13's literal list. None touches §14 forbidden scope. Each
  needs a §1 Amended record, per the established practice (NTF-001 and SHP-001 gates).
  - **The handover's controller list names no helper:**
    `src/Api/CatEcommerce.Api/Controllers/ClinicalResponses.cs`.
  - **A configuration helper:**
    `src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/ClinicalConfigurationExtensions.cs`.
    §13 lists one file per root.
  - **Not in §13's DomainServices list:**
    `src/Domain/CatEcommerce.Domain/DomainServices/FollowUpSchedule.cs`.
  - **HO-FIL-001's file**, an OQ-7 extension point (deviation 1):
    `src/Shared/CatEcommerce.Shared/Common/Constants/FilePurposeAccess.cs`.
  - **`tests/CatEcommerce.Infrastructure.Tests/**`**, which §13 does not list:
    - `Auditing/AuditCaptureExclusionsTests.cs`;
    - `Documents/QuestPdfDischargeSummaryRendererTests.cs`;
    - `Notifications/Guards/VisitFollowUpGuardTests.cs`.
  - The edits to other areas' test files are allowed by §13's `tests/**` globs: unsealing HO-APT's
    `AppointmentsApiFactory`, the two HO-APT handler-test constructor updates, and the HO-FIL test
    updates. §14 does not list FIL. Record them too, because §14 reserves HO-APT-001's files.
- [LOW] .ai/decisions/adr.md:3113-3116 — a doubled `---` separator before the HO-EMR-001 header.
  The same cosmetic fault was fixed for APT (APT backlog row 15).
- [LOW] src/Domain/CatEcommerce.Domain/Aggregates/Visit/VisitAggregate.cs:23 — the doc comment names
  the triggers' migration `AddMedicalRecordsModule`. The migration is
  `AddClinicalRecordImmutability`.
- [LOW] src/Domain/CatEcommerce.Domain/DomainServices/FollowUpSchedule.cs:15-30 (deviation 6) —
  rebuilds the zone-to-instant conversion with `TimeZoneInfo` directly. §11.4 C18 says "through
  HO-ADM-001's calendar helpers", and `BranchCalendar` / `AppointmentSlotFinder.InstantOf` already
  exist (the guard uses them). Returning UTC is fine.
- [LOW] Deviation 11: there is no `DiagnosticMapper` or `ConsentMapper`. Twelve call sites across the
  Visit handlers use HO-APT-001's `AppointmentMapper.ParseEnum`. That couples this area's handlers to
  another area's mapper for a generic helper.
- [LOW] src/Read/CatEcommerce.Read/QueryHandlers/Visit/VisitQueryHandlers.cs:66 (deviation 9) —
  `thumbnailUrl` is the full file's signed URL. This is a §12.4 contract nuance for HO-EMR-002: the
  client will download full-size photos for thumbnails.
- [LOW] HO-ACC-001's change-capture interceptor
  (src/Infrastructure/CatEcommerce.Infrastructure/Auditing/AuditChangeCaptureInterceptor.cs:145-148)
  attaches only owned entries whose principal is the root.
  - So `diagnostic_result_value` rows, owned by the result set rather than the order, are likely
    missing from the `DataChange` snapshot for `DiagnosticOrderAggregate`.
  - Not this slice's code, but it affects AC-20 for the first nested-owned clinical model. For TEST
    to confirm (I-C15 does not cover diagnostics).
- [LOW] Handover §6 says `.ai/bounded-contexts.md` "Rule 1: two recorded exceptions (ADR-121,
  ADR-126)". The doc records none. Deviation 12 correctly left `.ai/**` alone (§14), so this stays an
  OQ-1 action for the architect, not a code finding.

FOR TEST (runtime, not observable here)
- I-C7 / I-C9 (child-row triggers and the finalise save):
  - the finalise save must not touch child rows;
  - the vitals/weight companion commit must roll back as one transaction (I-B6);
  - `CR001` must surface through `ClinicalRecordFrozenFilter`.
- `dotnet ef migrations has-pending-model-changes` reports no changes.
- The nested-owned audit snapshot (LOW above).
- Once the draft retry moves, re-run I-C2 and the draft race.

SCOPE CHECK
- files in allowed scope: 185/192. Files counted in scope:
  - files inside §13;
  - ADR-341 per-area translations: `Modules/ConsultationModule.cs`, `CatEcommerceDbContext.Consultation.cs`,
    and the `Actions`, `ActionRoles`, `BusinessViolationCodes`, `SettingDefinitions` and `SettingKeys`
    `.Consultation.cs` partials;
  - CONTRACTS C-ALL files: `RecordClassification`, `ErasureExemptions` and `ClinicalAuditEntityTypes`
    `.Consultation.cs`, and `Retention/ConsultationRetentionReviewSource.cs`;
  - the ADR-133 guard edits, in the APT files that actually hold the Complete and Cancel handlers;
  - grouped validator and configuration files in the §13 folders.
- files outside allowed scope (7, none in §14 forbidden scope — MEDIUM above):
  - src/Api/CatEcommerce.Api/Controllers/ClinicalResponses.cs
  - src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/ClinicalConfigurationExtensions.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/FollowUpSchedule.cs
  - src/Shared/CatEcommerce.Shared/Common/Constants/FilePurposeAccess.cs
  - tests/CatEcommerce.Infrastructure.Tests/Auditing/AuditCaptureExclusionsTests.cs
  - tests/CatEcommerce.Infrastructure.Tests/Documents/QuestPdfDischargeSummaryRendererTests.cs
  - tests/CatEcommerce.Infrastructure.Tests/Notifications/Guards/VisitFollowUpGuardTests.cs
- None of these was touched:
  - `web/**`, `docs/handover/**`, `docs/SRS.md` or `docs/USER_STORIES.md`;
  - `.ai/**` other than adr.md, or `.claude/**`;
  - any `*.csproj`, `*.sln`, `NuGet.Config` or `global.json`;
  - any HO-IMP-001 or HO-SHP-001 file, `CatAggregate` or `CatWeightRecordAggregate`.

  No package was added.
