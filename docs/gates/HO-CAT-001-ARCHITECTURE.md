GATE: ARCHITECTURE
HANDOVER: HO-CAT-001
COMMITS: master(5200d75)...ho/CAT-001-gates(67f5865) — 177232d 8519b06 ccc3aa2 103fa7b 832bae4 5e26340 d4eea55 00967ce cb2ddd2 c529f8f ac79f82 1294ffe af20041 67f5865
DATE: 2026-09-26
STATUS: PASS

## History
- **First pass** (7abc9e4, below): FAIL. 0 BLOCKER, 2 HIGH, 4 MEDIUM, 5 LOW.
- **Second pass** (this section, 67f5865): PASS. 0 BLOCKER, 0 HIGH, 1 MEDIUM, 4 LOW.

## What I reviewed
- The branch's own changes: `git diff master...ho/CAT-001-gates` (76 files). I read every
  changed backend file. `master` is 5200d75, which merged HO-ADM-001 phase A (ADR-043
  `[RequireAction]`/`OwnerScope`). I accept that as master code and did not re-review it.
- HO-CAT-001 §1: both new 2026-09-26 Amended rows; §13; §14. HO-CAT-002 §13, §14 and its new
  Amended row.
- `.ai/`: `bounded-contexts.md`, `command-handler-pattern.md` §4–6, `architecture.md`
  (handler/Domain Service rules), plus everything the first pass read. The branch changes nothing
  under `.ai/`.
- The owner decisions I was told to accept, and did not re-judge:
  - `pageSize` is clamped at 100, and the lists return `PagedResult<T>` with a total.
  - A11/C10 returns `{treatmentId, nextDueOn}`.
  - Audit records personal data in full.
  - The QuestPDF Community licence applies.
  - 4007225's 27 extra files are the owner's own.
  - The photo `StorageKey` trust issue is carried to HO-FIL-001.

## Commands run (read-only)
- `git archive 67f5865` into a new scratch folder. It had no git-ignored appsettings, only
  `appsettings.Development.json.example`.
  - `dotnet build CatEcommerce.sln`: **0 warnings, 0 errors**.
  - `dotnet dotnet-ef migrations has-pending-model-changes`: **"No changes have been made to the
    model since the last migration."**
  - `migrations list --no-connect`: `WidenAuditEntryValues` (20260926135821) is last. It follows
    `AddAdministration` (20260925185529).
- `git merge-tree --write-tree master ho/CAT-001-gates`: clean (tree f713349).
- `git merge-tree --write-tree ho/integrator-host ho/CAT-001-gates`: clean (tree f2902f0).
  - `master` is an ancestor of `ho/integrator-host` (0421d4f), and `ho/integrator-host` adds no
    migration.
  - I archived that merged tree and built it: 0 warnings, 0 errors, and
    `has-pending-model-changes` is clean. `ho/integrator-host`'s move of `ContainsControlCharacter`
    into the base `BusinessViolationCodes.cs` does not collide with this branch's
    `InvalidPageSize`/`InvalidEnumValue`/`StaffAccountNotVeterinarian`.
- No tests run. Every runtime claim is for TEST.

## First-pass findings: status now
- **HIGH, Q1 rehydration (`GetCatProfileQueryHandler`): CLOSED.** The handler now does
  `AsNoTracking().Where(...).Select(new {...}).FirstOrDefaultAsync()`:
  - scalars, `Enumeration` members and nested `Select`s over the four owned collections Q1 shows;
  - `DisplayName` resolved after materialisation, the same two-phase shape as `GetCatsQueryHandler`;
  - vaccinations and parasite treatments are no longer loaded, and `CatAggregate` is never
    constructed.
  - Regression test: `CatProfileProjectionEndpointTests.cs`.
- **HIGH, Q5 rehydration (`GetCatMedicalHistoryQueryHandler`): CLOSED.** Same projection, and the
  photo collection is not loaded. The weight query was already a projection.
- **MEDIUM, QuestPDF §20.6 stop: CLOSED.** Owner decision (a). The comments in
  `CatProfilesModule.cs:89-91` and in the renderer are updated.
- **MEDIUM, `Application.Tests.csproj` → `Read` ProjectReference: CLOSED by record.** The Amended
  row names it. The production boundary is unchanged: Application → Domain and Shared only.
