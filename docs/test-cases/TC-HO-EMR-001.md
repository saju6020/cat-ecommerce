# TC-HO-EMR-001 — Clinic: Consultation and Medical Records (backend)

Handover: docs/handover/HO-EMR-001.md
Branch: `ho/EMR-001` (from master `50f1ab1`)
Written: 2026-10-07 (implementation session) — the **implementation evidence map**: for every §15 / §16 case and every §18 criterion, the test that
proves it. **TEST gate re-executed it on 2026-10-07 at 729393f** (see "TEST gate execution" below); the per-row statuses in the tables that follow are
the TEST gate's observed ones. Phase F rows are `DEFERRED` (owner decision), not failed.

> Statuses record what was **observed**. `DEFERRED` marks Phase F (stock-moving treatments, US-CLN-15), which was not built on the
> owner's instruction — its cases are not failures and are not counted as passes.

## Environment

- Build: `dotnet build CatEcommerce.sln` — 0 warnings, 0 errors.
- Database: this branch's own `catecommerce_emr` (`CATECOMMERCE_TEST_DB` = full Npgsql connection string), migrated to head
  (`AddConsents`). **Never `catecommerce_dev`**: `ConsultationApiFactory` refuses to construct when the variable is unset or names
  `catecommerce_dev`.
- Scanner `EicarTestScanner`, store `LocalFileStore` under a per-run temp root (Testing environment). No GCS bucket, no `clamd`.
- Clock: every clinical test runs on a host with a controllable `FakeTimeProvider` (`SeedClinicalWorldAsync`).
- Suites at this commit: see "Results" at the end.

## TEST gate execution (2026-10-07, commit 729393f + the new test file)

Database `catecommerce_emr` only (`CATECOMMERCE_TEST_DB` = its full Npgsql string, the committed local-dev credentials of `CatEcommerceApiFactory`
with the database name replaced); `catecommerce_dev` never touched. Migrations listed through `AddConsents`; `dotnet ef migrations
has-pending-model-changes` → **"No changes have been made to the model since the last migration."**

Observed runs: build 0 warnings / 0 errors; Domain 867/867; Application 211/211; Infrastructure 128/128; Integration full suite 827 pass / 1 fail
(`ShopCataloguePerformanceTests.I_E11`, known load-dependent flake, **passed when rerun alone**: 1/1). After adding `ClinicalRecordGateTests`:
9 pass, 2 skipped (known defects below, observed FAIL before being skipped). I-E4 run alone twice: summary p95 56 / 58 ms (budget 600), section page p95 46 / 41 ms (budget 300).

> **Update 2026-10-13 (`ho/EMR-001-F`):** the 19 DEFERRED rows below are built and PASS -- see "Phase F" at the end. The counts that follow are the 2026-10-07 TEST gate as recorded.