- **MEDIUM, 27 non-implementation files in 4007225: CLOSED.** Owner decision (b).
- **MEDIUM, I19 had no paging headers or total: CLOSED.**
  - Q2, Q3 and Q6 return `PagedResult<T>`, reusing `Read/ViewModels/PagedResult.cs` rather than
    adding a second type. Each counts the filtered set, then does `Skip`/`Take` and a `Select`
    projection.
  - The module's query-handler registrations are updated to the new result type
    (`CatProfilesModule.cs:77-78,81`).
  - `pageSize <= 0` returns the standard `ValidationResponse` 400 (`InvalidPageSize`). A larger
    `pageSize` is clamped to `PagingRules.MaxPageSize`.
- **LOW, stale `RecordClassification.CatProfiles.cs` comment: CLOSED.**
- **LOW, HO-ACC-001 appends missing from HO-CAT-001 §1: CLOSED.** The Amended row lists them.
- **LOW, `TryParseEnum`'s bespoke `{ error }` 400: CLOSED.** It now returns a `ValidationResponse`
  with `InvalidEnumValue`.
- **LOW, CorrelationId is `Guid.NewGuid()`, not the trace id: CARRIED.** It is a handover defect
  (§12.4 names a helper that does not exist) and is consistent with every controller. Unchanged.
- **LOW, §12.4 "inject nothing else" and §12.2 "ports take a ViewModel" wording: CARRIED.** Both are
  handover defects that the code deviates from correctly. Not amended.
- **Integrator carry items: STILL CARRIED, unchanged, `.ai/**` is §14.**
  - `.ai/persistence-ef-core.md` §2 lacks ADR-021's two corrections.
  - `.ai/enumeration-pattern.md:7` names the wrong package.
  - `Microsoft.AspNetCore.OpenApi` is unused (baseline).

## Merge resolution (ac79f82): ADR-043 authorization intact
- All 18 `CatsController` actions and both `VaccinesController` actions carry the same
  `[RequireAction(...)]`, with the same `OwnerScope` arguments, as `master`. I compared them
  attribute by attribute against `git show master:`. No action lost or changed its attribute.
  `CurrentCaller()` owner-forcing is retained.
- The branch's own additions sit alongside it: `ApplyPaging`'s clamp and 400,
  `InvalidPageSizeValidationResponse`, `TryParseEnum`'s `ValidationResponse`, and the PDF
  `DocumentDrawingException` catch.
- **PagingRules reuse: yes, for `MaxPageSize`.** No local `MaxPageSize = 100` constant is left in
  either controller. Both use `Shared.Common.Constants.PagingRules.MaxPageSize`. `MaxPageNumber`
  is not applied (MEDIUM 1 below).

## New code this pass: architecture checks
- **Cross-aggregate lookups in C9/C10.** `RecordCatVaccinationCommandHandler` and
  `RecordCatParasiteTreatmentCommandHandler` now also inject
  `IAggregateRootRepository<StaffAccountAggregate>` and load the staff account **read-only**:
  - A null result returns `StaffAccountNotFound`. A vet without `StaffRoles.Vet` returns
    `StaffAccountNotVeterinarian`. Both are returned `CommandResponse` violations, not throws.
  - This is the existence/attribute-check-in-the-handler shape that
    `.ai/command-handler-pattern.md` §6 ("Cat must exist → handler, repository load") and
    `.ai/bounded-contexts.md`'s BR-1 row allow. It is the same shape as the existing
    `VaccineAggregate` lookup.
  - One aggregate is still mutated per command: `CatAggregate`. `StaffAccountAggregate` is never
    saved.
  - The open-generic registration already covers the repository, so no DI change was needed.
  - No finding.
- **`TimeProvider` seam.** Every Cat handler that needs "today" injects `TimeProvider` and threads
  `Today`/`uploadedOn` through the Domain DTO.
  - `CatAggregate` and `CatWeightRecordAggregate` no longer read `DateTime.UtcNow`, and the future-
    date invariants stay **in the aggregate**.
  - The Domain DTOs gain a `DateOnly` only: no EF, Application or Read type.
  - `TimeProvider` is host-registered (HO-ACC-001), and ValidateOnBuild passes in the build above.
    That it resolves at runtime is for TEST.
- **Validators.** `Reason.MaximumLength(1000)` is added to three `IValidator<T>`s that are already
  registered. No registration changed.
- **Persistence.** The only model change is `AuditEntryConfiguration.BeforeValue`/`AfterValue` →
  `text`.
  - The migration is two `AlterColumn` calls in `Up`, with the exact reverse in `Down`, and the
    snapshot matches.
  - The file is Accounts-owned, but the edit is integrator-authorised (Amended row; the caller
    confirmed it), the same precedent HO-ACC-001's gate accepted for integrator-authorised edits.
  - `timestamptz`, `OwnsMany` and `xmin` are untouched.
- **Infrastructure csproj.** Adds `EmbeddedResource` (font) and `None` (licence text), with no
  package change. It is recorded in the Amended row as outside §13(a)/(b). Accepted on that
  record.
- **Absent, as required.** None of these appears in the delta: `IDispatcher`, `IEventPublisher`,
  `IBusMessageDispatcher`, a broker, Mongo/Bson, `202`, a rejection event, `CommandServices`,
  `QueuePublishOnly`, a bus hand-registration, `Program.cs`, a `PackageReference`, `.sln` or
  appsettings. There is no in-memory, SQLite or Testcontainers provider and no TRUNCATE in the
  test delta.

FINDINGS
- [MEDIUM 1] src/Api/CatEcommerce.Api/Controllers/CatsController.cs:470 (`ApplyPaging`) and
  src/Api/CatEcommerce.Api/Controllers/VaccinesController.cs:58 — `pageNumber` has no
  `PagingRules.MaxPageNumber` cap.
  - Every other paged controller in the host caps it: `AdminAuditEntriesController:44`,
    `AdminLeaveRequestsController:27`, and `StaffAccounts`/`StaffCustomers` on
    `ho/integrator-host` (99861dd).
  - `PagingRules`' own doc comment says the cap exists because `Query<T>.Skip = (PageNumber-1)*PageSize`
    overflows `int`. With `pageSize` clamped to 100, `pageNumber` above about 21.5 million still
    overflows `Skip` to a negative `OFFSET`.
  - So the branch reuses half of `PagingRules`: the pageSize cap, but not the pageNumber cap.
  - It is self-contained and pre-existing in shape. The 500 it produces is a runtime claim for
    **SECURITY/TEST**, not verified here.
- [LOW 1] src/Shared/CatEcommerce.Shared/Common/Constants/PagingRules.cs:1-19 — the record no
  longer matches its uses.
  - The doc comment still describes `MaxPageSize` as the cap "for every other paged **admin**
    endpoint, applied by **rejecting** with 400".
  - The Cat and Vaccine endpoints now use it by **clamping**, per the owner decision.
  - Both behaviours are legitimate. The shared constant's comment should say it has two users with
    different policies.
- [LOW 2] src/Api/CatEcommerce.Api/Controllers/VaccinesController.cs:56-65 — one controller calls
  another controller's static helper.
  - It calls `CatsController.InvalidPageSizeValidationResponse()` and re-implements
    `ApplyPaging`'s body inline instead of sharing it.
  - It works, but a shared paging helper (or `PagingRules`) is the natural home, rather than a
    dependency between controllers.
- [LOW 3] src/Api/CatEcommerce.Api/Controllers/CatsController.cs:429 — the port leaks an adapter's
  exception type.
  - The controller catches `QuestPDF.Drawing.Exceptions.DocumentDrawingException` coming out of
    the `ICatMedicalHistoryPdfRenderer` port.
  - As a result, the Api action knows which PDF library sits behind the port. The adapter
    (`QuestPdfCatMedicalHistoryRenderer`) is the place to translate it into a port-level failure.
  - The logging decision itself is sound: it logs the cat id only.
- [LOW 4] src/Application/CatEcommerce.Application/CommandHandlers/Validators/RecordCatWeightCommandValidator.cs:13-18
  — a stale comment.
  - It says `RecordedByStaffId` `NotEmpty()` was dropped because `UserId` is `Guid.Empty` "until
    HO-ADM-001's `[RequireAction]` lands on this controller".
  - That has now landed (`[RequireAction(Actions.Cats.RecordClinical)]`, `CatsController.cs:246`).
    The comment is out of date, and the `NotEmpty()` guard could be restored as defence in depth.

CARRIED (not findings against this pass)
- **HO-CAT-002 web delta (cb2ddd2, 11 `web/**` files).** It is inside HO-CAT-002 §13 (`web/**`),
  required by owner decision (c) and recorded in HO-CAT-002's new Amended row.
  `docs/gates/HO-CAT-002-*.md` (2026-09-25) predates it. **HO-CAT-002's gates owe a delta run** on
  cb2ddd2. This backend gate did not review the TypeScript.