Counts (case IDs from §15/§16/§18 plus the gate's own rows): **PASS 133 · FAIL 2 · BLOCKED 1 · DEFERRED 19 · NOT RUN 0**
(§15: 51 PASS, 9 DEFERRED; §16: 55 PASS, 8 DEFERRED; §18: 21 PASS, 2 DEFERRED; gate rows G1–G8: 6 PASS, 2 FAIL; BLOCKED: G9.) U-D6..U-D9 are
HO-FIL-001's classes (`FileRulesTests`, `HmacFileUrlSignerTests`, `DevelopmentNoOpScannerTests`, `FileNameSanitizerTests`) and passed in the Infrastructure/Domain runs.
Every non-F §16 ID was confirmed to have a named test in `--list-tests`.

### TEST-gate rows added (`tests/CatEcommerce.IntegrationTests/Endpoints/ClinicalRecordGateTests.cs`)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| G1 | A database `CR001` raised inside a vitals+weight commit (test-only trigger on this cat's weight insert, dropped after) | `409` with "finalised and cannot be changed"; vitals rolled back | PASS | `G1_ACr001FromTheDatabase_IsAnswered409_AndTheCommitRollsBack` |
| G2 | UPDATE/DELETE of vitals and problems of a finalised visit; TRUNCATE of both tables | `CR001` each; values unchanged | PASS | `G2_ChildRowsOfAFinalisedVisit_RefuseUpdateAndDelete` |
| G3 | `UPDATE visit_vitals SET visit_id = <open visit>` for a row of a finalised visit | `CR001`; row stays | PASS (was FAIL, F-1, MEDIUM) | Fixed 2026-10-09 by migration `HardenClinicalAndBillingTriggers` (`46ce13fa`): `visit_child_frozen` refuses any change of `visit_id` and checks `OLD.visit_id`'s visit. Un-skipped, assertion unchanged. Added G10 (open row cannot be moved to a finalised or any other visit; in-place edit still works), G11 (visit cannot be marked to delete), G12 (diagnostic order identity and flag frozen; result recording still works), I-H6b (consent flag frozen; withdrawal still works) |
| G4 | Audit `DataChange` snapshot of a diagnostic order after recording a result set with two values | Observed behaviour pinned | PASS (pins observed) | Snapshot holds the order and `DiagnosticResultSet.*` (Id, ReportedOn, RecordedBy, ...); **`diagnostic_result_value` rows (names, numbers, flags) are absent** |
| G5 | Audit snapshot of a visit saved with two problems | Both problems distinguishable | **FAIL** (F-2, MEDIUM) | One set of `VisitProblem.*` keys: the later row overwrites the earlier (dictionary keyed by type name). `Skip`ped with the reason; assertion unchanged |
| G6 | Raw JSON `measuredOn` with `+06:00`, `-05:00`, `+05:30`, `Z` | `201`; same instant stored | PASS ×4 | `G6_..(offset: ...)`; stored `timestamptz` compared in UTC |
| G7 | `measuredOn` with no offset | not a 5xx | PASS | `G7_...` |
| G8 | Draft first-write race: 8 fresh visits × 3 simultaneous first writes by one author | no 5xx; ≥1 `204`; exactly one row | PASS | `G8_...`; also existing `TheDraftFirstWriteRace_*` rerun 3× green (with I-C2/3, I-C7, I-C9, I-B6, `MeasuredOn_WithALocalOffset`, open-visit delete probe: 7/7 each time) |
| G9 | Startup refusal outside Development/Testing with a short signing key or root inside the content root (I-A2, second half) | host fails to start | BLOCKED | HO-FIL-001's guard; no `Staging`/`Production` host was started here. Unblock: a host-start test in the FIL-001 sheet |

## Verified platform facts (§7) — recorded as F31–F33

| # | Fact | Result | Evidence |
|---|---|---|---|
| **V1 / F31** | A PL/pgSQL row trigger on an EF-mapped table raises through Npgsql as `PostgresException` with the trigger's SQLSTATE and rolls back the whole `SaveChangesAsync` | **Holds.** `CR001` surfaces as `PostgresException.SqlState`; `ClinicalRecordFrozenFilter` maps it to `409`; I-B6 shows a failing insert inside the commit rolls back the visit's own row too | I-C9, I-D9, I-H6, I-B6 |
| **V2 / F32** | Nested `OwnsMany` (result values inside result sets) maps and round-trips on EF Core 9 / Npgsql with `ValueGeneratedNever()` on both levels | **Holds.** Values are read back in a stable order through a `position` column (EF returns an owned collection unordered) | I-D2, I-D8 |
| **V3 / F33** | Kestrel request-size limits override the global limit per action | **Not applicable here** — the upload endpoint, its limits and its `413`/`FileTooLarge` handling are HO-FIL-001's; the clinical flow only exercises it (I-D5) | I-D5 |

## Unit cases (§15)

| ID | Case | Status | Evidence |
|---|---|---|---|
| U-A1 | Diagnosis code lower-case / blank / duplicate | PASS | `ClinicalCatalogueHandlerTests.CreateDiagnosisCode_*`, Domain `ClinicalCatalogueAndConsentTests` |
| U-A2 | Deactivate `SYS-NAD` | PASS | `ClinicalCatalogueHandlerTests.SetActive_OnTheSystemCode_IsRefused` |
| U-A3 | Lab analyte low > high; numeric without unit | PASS | Domain `ClinicalCatalogueAndConsentTests`; `ClinicalCatalogueEndpointTests.LabAnalyte_*` |
| U-A4 | `NoteTemplate.Update` bumps `Version` | PASS | Domain `ClinicalCatalogueAndConsentTests`; `ClinicalCatalogueEndpointTests.NoteTemplate_*` |
| U-A5 | Access command writes one `ClinicalRecordAccess` row, `DoNotPublish` | PASS | `ClinicalCatalogueHandlerTests.RecordClinicalRecordAccess_*` |
| U-B1 | Open for Booked / Completed / CheckedIn / InConsultation | PASS | Domain `VisitAggregateTests.Open_*`; `VisitHandlerTests.Open_*` |
| U-B2 | Open for a deceased cat | PASS | `VisitHandlerTests.Open_ForADeceasedCat_IsRefused` |
| U-B3..B7 | Vitals rules, two-tier ranges, boundaries | PASS | Domain `VisitAggregateTests`, `ClinicalRulesTests`; `VisitHandlerTests.RecordVitals_*` |
| U-B8 / U-B9 | Weight companion commit; no weight without a weight | PASS | `VisitHandlerTests.RecordVitals_WithAWeight_*`, `_WithoutAWeight_*` |
| U-C1 / U-C2 | Inactive / duplicate code; snapshot of code and name | PASS | `ConsultationNoteHandlerTests.SaveNote_*`, Domain `VisitAggregateTests` |
| U-C3 / U-C4 / U-C6 | Finalise preconditions; roles; twice | PASS | `ConsultationNoteHandlerTests.Finalise_*`, Domain `VisitAggregateTests` |
| U-C5 / U-C7 | Every mutation refused on a finalised visit; reflection: each public method calls `EnsureOpen` | PASS | Domain `VisitAggregateTests` |
| U-C8 / U-C9 | Addendum rules; no method on the addendum root besides `Add` | PASS | `ConsultationNoteHandlerTests.AddAddendum_*`, Domain `VisitAddendumAndDraftTests` |
| U-C10 | Draft read: author only, only when newer, only while open | PASS | Domain `VisitAddendumAndDraftTests`; I-C2/I-C3 |
| U-C11 / U-C12 | `VisitAllowedActions`; template only before the first save | PASS | Domain `ClinicalRulesTests`; I-C4 |
| U-C13 | Finalise enqueues `visit.summary-available` to the current owner before the save | PASS | `ConsultationNoteHandlerTests.Finalise_ByAVet_EnqueuesTheSummaryMessage_*` |
| U-D1 / U-D2 | Range classifier; analyte defaults copied onto the value | PASS | Domain `ClinicalRulesTests`; `DiagnosticHandlerTests.RecordResults_*` |
| U-D3..U-D5 | Superseding another order's set; cancel with results; results after finalisation | PASS | `DiagnosticHandlerTests`, Domain `DiagnosticOrderAggregateTests` |
| U-D6..U-D9 | Content sniffing, URL signing, scanner guards, file-name sanitising | **HO-FIL-001's** (the pipeline moved there, README C-5) | `FileRulesTests`, `HmacFileUrlSignerTests`, `DevelopmentNoOpScannerTests`, `FileNameSanitizerTests` |
| U-D10 | Photo #21; photo date in the future | PASS | Domain `VisitAggregateTests`; `DiagnosticHandlerTests.AttachPhoto_*` |
| U-F1..U-F9 | Treatments and stock | Built on `ho/EMR-001-F` -- see **Phase F** below | see below |
| U-G1 | Follow-up send time | PASS | Domain `ClinicalRulesTests`; `FollowUpHandlerTests.Reminder_*` |
| U-G2 | `VisitFollowUpGuard` | PASS | `VisitFollowUpGuardTests` (8 cases) |
| U-G3 | The two template definitions | PASS | `ConsultationCrossCuttingTests.U_G3_*` |
| U-G4 / U-G5 | No summary for an open visit; reversed treatment lines excluded, addenda included | PASS (treatments: n/a until F) | I-G1, I-G2; `QuestPdfDischargeSummaryRendererTests` |
| U-H1..U-H4 | Signature bounds; evidence mismatch; `IsValidFor`; SVG polylines only | PASS | Domain `ClinicalCatalogueAndConsentTests`, `ClinicalRulesTests`; I-H2, I-H5 |
| U-X1 | No string property on a visit event | PASS | `ConsultationCrossCuttingTests.U_X1_*` |
| U-X2 | `AuditCaptureExclusions` | PASS | `AuditCaptureExclusionsTests` |
| U-X3 | Migration trigger SQL uses the enumeration value and covers every table | PASS | `ConsultationConventionTests.U_X3_*` |
| U-X4 | `Visits.Finalise` = `[Vet]` exactly; no write action includes Reception or Administrator | PASS | `ConsultationCrossCuttingTests.U_X4_*` |
| U-X5 | Every clinical GET carries `[ClinicalRecordAccess]` | PASS | `ConsultationConventionTests.U_X5_*` |

## Integration cases (§16)

| ID | Case | Status | Evidence |
|---|---|---|---|
| I-A1 | Tables, uniques, seeded `SYS-NAD`, every trigger | PASS | `ClinicalCatalogueEndpointTests.I_A1_*`; `ConsultationConventionTests.U_X3_*` |
| I-A2 | Every registration resolves; (short key / root inside content root refuse start = HO-FIL-001's guard) | PASS | `ConsultationRegistrationAndRetentionTests.I_A2_*`; `HostCompositionTests` |
| I-A3 | Vet creates a code; nurse refused + `AuthorizationDenied` audit | PASS | `ClinicalCatalogueEndpointTests.I_A3_*` |
| I-B1..I-B7 | Open, idempotent open, concurrent open, not checked in, vitals + weight, plausible warning, forced failure, reception refused | PASS | `VisitOpenAndVitalsEndpointTests` (I-B6 forces the weight insert to fail with a real database trigger scoped to the test's own cat) |
| I-C1..I-C15 | Note, draft, template, concurrency, finalise refusals, finalised refusals, **raw-SQL immutability probes**, addenda, ADR-133, summary message, audit rows | PASS | `VisitNoteFinaliseEndpointTests`; photo/follow-up half of I-C8 in `DischargeSummaryAndFollowUpEndpointTests.I_C8_*` |
| I-D1..I-D11 | Orders, flagged results, lab files served safely, DICOM attachment, upload refusals, URL expiry/tamper, scanner unavailable, after-finalisation results and supersession, append-only, photos, one file once | PASS | `DiagnosticsEndpointTests`; timeline half of I-D10 in `ClinicalHistoryEndpointTests` |
| I-E1..I-E3 | Summary declares every section; paging newest first; one access row per call | PASS | `ClinicalHistoryEndpointTests` |
| I-E4 | 10-year fixture budget | PASS — **measured: summary p95 39 ms (budget 600), section page p95 44 ms (budget 300)** | `ClinicalHistoryPerformanceTests` (timing test; rerun alone if the machine is loaded) |
| I-E5 | Access log forced to fail → `500`, no clinical data | PASS | `ClinicalHistoryEndpointTests.I_E5_*` |
| I-F1..I-F8 | Treatments, stock, shortfall, reversal, atomicity, controlled drugs, charge lines | Built on `ho/EMR-001-F` -- see **Phase F** below | see below |
| I-G1..I-G6 | Summary JSON/PDF (staff and portal), open visit 404, ownership 404 incl. after transfer, reminder held then sent once, changed/booked stands down, appointment link | PASS | `DischargeSummaryAndFollowUpEndpointTests` |
| I-H1..I-H6 | Consent form, on-screen consent, signed upload, stale version, status contract, DB refuses delete/edit | PASS | `ConsentEndpointTests` |
| I-X1 | No clinical content in any log line | PASS | `ConsultationCrossCuttingEndpointTests.I_X1_*` |
| I-X2 | Every clinical GET matched by an access row | PASS | `ConsultationCrossCuttingEndpointTests.I_X2_*` |

## Acceptance criteria (§18)

| # | Criterion | Status | Evidence |
|---|---|---|---|
| A1 | Vitals with units; weight reaches the history atomically | PASS | U-B8, I-B4, I-B6 |
| A2 | Plausible warning / confirm; absurd refused | PASS | U-B4..U-B7, I-B5 |
| A3 | SOAP + coded problems as a draft on the open visit | PASS | U-C1, U-C2, I-C1, I-C5 |
| A4 | Auto-saved working copy returned to its author | PASS | U-C10, I-C2, I-C3 |
| A5 | One history API; prescriptions / boarding / food **deferred to their areas** | PASS (built sections) | I-E1, I-E2 |
| A6 | 10-year cat within the server budget | PASS (measured) | I-E4 |
| A7 | Orders recorded as pending | PASS | I-D1 |
| A8 | Results flagged against reference ranges | PASS | U-D1, U-D2, I-D2 |
| A9 | PDF / JPEG / DICOM attachable, safely stored and served | PASS | I-D3..I-D7 (pipeline HO-FIL-001's) |
| A10 | Exact batch deducted in the same commit; expired refused | Built on `ho/EMR-001-F` -- see **Phase F** below | U-F1..U-F3, I-F1, I-F2, I-F6 |
| A11 | Insufficient stock warns; shortfall recordable | Built on `ho/EMR-001-F` -- see **Phase F** below | U-F4..U-F6, I-F3, I-F4 |
| A12 | Summary with diagnosis, home care, follow-up date; printable; in the portal — **treatments, medicines administered and take-home prescriptions not yet** (F, HO-PHM-001) | PASS (without treatments) | I-G1..I-G3, I-C14 |
| A13 | A finalised note is read-only | PASS | U-C5..U-C7, I-C8 |
| A14 | Corrections are timestamped, attributed addenda | PASS | U-C8, U-C9, I-C10, I-C11 |
| A15 | Direct edits refused through the API **and the database** | PASS | I-C8, I-C9, I-D9, I-H6 |
| A16 | Follow-up: appointment link or guarded reminder | PASS | U-G1, U-G2, I-G4..I-G6 |
| A17 | Dated, noted photo on the timeline | PASS | U-D10, I-D10, I-D11 |
| A18 | Template offered, stays editable | PASS | U-C12, I-C4 |
| A19 | Consent by signature or upload with the form version | PASS | U-H1, U-H2, U-H4, I-H1..I-H4 |
| A20 | Consent-status contract for "cannot start" | PASS (enforcement is HO-SRG-001's) | U-H3, I-H5 |
| A21 | Non-vets incl. administrators refused finalisation, audited | PASS | U-C4, U-X4, I-C6 |
| A22 | Every clinical read logged, fail closed | PASS | U-A5, U-X5, I-E3, I-E5, I-X2 |
| A23 | Appointment cannot complete while its visit is open | PASS | I-C12, I-C13 |

## Cases not run or not built, stated plainly

- **Phase F (US-CLN-15, A10, A11, I-F\*, U-F\*)** — deferred on the owner's instruction. Its contracts are left as specified:
  `VisitAggregate` has no treatments collection yet, `VisitDetailViewModel.Treatments` is always `[]`, the summary's
  `treatments` / `medicinesAdministered` are `[]`, and `GetVisitChargeLinesQuery`, the dispensary reads and the shortfall event do not exist.
- **Real ClamAV / GCS** — not reachable here; the clinical flows run on the Testing adapters (HO-FIL-001's own §16 covers the pipeline).
- **SMS / email providers** — absent (HO-NTF-001 OQ-1); the in-app copy of both messages is what is asserted.

## Results (final run of this implementation session)

| Suite | Result |
|---|---|
| `dotnet build CatEcommerce.sln --no-incremental` | 0 warnings, 0 errors |
| `CatEcommerce.Domain.Tests` | 867 / 867 |
| `CatEcommerce.Application.Tests` | 210 / 210 |
| `CatEcommerce.Infrastructure.Tests` | 128 / 128 |
| `CatEcommerce.IntegrationTests` (developer's last run, `catecommerce_emr`) | 826 / 826 — of which 72 are this slice's own (catalogues 5, visit/vitals 9, note/finalise 15, diagnostics 12, history 5 + budget 1, summary/follow-up 8, consent 8, cross-cutting 2, registration/retention 2, conventions 5) |
| `dotnet ef migrations has-pending-model-changes` | none |

Flake notes: `ShopCataloguePerformanceTests.I_E11` (another area's timing test) failed once under full-suite load and passed alone; this
slice's `ClinicalHistoryPerformanceTests` (I-E4) is also a timing test — measured p95 39 ms / 44 ms against budgets of 600 / 300 ms, so it has wide headroom.


---

## Phase F -- treatments, consumables and stock (US-CLN-15) -- branch `ho/EMR-001-F`

Written 2026-10-13 **before** the code (the rows started `NOT RUN`); each status below is the one observed when the cases ran at the end of the build.
Database: `catecommerce_emrf` only (`CATECOMMERCE_TEST_DB`), never `catecommerce_dev` / `catecommerce_test`.

### §20.2 comparison -- HO-EMR-001 §8.12 versus the code on master `b12fd39d`

| Contract | §8.12 assumed | Merged code | Verdict |
|---|---|---|---|
| IMP-1 one dispensary per branch; ledger picks locations | `LocationIds = null`, ledger uses every permitted active location | `StockLocationRules`: `ClinicalUse` is permitted at `ClinicDispensary` only; `EfStockLedger` filters candidates by purpose. `GetClinicDispensaryAsync` returns the first active dispensary by code (uniqueness is not enforced by a constraint) | Holds |
| IMP-2 batch fields | `SkuId`, `LotNumber`, `ExpiresOn`, `Status` | `StockMovementLine(BatchId, LotNumber, ExpiresOn, LocationId, Quantity, FefoOverridden)`; `DispensaryBatch(..., BatchStatus, OnHand, Reserved)` | Holds (shapes named) |
| IMP-3 `PlanAsync` | FEFO plan, excludes expired/quarantined/recalled/reserved, writes nothing | Same; `StockPlan(Lines, Shortfall)` | Holds |
| IMP-4 `DeductAsync` | `StockReference(VisitTreatment, LineId)`, `StockParty(CustomerId, CatId)`, enlists | Reference kind is **`StockReferenceKind.Visit`** (CONTRACTS D-2: the owner's list wins); `ReferenceLineId` is separate; `ClinicalUse` needs the cat; enlists without saving; returns movement lines **without ledger-entry ids** | Name differs; ids not returned (see decisions) |
| IMP-5 race -> 409 | commit-time CHECK mapped | `UniqueConstraintExceptionFilter` maps `ck_stock_position_balance` (23514) -> 409 `InsufficientStock` globally | Holds |
| IMP-6 `RecordShortfall` | `StockShortfall(SkuId, LocationId, Quantity, VisitId, LineId)` | `StockShortfall(SkuId, LocationId, Quantity, ReferenceId, ReferenceLineId)` (generalised, CONTRACTS G-2); synchronous; enlists an inventory alert; Inventory consumes no event | Holds (names) |
| IMP-7 reversal | `ReceiveReturnAsync`, `VisitTreatmentReversal`, lands in Quarantine | `ReceiveReturnAsync(StockReturn, ...)` -> Quarantine only (OQ-19); there is **no `VisitTreatmentReversal`** member -- `Visit` is used with the same line id | Holds (name) |
| SHP-1 `ISkuReader` | `Name`, `StockUnit`, `IsActive`, `IsControlledDrug`, `TaxClassId` | **No `ISkuReader`.** `ISkuDirectory` (Inventory's port over `SkuRows`): name, active, **StockUnit always `each`, whole units only** (HO-IMP-001 OQ-19; Shop has no unit column). `TaxClassId` is on `SkuRows` only. **No controlled flag on the SKU anywhere**; the only flag is `FormularyMedicine.IsControlled`, behind HO-PHM-001's `IControlledDrugDirectory.FindBySkuAsync` | Differs -- adapted openly (see decisions) |
| SHP-2 SKU price at an instant | `Money` | `ISkuPriceReader.PriceAtAsync(skuId, at)` -> `Money?` (HO-PHM-001's port over `PriceRows`) | Holds (name) |
| SHP-3 dispensary item search | SKUs with stock at a location | No single query: `ISkuDirectory.SearchAsync` + `IStockLedger.GetDispensaryBatchesAsync` (the "IMP-3 by SKU plus a name search" alternative the clause allows) | Holds |

### Decisions

- **Superseded 2026-10-13 (owner decision C):** a request draws from batches in FEFO order and makes one treatment line per batch used (A10 "exact batch" holds per line); the warning and the shortfall use the whole dispensary. A named non-FEFO batch is drawn first, then FEFO order continues skipping it. The client may send the first line id as an idempotency key (a retry is a 200 replay). Fix-round cases: `TreatmentHandlerTests` split / pinned / whole-dispensary / replay, `TreatmentEndpointTests` `ARequestLargerThanTheFefoBatch_*`, `AcceptShortfall_DeductsEverythingAcrossBatches_*`, `ANamedNonFefoBatch_IsDrawnFirst_*`, `ARetryWithTheSameLineId_*`, `TwoConcurrentReversalsOfOneLine_*`, `TheDispensaryItemList_IsPaged`, `AProcedureQuantityBeyondTheColumn_IsA400`, and I-F8 asserting the exact Dhaka finalised date.
- `StockEntryId` / `ReversalStockEntryId` are not stored: `IStockLedger` returns movement lines, not ledger-entry ids. The trace is the ledger's own reference (`Visit`, visit id, line id).
- Stock is whole units: `AllowsFractionalQuantity` is false for every SKU (OQ-19), so a fractional quantity is refused (`FractionalQuantityNotAllowed`). The column stays `numeric(12,3)` (a future fractional SKU needs no migration).

### Unit cases (Domain `VisitTreatmentTests`, Application `TreatmentHandlerTests`)

| ID | Case | Planned test | Status |
|---|---|---|---|
| U-F1 | FEFO default picks the first issuable batch | `Record_WithNoBatchGiven_UsesTheFefoFirstBatch` | PASS |
| U-F2 | Non-FEFO batch needs a reason; with one it is recorded | `Record_ANonFefoBatch_WithoutAReason_IsRefused`, `Record_ANonFefoBatch_WithAReason_IsRecorded_AndTheReasonIsKept` | PASS |
| U-F3 | Expired batch refused with and without a reason | `Record_AnExpiredBatch_IsRefused_WithAndWithoutAReason` | PASS |
| U-F4 | 5 asked, 3 there, no `AcceptShortfall` | `Record_MoreThanAvailable_WithoutAcceptShortfall_ReportsTheAvailableQuantityAndUnit` | PASS |
| U-F5 | Same with `AcceptShortfall` | `Record_MoreThanAvailable_WithAcceptShortfall_DeductsWhatIsThere_AndRecordsTheRest` | PASS |
| U-F6 | 0 available with `AcceptShortfall` | `Record_NothingAvailable_WithAcceptShortfall_DeductsNothing_AndRecordsTheShortfall` | PASS |
| U-F7 | Controlled SKU | `Record_AControlledSku_IsRefused_BeforeAnyStockIsRead` | PASS |
| U-F8 | Reverse a line with deduction; twice | `Reverse_ALineWithADeduction_ReturnsTheStock_AndMarksTheLine`, `Reverse_Twice_IsRefused_AndReturnsNothingTheSecondTime` | PASS |
| U-F9 | Price snapshots at now | `Record_ABillableService_SnapshotsThePriceAtNow`, `Record_ABillableSku_SnapshotsThePriceAndTaxClassAtNow` | PASS |
| (aggregate) | `RecordTreatment` / `ReverseTreatment` call `EnsureOpen`; 100-line cap; shortfall event | Domain `VisitTreatmentTests` (+ the existing U-C7 reflection test) | PASS |

### Integration cases (`TreatmentEndpointTests`, real HTTP, real Inventory)

| ID | Case | Planned test | Status |
|---|---|---|---|
| I-F1 | Consumable with FEFO | `I_F1_AConsumableWithFefo_DeductsTheFefoBatch_AndRefersToTheLine` | PASS |
| I-F2 | Expired batch | `I_F2_AnExpiredBatch_IsRefused_AndNothingIsWritten` | PASS |
| I-F3 | Over stock, then `acceptShortfall` | `I_F3_OverStock_IsRefusedWithTheAvailableQuantity_ThenRecordedWithAShortfall` | PASS |
| I-F4 | Two concurrent for the last unit | `I_F4_TwoRequestsForTheLastUnit_OneWins_OneGets409` | PASS |
| I-F5 | Reverse | `I_F5_Reversing_WritesTheCompensatingEntry_AndMarksTheLine` | PASS |
| I-F6 | Ledger save forced to fail | `I_F6_AFailedSave_LeavesNeitherTheLineNorTheEntry` | PASS |
| I-F7 | Controlled SKU | `I_F7_AControlledSku_IsRefused` | PASS |
| I-F8 | `GetVisitChargeLinesQuery` after finalisation | `I_F8_ChargeLines_AreTheBillableNonReversedLines_WithTheirSnapshots` | PASS |
| (extra) | Dispensary reads, nurse 403, other-branch 404, visit payload and summary carry the lines, finalised visit refuses | `TreatmentEndpointTests`: `ANonFefoBatch_NeedsAReason_AndTheLedgerEntryIsFlagged`, `AnItemWithNoStockAtAll_CanBeSearched_AndRecordedAsAWholeShortfall`, `OnlyAVetRecordsTreatments_AndReadsTheDispensary`, `AVisitOfAnotherBranch_IsNotFound_ForTreatments`, `TheDispensaryReads_ListStockedItems_AndFefoBatches`, `AFractionalQuantity_IsRefused_AsWholeUnitsOnly`, `AProcedure_TakesNoStock_AndSnapshotsTheServicePrice`, `ABadTreatmentShape_IsA400` | PASS |

### Acceptance rows

| # | Status | Evidence |
|---|---|---|
| A10 | PASS | U-F1..U-F3, I-F1, I-F2, I-F6 |
| A11 | PASS | U-F4..U-F6, I-F3, I-F4 |

### Results (2026-10-13, database `catecommerce_emrf`, migrated to head `AddVisitTreatments`)

- Build: 0 errors; the 6 warnings are other areas (`StaffReturnsController`, `StaffSurgeriesController`, `AnaesthesiaCommandHandlers`, `OrderQueries`) and are on master.
- Domain 1999/1999 (17 new in `VisitTreatmentTests`, 2 new reflection entries in `VisitAggregateTests`); Application 477/477 (32 new in `TreatmentHandlerTests`); Infrastructure 174/174.
- Integration, full suite once at the end: **1624 pass, 1 fail, 2 skipped (1627)**. The failure is `WaitlistFlowTests.IG3_UnclaimedOffer_Lapses_AndTheNextEntryIsOfferedTheSameSlot` (Waitlist, not touched by this phase; `Offered` expected, `Closed` seen under full-suite load); it **passed when rerun alone**. The 2 skips are `ClinicalRecordGateTests.G5` and `BillingTestGateEndpointTests.XI_15` (both skipped before this phase). After two last edits (the charge-lines `serviceDate`, a constant stock-return note) the 16 new integration cases and `ConsultationConventionTests` were rerun: 22/22.
- `dotnet ef migrations has-pending-model-changes`: no changes.
- Existing tests edited: `VisitAggregateTests` (two reflection entries, U-C7), `ClinicalRulesTests` (`recordTreatment` in the vet allowed actions, U-C11), `ConsultationConventionTests` (U-X3 now covers `AddVisitTreatments` and `clinic.visit_treatment`).

### Fit with HO-BIL-001 §8.13 (Phase J), reported not edited

| BIL assumed (Visit row) | Built | Mismatch |
|---|---|---|
| `VisitFinalisedEvent(VisitId, CustomerId, CatId, BranchId, AppointmentId?, BoardingStayId?)` | `VisitFinalisedEvent(VisitId, AppointmentId, CatId, BranchId, FinalisedBy, FinalisedOn)` (Phase C, merged) | No `CustomerId` (a visit holds none, ADR-117); no `BoardingStayId` (no boarding link exists, OQ-9); `AppointmentId` is not optional. `GetVisitChargeLinesQuery` supplies `CustomerId` (the appointment customer), `ServiceDate` (branch-local date opened) and `FinalisedOn`. A boarding-stay group cannot be derived from EMR today |
| `VisitBillableItemSource` reads treatments, consumables **and diagnostics** with snapshots and tax class ids | Procedures, medicines and consumables only | Diagnostic orders carry no price or tax class (nothing in §8.5 prices them): no diagnostic charge lines exist |
| Items `Service` / `Medicine` / `Product` | Lines are `Procedure` / `Medicine` / `Consumable` | A mapping Billing must own: Procedure -> Service (`ServiceId`), Medicine -> Medicine (`SkuId`), Consumable -> Product (`SkuId`) |
| Group = the stay when `BoardingStayId` is set | not available | See the first row |
| (not stated) the query scope | finalised visits only, not branch-scoped; null otherwise | Billing must treat null as "not billable yet" |
| (not stated) idempotency key | the line id is stable and unique, and finalised lines never change | `SourceLineRef` = the line id works |

### Results after the gate fix round (2026-10-13, `catecommerce_emrf`)

- Domain 2000/2000; Application 483/483; Infrastructure 174/174.
- Integration, full suite once: **1631 pass, 1 fail, 2 skipped (1634)**. The failure is `ShopCataloguePerformanceTests.I_E11` (p95 675 ms against 500 under full-suite load, Shop, untouched); it **passed when rerun alone**. `TreatmentEndpointTests` 23/23 (16 original, 7 added: split, accept-shortfall across batches, named batch then FEFO, idempotent retry, concurrent double reverse, paged list, procedure quantity bound; I-F8 now asserts the exact Dhaka finalised date).

---

## Phase F -- TEST gate (2026-10-13, head `e82dd46e`, database `catecommerce_emrf`)

Written **before** any run. Every row starts `NOT RUN`; the status and evidence are filled one row at a time as each was observed. New tests are in
`TreatmentEndpointTests` (rows TG-*), scratch files use the prefix `emrf-test-`. Mutation rows edit one production line, run the named tests, and restore the file with `git checkout`.

### Re-executed cases (the contract rows above, run by the gate)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| RX-1 | U-F1..U-F9 + aggregate (`VisitTreatmentTests`, `TreatmentHandlerTests`, incl. split / pinned / whole-dispensary / replay) | all pass | PASS | Domain 2000/2000 (`VisitTreatmentTests` among them), Application 483/483 (`TreatmentHandlerTests` 38 with the filter run) -- watched |
| RX-2 | I-F1..I-F8 and the 15 extra/fix-round `TreatmentEndpointTests` | all pass | PASS | `TreatmentEndpointTests` 32/32 (26 existing + TG-1..TG-5 + the I-F4 assertion) on `catecommerce_emrf`; ran 3x, all green; and again inside the full suite |

### New TEST-gate cases (thin coverage found)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| TG-1 | `clinic.visit_treatment` of a FINALISED visit: UPDATE a column, DELETE, INSERT a copy, move to an open visit, TRUNCATE (direct SQL) | each `CR001`; the row unchanged. (No Phase F test touched the table's triggers before: the earlier gate's G2 covers vitals/problems only) | PASS | `TG1_TheLinesOfAFinalisedVisit_RefuseUpdateDeleteInsertMoveAndTruncate`: UPDATE x2, DELETE, re-parent, TRUNCATE and INSERT all `CR001`; row unchanged. First attempt used the wrong column name (`id`, SQLSTATE 42703): fixed to `"Id"` in the test, not a product fault |
| TG-2 | `clinic.visit_treatment` of an OPEN visit: DELETE refused, move to another visit refused, in-place UPDATE still allowed | `CR001`, `CR001`, no error | PASS | `TG2_TheLinesOfAnOpenVisit_*`: DELETE `CR001`, move `CR001`, in-place UPDATE succeeds and the value is read back |
| TG-3 | Double reverse that truly overlaps: both requests held at `ReceiveReturnAsync` until both have loaded the line (backlog 60(b)) | one `204`, the other `400`/`409`; one Return entry; 4 in Quarantine; line reversed once | PASS | `TG3_TwoReversalsThatReallyOverlap_ReturnTheStockOnce`: both held at `ReceiveReturnAsync` (30 s gate); one 204, loser 400/409, one Return entry, 4 in Quarantine, 6 in dispensary. With the aggregate's already-reversed check mutated out (M-7) it STILL passed: the xmin/transaction guard alone holds the stock |
| TG-4 | Charge lines and discharge summary of a finalised split read in DRAW order (not GUID order) | order equals the draw order | PASS | `TG4_ChargeLines_ListASplitInDrawOrder` (two splits, 5 lines). First draft FAILED because the fake clock was not advanced between the two requests, so their lines tied on `RecordedOn` and `position` and interleaved by id (see backlog 64); fixed in the test, not a product fault under a real clock |
| TG-5 | ServiceDate, other side of midnight: opened 17:30 UTC, finalised 17:50 UTC (23:50 Dhaka) | the same Dhaka date as the UTC date (not +1) | PASS | `TG5_AVisitFinalisedBeforeDhakaMidnight_*`: 23:50 Dhaka finalised keeps the same date. Fails under mutation M-2b (`+1 day`), as I_F8 fails under M-2 (UTC date) and M-2b |
| TG-6 | I-F4 loser: the 409 carries `InsufficientStock` (the commit-time CHECK path mapped by the filter) | error code `InsufficientStock` | PASS | I_F4 now also asserts the loser's `errorCode` is `InsufficientStock`; I_F4 green 4x |

### Mutation spot-checks (restore every file afterwards)

| ID | Mutation | Tests expected to FAIL | Status | Evidence |
|---|---|---|---|---|
| M-1 | `DispensaryBatchPicker`: reverse the FEFO order of the draw | split / named-first / FEFO cases | PASS | Mutated (continuation `.Reverse()`): Application 5 fail (of 38); integration 8 fail: split, named-first, I-F1, retry, draw-order x3, reverse-second-of-split. File restored |
| M-2 | `GetVisitChargeLinesQueryHandler`: `ServiceDate` from `OpenedOn` instead of `FinalisedOn` (needs the visit's OpenedOn; or the UTC date of `finalisedOn`) | I_F8 and/or TG-5 | PASS | Mutated (`LocalDateOf(finalisedOn, "UTC")`): `I_F8` FAIL, TG-5 passed (it covers the other side of midnight). Also M-2b (`.AddDays(1)`): `I_F8` and TG-5 both FAIL. File restored |
| M-3 | `TreatmentRows.OfVisitAsync`: drop `.ThenBy(t => t.Position)` | the draw-order reads, TG-4 | PASS | Mutated (no `ThenBy(Position)`): `ASplitOfThreeBatches_ReadsBackInDrawOrder`, `ANamedNonFefoBatchFirst_ReadsBackInDrawOrder`, TG-4 FAIL (3 of 32). File restored |
| M-4 | Idempotent replay branch removed (retry deducts again / 5xx) | `ARetryWithTheSameLineId_*` | PASS | Mutated (replay branch dead): 1 Application test FAIL and `ARetryWithTheSameLineId_*` FAIL; `ALineIdUsedOnAnotherVisit` still passes (separate path). File restored |
| M-5 | `UniqueConstraintExceptionFilter`: drop the `PK_visit_treatment` -> 400 mapping | `ALineIdUsedOnAnotherVisit_Is400_NotA500` | PASS | Mutated (key renamed `PK_visit_treatment_x`): `ALineIdUsedOnAnotherVisit_Is400_NotA500` FAIL. File restored |
| M-6 | `acceptShortfall` deducts only the FEFO batch (no continuation across batches) | `AcceptShortfall_DeductsEverythingAcrossBatches_*`, handler tests | PASS | Mutated (`deducted` limited to the first plan line): 1 Application test and `AcceptShortfall_DeductsEverythingAcrossBatches_*` FAIL, nothing else. File restored |
| M-7 | `VisitAggregate.ReverseTreatment`: the already-reversed check removed | `I_F5` (second reversal 400) and the Application reverse test; the two concurrent-reversal tests should STILL pass (xmin guards them) | PASS | `I_F5` FAIL and 1 Application test FAIL; `TG3` and `TwoConcurrentReversalsOfOneLine` stayed green, as predicted. File restored |
| M-8 | Drop / disable `tr_visit_treatment_frozen` in the database and rerun TG-1/TG-2 | TG-1, TG-2 FAIL | BLOCKED | No `psql` on this machine and I did not alter the shared `catecommerce_emrf` schema by hand. TG-1/TG-2 assert the exact SQLSTATE `CR001`, so a missing trigger would give a success or a different state. Unblock: a psql client, or a test-only `ALTER TABLE .. DISABLE TRIGGER` helper |

### Build, model and suites

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| B-1 | `dotnet build CatEcommerce.sln` | 0 errors | PASS | `dotnet build CatEcommerce.sln`: 0 errors; incremental build of the restored tree 0 warnings |
| B-2 | Domain, Application, Infrastructure unit suites | all pass | PASS | Domain 2000/2000, Application 483/483, Infrastructure 174/174 -- watched |
| B-3 | `dotnet ef migrations has-pending-model-changes` | no changes | PASS | `dotnet ef migrations has-pending-model-changes`: "No changes have been made to the model since the last migration." (head `20261013110937_AddVisitTreatments`) |
| B-4 | Full integration suite once on `catecommerce_emrf`; any failure rerun alone | only known flakes, and they pass alone | PASS | Full suite once on `catecommerce_emrf` (CATECOMMERCE_TEST_DB the full string): **1639 pass, 2 fail, 2 skipped (1643)**, 17 m 9 s. Failures: `WaitlistFlowTests.IG3` (known flake) and `DischargeSummaryAndFollowUpEndpointTests.I_G5` (not on the known list); BOTH passed rerun alone, and the whole `DischargeSummaryAndFollowUpEndpointTests` class passed 8/8. Skips: `ClinicalRecordGateTests.G5`, `BillingTestGateEndpointTests.XI_15` (both pre-existing) |

**TEST gate counts (Phase F, 2026-10-13):** 20 rows -- PASS 19 · FAIL 0 · BLOCKED 1 (M-8) · NOT RUN 0. Underneath: 6 new tests/assertions (TG-1..TG-5 plus the I-F4 error-code assertion), 8 mutation spot-checks (7 killed as predicted), suites Domain 2000 · Application 483 · Infrastructure 174 · Integration 1639 pass / 2 load flakes (both pass alone) / 2 pre-existing skips.