- **Photo `StorageKey` trust → HO-FIL-001** (owner decision).
- The CorrelationId LOW, the §12.4/§12.2 wording LOWs and the three `.ai/`/package integrator items
  listed above.

SCOPE CHECK
- files in allowed scope: 76/76.
  - **63 under HO-CAT-001 §13 globs:**
    - 3 `docs/gates/HO-CAT-001-*.md`;
    - 44 `src/**`;
    - 16 `tests/CatEcommerce.{Application,Domain,Integration}Tests/**`.
  - Some of those 63 are in scope only on the strength of an Amended row or an authorisation:
    - `AuditEntryConfiguration.cs`, `20260926135821_WidenAuditEntryValues{,.Designer}.cs` and the
      snapshot: integrator-authorised migration, Amended row 2026-09-26 (merge).
    - `AdministrationIntegrationTestBase.cs`, `AdminGlobalSettingsLiveEffectEndpointTests.cs` and
      `CatVaccineAuthorizationSecurityTests.cs` (HO-ADM-001 test files): post-merge compatibility
      fixes recorded in the same Amended row.
    - `CatEcommerce.Infrastructure.csproj` (embedded font, no package): recorded in the gate-fix
      Amended row.
  - **2 `docs/handover/*.md`:** HO-CAT-001's own §1 Amended rows and §12.3 table annotations, and
    HO-CAT-002's Amended row propagating owner decision (c). These are owner/integrator records,
    the same precedent as HO-ACC-001's gate.
  - **11 `web/**`:** inside HO-CAT-002 §13, and required by owner decision (c). See CARRIED for the
    gate they still owe.
- files outside allowed scope: none. There is no `.ai/**`, `.claude/**`, SRS, `instructions/**`,
  `NuGet.Config`, `global.json`, `.sln` or `Program.cs`, and no other bounded context beyond the
  recorded integrator authorisations.
- Result: **PASS. 0 BLOCKER, 0 HIGH, 1 MEDIUM, 4 LOW.**
  - Both first-pass HIGHs are closed. Every first-pass MEDIUM is closed. Two first-pass LOWs are
    carried as handover defects.

---

# First pass (2026-09-26, reviewed at 7abc9e4) — retained unchanged below

GATE: ARCHITECTURE
HANDOVER: HO-CAT-001
COMMITS: 4007225 (the implementation, landed on master 2026-09-21 as "added handover docs"), reviewed as it stands at master 7abc9e4, including the later commits that touched the Cat file set: 2587c0f b6906ff 291b1fe 2fc4707 a7a8d08 bf2c860 1f66b1b 6ae20b6 56bc233 58ef744 1462bb6 4012393 39254c0 53fb683 328622e fdec8b0 39d8c1f 2f46bc9
DATE: 2026-09-26
STATUS: FAIL

This is the first ARCHITECTURE run on HO-CAT-001. The implementation landed on master in `4007225`
without any gate record. I reviewed every file that implements the handover as it now stands on
master (`7abc9e4`), not the 2026-09-21 snapshot. That means the review includes:
- the ADR-341 per-area partials;
- `RecordClassification.CatProfiles.cs` (HO-XCT-001 contracts);
- the C10 change, already gated by `HO-CAT-001-C10-ARCHITECTURE.md`;
- HO-ACC-001's edits to the Cat audit handlers and to `CatAggregate`;
- the ADR-342 host pipeline.

I reviewed against:
- HO-CAT-001: all sections, including the three §1 Amended rows;
- `.ai/`: `architecture.md`, `cqrs.md`, `bounded-contexts.md`, `namespace-conventions.md`,
  `di-registration-guide.md`, `host-composition.md`, `command-handler-pattern.md`,
  `read-side-pattern.md`, `persistence-ef-core.md`, `domain-events-and-outbox.md` and
  `enumeration-pattern.md`;
- ADR-010…023, ADR-040, ADR-341 and ADR-342 as amended;
- HO-XCT-001 §5.2 (C-ALL-1…4), §8.2, §8.8 and §10.3;
- `docs/handover/CONTRACTS.md` §Cross-cutting.

I ran one command, read-only: `dotnet dotnet-ef migrations has-pending-model-changes`. It ran on a
`git archive` export of HEAD in a scratch folder, so it would not collide with the SECURITY and
CODE_REVIEW builds running in parallel. Result: **"No changes have been made to the model since the
last migration."**

I ran no tests. Every runtime claim below is marked for TEST.

FINDINGS
- [HIGH] src/Read/CatEcommerce.Read/QueryHandlers/Cat/GetCatProfileQueryHandler.cs:23-24 — the
  handler rehydrates the aggregate to serve a read, which HO-CAT-001 §12.3 and
  `.ai/read-side-pattern.md` §4 rules 1–2 forbid.
  - §12.3 says query handlers "use `AsNoTracking()`, and `Select` into the ViewModel **before**
    `ToListAsync`". The pattern doc says "Project, never rehydrate" and "`Select` before
    `ToListAsync`".
  - The handler instead calls `db.Cats.AsNoTracking().FirstOrDefaultAsync(...)`. That materialises
    a full `CatAggregate` through its private constructor, with all six owned collections, and
    only then maps it in memory. Vaccinations and parasite treatments are loaded even though Q1
    never shows them.
  - The class comment at :11-13 records this as a deliberate choice ("a projection buys nothing
    here"). The rule has no size exemption, and neither the handover nor any ADR records one.
  - Fix: project in the database with `db.Cats.AsNoTracking().Where(...).Select(c => new
    CatProfileViewModel(...))`. The owned collections can be projected with nested `Select`s.
    `Enumeration` members can be read directly and `DisplayName` resolved after
    materialisation, as `GetCatsQueryHandler` already does.
- [HIGH] src/Read/CatEcommerce.Read/QueryHandlers/Cat/GetCatMedicalHistoryQueryHandler.cs:25 — the
  same rehydration. It loads `db.Cats.AsNoTracking().FirstOrDefaultAsync(...)`, a whole
  `CatAggregate` including the photo collection, which Q5 does not use, and maps it in memory. The
  fix is the same as above. The weight-history query in the same handler (:29-33) projects
  correctly.
- [MEDIUM] src/Api/CatEcommerce.Api/Modules/CatProfilesModule.cs:148-150 — a §20.6 stop condition
  was bypassed at merge.
  - HO-CAT-001 §20.6 says "QuestPDF licensing must be confirmed before merge… If it does not apply
    here, stop and report."
  - The code is on master. It sets `QuestPDF.Settings.License = LicenseType.Community`, and its
    own comment says the licence was "not resolved here".
  - I found no record of the owner confirming it. HO-BIL-001 (F32, §20.9) and HO-RPT-001 (§20.6)
    both inherit the open stop.
  - It was reported in a comment, not hidden. It still needs an explicit owner decision, recorded
    as a §1 Amended row, before ACCEPTANCE. Owner decision needed.
- [MEDIUM] tests/CatEcommerce.Application.Tests/CatEcommerce.Application.Tests.csproj (+ProjectReference
  to `src/Read/CatEcommerce.Read`) — a csproj edit outside the two that §13 permits.
  - §13 allows csproj edits only to (a) add QuestPDF and QRCoder to Infrastructure and (b) remove
    Testcontainers from IntegrationTests. This edit is neither, and no Amended row covers it.
  - The production boundary is intact: `CatEcommerce.Application.csproj` still references only
    Domain and Shared.
  - The reference exists so Read-side calculator tests (U34–U38) can live in the Application test
    project.
  - Fix: record it in an Amended row. Alternatively, when the integrator next touches test
    projects, move those tests to a Read or Infrastructure test project.
- [MEDIUM] Commit 4007225 — 27 files that are not HO-CAT-001 implementation were committed with it.
  - They are `.claude/agents/handover-writer.md` (§14 forbidden, `.claude/**`) and 26
    `docs/handover/HO-US-ACC-*.md` files, which are outside §13 and have since been deleted.
  - Both look like the owner's own handover-authoring work, swept into the same commit, which
    matches the commit title. They are not implementation files, so I excluded them from the
    scope count below.
  - **Owner to confirm the attribution.** If the HO-CAT-001 implementer wrote the `.claude` file,
    it is a §14 BLOCKER.
- [MEDIUM] src/Api/CatEcommerce.Api/Controllers/CatsController.cs:56-72 and
  src/Read/CatEcommerce.Read/QueryHandlers/Cat/GetCatsQueryHandler.cs:35-50 — this handover
  requirement has no code: I19 expects "paging headers correct".
  - The list is paginated with the base `Skip`/`Take`, so A23 and UI-4 are met in shape.
  - But the endpoint returns a bare array: no total count, no paging headers, and no
    `PagedResult<T>`, even though that type now exists in `Read/ViewModels/PagedResult.cs`.
  - The same applies to `GET .../weights` and `GET /api/vaccines`.
  - For TEST: I19 cannot pass as written.
- [LOW] src/Shared/CatEcommerce.Shared/Common/Constants/RecordClassification.CatProfiles.cs:14-20 —
  a stale comment. It still says `audit.audit_entry` is "an intentional, temporary gap… until
  Accounts lands". Accounts has landed and classifies it at `RecordClassification.Accounts.cs:42`.
- [LOW] docs/handover/HO-CAT-001.md:14 — a record gap: the 2026-09-23 row that lists appends by later
  handovers does not list HO-ACC-001's appends to this area.
  - `CatAggregate.ReassignOwnerOnCustomerMerge` (`CatAggregate.cs:420`),
    `CatOwnerReassignedOnMergeEvent` and `CatOwnerMergeReassignmentDto` (HO-ACC-001 §8.9, its §13
    line 1217).
  - The `IClientIpAccessor`, `TimeProvider` and `AuditCategory` changes to the three Cat audit
    handlers (HO-ACC-001 §1 Amended (19)(b)).
  - `SessionUserContextProvider` replacing `DevUserContextProvider` as the §12.5 seam.
  - HO-ACC-001 governs this code and its gates reviewed it. HO-CAT-001's own record is simply
    silent about it.
- [LOW] src/Api/CatEcommerce.Api/Controllers/CatsController.cs:419-431 — `TryParseEnum` returns a
  400 in a different shape.
  - Request DTOs carry `Enumeration` values as strings. An unknown value returns
    `400 { error }`, not a `ValidationResult`.
  - ADR-014 describes a 4xx that carries `CommandResponse.ValidationResult`. HO-CAT-001 does not
    say how strings map to `Enumeration` values, so this is a gap in the specification, not a
    broken rule.
  - HO-CAT-002's client should be confirmed to handle both shapes.
- [LOW] src/Api/CatEcommerce.Api/Controllers/CatsController.cs:45 (and every other action) — the
  CorrelationId is not the request's trace id.
  - Every action sets `CorrelationId = Guid.NewGuid()`.
  - The §12.4 snippet's `HttpContext.TraceIdentifier.ToCorrelationId()` does not exist anywhere
    in the solution, which is a handover defect.
  - As a result, the audit rows' correlation id cannot be joined to the request's trace id.
  - The implementation is consistent with every other controller.
- [LOW] docs/handover/HO-CAT-001.md §12.4 and §12.2 — two handover wording defects. The code
  deviates correctly in both cases:
  - §12.4 says "Controllers inject `IInMemoryDispatcher` and nothing else". Yet its own example
    uses `userContextProvider`, and its route table sends Q5 to the renderer and Q7 to the QR
    generator. `CatsController.cs:20-24` injects exactly those three extra ports.
  - §12.2 says both document ports "take a ViewModel". That is impossible, because
    `Read → Infrastructure` would become a cycle. `ICatMedicalHistoryPdfRenderer.cs:6-11`
    documents the deviation, and the controller maps the query result into
    `CatMedicalHistoryDocument`.

HANDOVER REQUIREMENTS WITH NO CODE (for TEST / ACCEPTANCE; not architecture findings)
- §16 integration cases: only I10a exists (`Endpoints/ParasiteTreatmentEndpointTests.cs`).
  - I1 is partly covered by HO-ACC-001's `HostCompositionTests`.
  - I22 is partly covered by `Persistence/MigrationsOnAnEmptyDatabaseTests.cs`, which asserts only
    that `clinic.cat` exists, not the §12.1 index set.
  - I2–I10, I11–I21 have no test code: 404, the 409 on `xmin`, the audit rows, read-after-write,
    the PDF and the QR code.
- `docs/test-cases/TC-HO-CAT-001.md` does not exist. Only `TC-HO-CAT-001-C10.md` does.
- §20.3: the Cat and Vaccine endpoints carry no `[RequireAction]`.
  - HO-ACC-001 §1 Amended (1) says HO-ADM-001 §12.3 will guard them, and HO-ADM-001 is not built.
  - A18 (US-CAT-08 AC2) therefore still rests on the `ownerId` query filter only. Stated for
    SECURITY, as §20.3 requires.
- HO-BRD-001's `VaccinationStatusRule` and `CatAggregate.EvaluateVaccinationStatus` appends (§1
  Amended) are absent. This is expected: HO-BRD-001 is not built.

CODE ON MASTER NOT IN HO-CAT-001 (reviewed; no finding unless listed above)
- `CatAggregate.ReassignOwnerOnCustomerMerge` and its event and DTO: HO-ACC-001 (LOW above).
- The audit handlers' `IClientIpAccessor` and `TimeProvider` injections: HO-ACC-001 (19)(b).
- `CatProfileOptions.PublicWebBaseUrl`: needed for the Q7 QR target, and self-documented.
- `CatVaccinationStatusCalculator`: the Q4 algorithm factored out as a pure function.
- The `CatMedicalHistoryDocument` records: the necessary §12.2 deviation.
- `Filters/ConcurrencyExceptionFilter`: the §12.4 409 mapping.
- The two `EfAggregateRootRepository` corrections and `ValueGeneratedNever` / optional
  `Tags`/`Language`/`ServiceId`: ADR-021.

CARRIED INTEGRATOR ITEMS (`.ai/**` is §14 for this handover)
- `.ai/persistence-ef-core.md` §2 still says "copy exactly" without ADR-021's two corrections: the
  guarded `.Update()` and `ValueGeneratedNever()`. A new area copying it will hit the same spurious
  `DbUpdateConcurrencyException` on its first owned-child insert.
- `.ai/enumeration-pattern.md:7` still says `Enumeration` is "Provided by
  `Platform.Infrastructure.Core`". ADR-022 flagged this in 2026-09-20.
- `Microsoft.AspNetCore.OpenApi` in `CatEcommerce.Api.csproj` is used by no source file. It dates
  from the baseline (`bb9fbf6`), not from HO-CAT-001.

VERIFIED COMPLIANT (no finding)
- **Layers.**
  - Domain references only Shared and the Platform packages. It has no EF, Application, Read or
    Api type.
  - Application → Domain and Shared. Read → Infrastructure and Shared.
  - Neither of Application and Read references the other in production code.
- **Aggregates.**
  - All setters are private.
  - Collections are `IReadOnlyList` over private lists.
  - Every mutation method raises its specified event, then calls `UpdateVersion()`. `AuditEntry`
    raises none, by design (§8.4).
  - `EnsureAlive()` guards the methods §8.2 lists.
  - Invariants live in the aggregate and are translated in the handler through
    `DomainRuleViolationException`. `OwnerUnchanged` was correctly moved from the handler into the
    aggregate.
  - Other aggregates are referenced by id only.
  - Weight history is its own aggregate (ADR-023). `VaccineAggregate` is loaded read-only in C9.
  - One aggregate is mutated per command, including C4.
- **CQRS.**
  - The 13 handlers inject only `ILogger` and `IAggregateRootRepository<T>`, with the permitted
    read-only `VaccineAggregate` lookup.
  - Every save passes `EventPublishOption.InMemoryPublishOnly`. The audit handlers pass
    `DoNotPublish`, as §11.7 specifies.
  - Expected outcomes are returned, not thrown.
  - The C13 uniqueness check is `ExistAsync` in the handler.
  - Controllers inject `IInMemoryDispatcher` and dispatch exactly one command or query per action.
  - `ValidateCommand = true` is set on all 13 commands.
  - `GetResultAsync` is used for queries. There is no `202`.
  - None of the forbidden shapes appears: `IDispatcher`, `IEventPublisher`, broker types, Mongo,
    rejection events, `CommandServices` or `QueuePublishOnly`. The one occurrence of
    `QueuePublishOnly` is the mandated guard in the repository.
- **Events.**
  - All events are past tense and `sealed : Event`.
  - Dispatch resolves handlers on the runtime type (`EfAggregateRootRepository.cs:85`).
  - No handler calls `SaveChangesAsync` or another module.
  - The three audit handlers each carry the §11.7 "fails after commit" comment.
- **Registration (ADR-341).**
  - `CatProfilesModule.AddCatProfilesModule` registers all 13 handlers, 13 `IValidator<T>`, 7
    query handlers, 3 event handlers, 2 document ports and `CatProfileOptions`, all Scoped.
  - It is called once from `Program.cs:109`.
  - There is no assembly scanning, and nothing from `AddInMemoryBusServices()` is hand-registered.
  - The per-area partials are `CatEcommerceDbContext.Cat.cs`, `BusinessViolationCodes.Cat.cs` and
    `RecordClassification.CatProfiles.cs`.
- **Host (ADR-342).**
  - `ValidateScopes` and `ValidateOnBuild` are on.
  - `AddInMemoryBusServices()` is the only dispatch registration.
  - The explicit pipeline is `UseForwardedHeaders → UseRouting → SafeExceptionHandlerMiddleware →
    UseCors → UseAccountsModule → MapHealthChecks → MapControllers`. `UseHttpPipeline()` is not
    called.
  - Cat adds no pipeline middleware.
  - `ConcurrencyExceptionFilter` is an MVC filter, consistent with ADR-342.
- **Persistence.**
  - Every Cat child collection is `OwnsMany` with field access mode.
  - `xmin` is a row version on all four aggregate tables.
  - `Events` and `Metadata` are ignored.
  - Every `Enumeration` is converted by `Value`.
  - `DateOnly` maps to `date`. `DateTimeOffset` and the base `DateTime` both map to `timestamptz`.
    Weight is `numeric(5,2)`, and there is no money.
  - The repository returns aggregates, not `IQueryable`. Reads filter `!IsMarkedToDelete`.
  - Every §12.1 index is in `AddCatProfile`, including the unique `vaccine.code`.
  - `DROP` appears only in `Down`.
  - The migration matches the model: `has-pending-model-changes` is clean.
- **RecordClassification (C-ALL-1…4).**
  - All nine Cat tables are classified exactly as HO-XCT-001 §10.3 specifies. `audit.audit_entry`
    is classified by Accounts, with no duplicate.
  - C-ALL-2: no Cat eraser is owed. HO-XCT-001 §8.8 exempts Cat in `ErasureExemptions` ("`OwnerId`
    only"), and HO-XCT-001 builds that exemption.
  - C-ALL-3: no review source is owed by this handover. `CatClinicalReviewSource` is HO-XCT-001's
    own (§13 line 829).
  - There are no purgeable Cat tables, so no purge source is owed.
  - C-ALL-4: no method removes a cat, vaccination, parasite treatment, weight record or photo.
    `UpdateMedicalProfile` replaces only the three `CurrentState`/`AggregateOnly` collections
    (F32).
- **Tests.** No in-memory or SQLite provider, no Testcontainers and no TRUNCATE (§20.1, §20.1a).

SCOPE CHECK
- files in allowed scope: 181/181.
  - 169 are `src/**` and `tests/**` implementation files from 4007225: 163 added, 5 modified and 1
    deleted (`appsettings.Development.json`, now git-ignored).
  - 1 is the `.ai/decisions/adr.md` append in 4007225. It holds ADR-021, 022 and 023 only, as §9
    and §13 permit.
  - 11 are Cat-specific files added since then (`CatProfilesModule.cs`, `CatEcommerceDbContext.Cat.cs`,
    `BusinessViolationCodes.Cat.cs`, `RecordClassification.CatProfiles.cs`, `CatApiFactory.cs`,
    `CatIntegrationTestBase.cs`, `ParasiteTreatmentEndpointTests.cs`,
    `RecordCatParasiteTreatmentCommandHandlerTests.cs`, and HO-ACC-001's
    `CatOwnerReassignedOnMergeEvent.cs`, `CatOwnerMergeReassignmentDto.cs`,
    `CatAggregateMergeTests.cs`), all under the `src/**` and
    `tests/CatEcommerce.{Domain,Application,Integration}Tests/**` globs.
- files outside allowed scope: none among the implementation files. There are two caveats:
  - The `Application.Tests.csproj` ProjectReference is inside the path glob but outside §13's csproj
    clause (MEDIUM above).
  - The 27 non-implementation files co-committed in 4007225 are excluded pending the owner's
    confirmation of who wrote them (MEDIUM above).
- Result: FAIL. 0 BLOCKER, 2 HIGH, 4 MEDIUM, 5 LOW.
  - Both HIGHs are the same fault: rehydration on the read side, in Q1 and Q5.
  - Fixing them is local to two query handlers and needs no schema or contract change. A re-run of
    this gate can be a short delta.
