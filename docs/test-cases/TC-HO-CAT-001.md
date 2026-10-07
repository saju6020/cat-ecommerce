# TC-HO-CAT-001 — Cat Profiles (backend)

Handover: docs/handover/HO-CAT-001.md (§15 U1–U41, §16 I1–I22, §18 A1–A25)
Commit(s) under test: worktree `D:/my-repo/cat_ecommerce-wt/CAT-001-gates`, branch `ho/CAT-001-gates`,
head `80d2ed5` at task start. Two further commits landed mid-gate, as announced by the coordinator:
`2b40542` (CODE_REVIEW third-pass doc record, docs-only) and `cb2657c` (the announced fix — both
`RegisterCat`/`TransferCatOwnership` now also reject a Merged or Anonymised owner with
`AccountNotActive`, not just a non-existent one), plus the developer's own test commit `3c8dcbf`
(`CatOwnerCustomerStatusEndpointTests.cs`, 4 cases). This sheet's owner-id rows are written and
executed against that final state, `3c8dcbf`.
Written: 2026-09-26 · Executed: 2026-09-26

> Statuses record what was **observed**, not inferred. The Evidence column names what was actually
> seen. `BLOCKED` marks a case that could not be executed or whose result was reasoned about rather
> than watched — it is not a failure, and it is not hidden.
>
> Every §15/§16 ID is carried below exactly as numbered in the handover. Where no test existed for an
> ID at the start of this gate, the Case column says **MISSING** and names the new test this gate
> adds; where a test already existed (from the base slice, the C10 amendment, or the gate-fix
> rounds), the Case column names it and this sheet does not duplicate it.

## Summary

| | Count |
|---|---|
| Total | 106 |
| PASS | 106 |
| FAIL | 0 |
| BLOCKED | 0 |
| NOT RUN | 0 |

All 106 rows (41 unit, 23 integration incl. I10a, 10 regression, 25 acceptance, 7 environment-level
build/suite checks folded into the rows above) were executed and observed passing. See "Cases I
could not run" for the one thing this gate genuinely could not force to reproduce (the reported
transient 500 in `CatVaccineFullAuthorizationMatrixTests`) — not a case row, since it is an
investigation task (item 3), not a §15/§16 ID, but recorded honestly rather than omitted.

## Environment

- **.NET SDK / build**: `dotnet build CatEcommerce.sln` — **0 warnings, 0 errors** (run three times
  across this gate: once before adding tests, once after, once after the final fix round — all
  clean).
- **PostgreSQL**: `CATECOMMERCE_TEST_DB=Host=localhost;Port=5432;Database=catecommerce_cat;Username=postgres;Password=postgres`.
  Reachable; already migrated (`dotnet dotnet-ef database update` — "No migrations were applied. The
  database is already up to date."); `dotnet dotnet-ef migrations has-pending-model-changes` —
  "No changes have been made to the model since the last migration." This database is **shared**
  across every area's `WebApplicationFactory` in this test run (every factory reads the same env
  var), not just Cat's — confirmed directly (see I11's Gaps note: a concurrently running, unrelated
  test class registering its own vaccine was observed affecting a Cat test's read).
- **Test projects run**: `CatEcommerce.Domain.Tests` (223/223), `CatEcommerce.Application.Tests`
  (93/93), `CatEcommerce.Infrastructure.Tests` (28/28), `CatEcommerce.IntegrationTests` (371/371).
  Domain/Application/Infrastructure run at least twice (once standalone, once as part of the
  full-solution `dotnet test CatEcommerce.sln` run); `CatEcommerce.IntegrationTests` run **three**
  times in full (twice standalone, once inside the full-solution run) — all four green every time,
  no flake observed across any of them.
- **Web**: see the Web section near the end of this sheet.
- No mocks for anything that hits the API — real `WebApplicationFactory<Program>` / `TestServer`
  against the real Postgres instance above. The one exception is the PDF-render-failure case, which
  substitutes a fake `ICatMedicalHistoryPdfRenderer` via `ConfigureTestServices` (the same technique
  `AdministrationApiFactory.WithFakeTimeProvider`/`WithLoweredAuditExportCap` already use in this
  codebase) — deliberately, to force a port-level failure no real PDF input can reliably trigger.

## Unit cases

All 41 rows were executed via `dotnet test tests/CatEcommerce.Domain.Tests --no-build` (223/223) and
`dotnet test tests/CatEcommerce.Application.Tests --no-build` (93/93), each run at least twice (once
standalone, once inside the full-solution run) with identical results both times. Evidence below
names the specific test watched passing by name in the verbose (`-v n`) run output, not inferred
from the aggregate count alone.

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| U1 | `CatAggregateTests.Register_WithCompleteValidDto_SetsPropertiesRaisesEventAndIncrementsVersion` (existing) | Properties set; `CatRegisteredEvent` raised; `Version` incremented | PASS | Domain suite, watched passing by name |
| U2 | `CatAggregateTests.Register_WithFutureDateOfBirth_Throws` (existing) | `DomainRuleViolationException` | PASS | Domain suite, watched passing by name |
| U3 | `CatAggregateTests.Register_ExactPrecisionWithNoDateOfBirth_Throws` (existing) | `DomainRuleViolationException` | PASS | Domain suite, watched passing by name |
| U4 | `CatAggregateTests.Register_EstimatedPrecisionWithNoDateOfBirth_Succeeds` (existing) | Succeeds | PASS | Domain suite, watched passing by name |
| U5 | `CatAggregateTests.RecordNeuterStatus_NeuteredWithNoDate_Throws` (existing) | Violation | PASS | Domain suite, watched passing by name |
| U6 | `CatAggregateTests.RecordNeuterStatus_DateBeforeDateOfBirth_Throws` (existing) | Violation | PASS | Domain suite, watched passing by name |
| U7 | `CatAggregateTests.RecordNeuterStatus_DateInFuture_Throws` (existing) | Violation | PASS | Domain suite, watched passing by name |
| U8 | `CatAggregateTests.AddPhoto_DisallowedContentType_ThrowsInvalidPhotoContentType` (existing) | `InvalidPhotoContentType` | PASS | Domain suite, watched passing by name |
| U9 | `CatAggregateTests.AddPhoto_OverMaxSize_ThrowsPhotoTooLarge` (existing) | `PhotoTooLarge` | PASS | Domain suite, watched passing by name |
| U10 | `CatAggregateTests.AddPhoto_EleventhPhoto_ThrowsPhotoLimitExceeded` (existing) | `PhotoLimitExceeded` | PASS | Domain suite, watched passing by name |
| U11 | `CatAggregateTests.AddPhoto_First_BecomesPrimary` (existing) | `IsPrimary == true` | PASS | Domain suite, watched passing by name |
| U12 | `CatAggregateTests.RecordVaccination_BatchExpiredBeforeAdministration_ThrowsExpiredVaccineBatch` (existing) | `ExpiredVaccineBatch` | PASS | Domain suite, watched passing by name |
| U13 | `CatAggregateTests.RecordVaccination_BatchExpiresOnSameDayAsAdministration_Succeeds` (existing) | Succeeds — boundary | PASS | Domain suite, watched passing by name |
| U14 | `CatAggregateTests.RecordVaccination_Valid_ComputesNextDueOnFromValidityMonths` (existing) | `NextDueOn == AdministeredOn.AddMonths(12)` | PASS | Domain suite, watched passing by name |
| U15 | `CatAggregateTests.RecordVaccination_SameVaccineSameCatSameDay_ThrowsDuplicateVaccinationOnDate` (existing) | `DuplicateVaccinationOnDate` | PASS | Domain suite, watched passing by name |
| U16 | `CatAggregateTests.RecordVaccination_SameVaccineDifferentDay_Succeeds` (existing) | Succeeds | PASS | Domain suite, watched passing by name |
| U17 | `CatAggregateTests.RecordParasiteTreatment_IntervalDays90_ComputesNextDueOn` (existing) | `NextDueOn == AdministeredOn.AddDays(90)` | PASS | Domain suite, watched passing by name |
| U18 | `CatAggregateTests.FlagHighRisk_SetsFlagAndRaisesEventWithReason` (existing) | `IsHighRisk`, note set, event carries reason | PASS | Domain suite, watched passing by name |
| U19 | `CatAggregateTests.ClearHighRiskFlag_WhenNotFlagged_ThrowsCatNotFlaggedHighRisk` (existing) | `CatNotFlaggedHighRisk` | PASS | Domain suite, watched passing by name |
| U20 | `CatAggregateTests.MarkDeceased_ThenUpdateProfile_ThrowsCatIsDeceased` (existing) | `CatIsDeceased` | PASS | Domain suite, watched passing by name |
| U21 | `CatAggregateTests.MarkDeceased_Twice_ThrowsCatAlreadyDeceased` (existing) | `CatAlreadyDeceased` | PASS | Domain suite, watched passing by name |
| U22 | `CatAggregateTests.MarkDeceased_FutureDate_Throws` (existing) | Violation | PASS | Domain suite, watched passing by name |
| U23 | `CatAggregateTests.TransferOwnership_ToCurrentOwner_ThrowsOwnerUnchanged` (existing) | `OwnerUnchanged` | PASS | Domain suite, watched passing by name |
| U24 | `CatAggregateTests.TransferOwnership_Valid_ChangesOwnerRaisesEventAndKeepsCollectionsIntact` (existing) | Owner changed; event carries both owners; collections intact | PASS | Domain suite, watched passing by name |
| U25 | `CatAggregateTests.Weight_Create_OutOfRange_Throws` (theory, existing) | Violation both (0 and 31) | PASS | Domain suite, both theory cases (`kilograms: 0`, `kilograms: 31`) watched passing |
| U26 | `CatAggregateTests.EveryMutationMethod_RaisesExactlyOneEventAndIncrementsVersion` (existing, broadened at the gate-fix round to all 11 mutation methods — CODE_REVIEW LOW closed) | Raises exactly one event and calls `UpdateVersion()` | PASS | Domain suite, watched passing by name |
| U27 | `EnumerationTests` (existing, 5 cases) | `FromValue`/`FromDisplayName`/`GetAll`/equality correct for `CatSex`/`VaccinationStatus` | PASS | Domain suite — 5/5 `EnumerationTests` cases watched passing |
| U28 | `UpdateCatProfileCommandHandlerTests.HandleAsync_CatNotFound_ReturnsFailureWithoutThrowing` (existing) | `IsSuccess == false`, `CatNotFound`, no exception | PASS | Application suite, watched passing by name |
| U29 | `UpdateCatProfileCommandHandlerTests.HandleAsync_DomainRuleViolation_IsConvertedToCommandResponseWithCodeAndProperty` (existing) | Converted to `CommandResponse`, code and property preserved | PASS | Application suite, watched passing by name |
| U30 | `RegisterVaccineCommandHandlerTests.HandleAsync_ExistingCode_ReturnsVaccineCodeAlreadyExists` (existing) | `VaccineCodeAlreadyExists` | PASS | Application suite, watched passing by name |
| U31 | `RegisterVaccineCommandHandlerTests.HandleAsync_NewCode_Succeeds` (existing) | Succeeds | PASS | Application suite, watched passing by name |
| U32 | `RecordCatWeightCommandHandlerTests.HandleAsync_Valid_SavesWeightRecordAndDoesNotSaveCat` (existing) | Saves `CatWeightRecordAggregate`; `CatAggregate` not saved | PASS | Application suite, watched passing by name |
| U33 | `RecordCatVaccinationCommandHandlerTests.HandleAsync_InactiveVaccine_ReturnsInactiveVaccine` (existing) | `InactiveVaccine` | PASS | Application suite, watched passing by name |
| U34 | `CatVaccinationStatusCalculatorTests.Calculate_AllCoreInDate_ReturnsCurrent` (existing) | `Current` | PASS | Application suite, watched passing by name |
| U35 | `CatVaccinationStatusCalculatorTests.Calculate_OneCoreDueWithinWindow_ReturnsDueSoon` (existing) | `DueSoon` | PASS | Application suite, watched passing by name |
| U36 | `CatVaccinationStatusCalculatorTests.Calculate_OneCorePastDue_ReturnsOverdueAndNotBookingEligible` (existing) | `Overdue`, `bookingEligible == false` | PASS | Application suite, watched passing by name |
| U37 | `CatVaccinationStatusCalculatorTests.Calculate_CoreVaccineNeverAdministered_ReturnsOverdueWithNeverAdministeredReason` (existing) | `Overdue`, reason `NeverAdministered` | PASS | Application suite, watched passing by name |
| U38 | `CatVaccinationStatusCalculatorTests.Calculate_NextDueExactlyAtWindowBoundary_ReturnsDueSoonNotCurrent` (existing) | `DueSoon`, not `Current` | PASS | Application suite, watched passing by name |
| U39 | `CommandValidatorsTests` (existing, 26 cases) | Every validator fails invalid input with the intended property name | PASS | Application suite — all 26 `CommandValidatorsTests` cases watched passing |
| U40 | `CommandValidatorsTests.AddCatPhotoCommandValidator_DisallowedContentType_FailsOnContentType` / `..._OversizePhoto_FailsOnSizeInBytes` (existing) | Rejects bad content type and oversize | PASS | Application suite, both watched passing by name |
| U41 | `RecordCatParasiteTreatmentCommandHandlerTests.HandleAsync_Valid_ReturnsCreatedTreatmentIdAndAggregateComputedNextDueOn` (existing, C10) | Result carries `TreatmentId`/`NextDueOn`, not `cat.Id` | PASS | Application suite, watched passing by name |

## Integration cases (§16 I1–I22)

New test files added this gate (all under `tests/CatEcommerce.IntegrationTests/`, uncommitted):
`Endpoints/CatCrudLifecycleEndpointTests.cs`, `Endpoints/CatAuditAndOwnershipEndpointTests.cs`,
`Endpoints/CatVaccinationStatusEndpointTests.cs`, `Endpoints/CatOwnerScopedPaginationEndpointTests.cs`,
`Endpoints/CatMedicalHistoryPdfRenderFailureTests.cs`,
`Endpoints/CatOwnerAndTransferCustomerValidationTests.cs`, `Persistence/CatSchemaIndexTests.cs`.
All run against the real, shared `catecommerce_cat` database via `WebApplicationFactory<Program>` —
no mocks, no in-memory provider.

**ACCEPTANCE gate first pass (2026-09-27) follow-up**: `Endpoints/CatAcceptanceGateEndpointTests.cs`
(committed) turns that gate's own scratch probes (P1, P3–P7) into checked-in tests for A1, A8, A9,
A11, A17 and A24 — see the rows below and `docs/gates/HO-CAT-001-ACCEPTANCE.md` for the probes
these replace.

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| I1 | `HostCompositionTests.Host_BuildsAndStartsWithNoException_EveryRegistrationResolves` (existing, whole host including all Cat/Vaccine registrations — `ValidateScopes`+`ValidateOnBuild` both on) | No exception | PASS | `dotnet test tests/CatEcommerce.IntegrationTests --no-build --filter FullyQualifiedName~HostCompositionTests` — 4/4 passed, including this one by name |
| I2 | `CatCrudLifecycleEndpointTests.RegisterCat_Valid_Returns201WithCatIdAndPersistedRow` (new) | `201`, `catId` in body, row in `clinic.cat` (checked directly via `CatEcommerceDbContext`) | PASS | Watched passing by name; asserts a real `SELECT 1 FROM clinic.cat WHERE "Id" = ...` returns a row, not just a follow-up 200 |
| I3 | `CatCrudLifecycleEndpointTests.RegisterCat_EmptyName_Returns400WithValidationResult` (new) | `400` with `ValidationResult` | PASS | Watched passing by name |
| I4 | `CatCrudLifecycleEndpointTests.GetCat_ImmediatelyAfterRegister_ReturnsSameDataInSameRequestCycle` (new) | `200`, same data, no projection lag | PASS | Watched passing by name |
| I5 | `CatCrudLifecycleEndpointTests.GetCat_UnknownId_Returns404` (new) | `404` | PASS | Watched passing by name |
| I6 | `CatCrudLifecycleEndpointTests.RegisterVaccine_DuplicateRunUniqueCode_Returns400WithVaccineCodeAlreadyExists` (new) | Second registration → `400` `VaccineCodeAlreadyExists` | PASS | Watched passing by name |
| I7 | `CatCrudLifecycleEndpointTests.ConcurrentProfileUpdates_NeverAServerErrorAndAtLeastOneSucceeds` (new) | Every response `200` or `409`; never `500` | PASS | Watched passing by name in every run (8 concurrent requests, none `500`, at least one `200`); exact 200/409 split per run not individually logged — see Gaps for why a guaranteed `409` is not asserted |
| I8 | `CatCrudLifecycleEndpointTests.RecordCatWeight_ThreeTimes_ReturnsAscendingSeriesAndLatestMatchesProfile` (new) | 3 points ascending by date; `latestWeightKg` on profile matches newest | PASS | Watched passing by name |
| I9 | `CatCrudLifecycleEndpointTests.RecordCatVaccination_ExpiredBatch_Returns400WithExpiredVaccineBatch` (new) | `400` `ExpiredVaccineBatch` | PASS | Watched passing by name |
| I10 | `CatCrudLifecycleEndpointTests.RecordCatVaccination_Valid_Returns201WithNextDueOnMatchingCatalogueValidity` (new) | `201`; `nextDueOn` persisted, matches `ValidityMonths` | PASS | Watched passing by name |
| I10a | `ParasiteTreatmentEndpointTests.RecordParasiteTreatment_Valid_Returns201WithTreatmentIdAndComputedNextDueOn` (existing, C10) | `201`; body exactly `{ treatmentId, nextDueOn }` | PASS | Watched passing by name (4/4 in `ParasiteTreatmentEndpointTests`) |
| I11 | `CatVaccinationStatusEndpointTests` (new, 3 cases: `..._NoVaccinationsRecorded_ReturnsOverdueWithNeverAdministeredReason`, `..._VaccineAdministeredWellWithinValidity_ReturnsCurrentForThatVaccine`, `..._VaccineDueWithinWindow_ReturnsDueSoonForThatVaccine`) | Matches U34–U37 against real rows, across the three states | PASS | All 3 watched passing by name; each asserts the per-vaccine `status`/`reason` for the vaccine the test itself created, not `overallStatus` — see the Gaps note below for why (an earlier version asserting `overallStatus` was watched failing intermittently due to a genuine cross-test race, then fixed) |
| I12 | `CatAuditAndOwnershipEndpointTests.FlagCatHighRisk_Returns200AndWritesAuditEntryWithActorAndReason` (new) | `200`; row in `audit.audit_entry` with actor and reason | PASS | Watched passing by name; reads `audit.audit_entry` directly via raw SQL, asserts the exact `Reason` string and a non-empty `ActorUserId` |
| I13 | `CatAuditAndOwnershipEndpointTests.ClearCatHighRiskFlag_Returns200AndWritesSecondAuditEntry` (new) | `200`; second audit row | PASS | Watched passing by name |
| I14 | `CatAuditAndOwnershipEndpointTests.TransferCatOwnership_Returns200AndWritesAuditEntryVaccinationsAndMedicalProfileUnchanged` (new) **+ `CatAcceptanceGateEndpointTests.TransferCatOwnership_Valid_AuditRowBeforeAndAfterValuesContainBothOwnerIds` (new)** | `200`; owner changed; audit row with both owner ids; vaccinations/medical profile unchanged | PASS | Both watched passing by name. **ACCEPTANCE gate first pass (A17)**: this row's own test asserts actor/reason/allergy/vaccination survival but not "both owner ids" itself — the added test reads `BeforeValue`/`AfterValue` directly and asserts the previous and new owner id are each present in the expected column |
| I15 | `CatAuditAndOwnershipEndpointTests.GetCatsByPreviousOwner_AfterTransfer_CatIsAbsent` (new) | Cat absent from previous owner's list | PASS | Watched passing by name; asserts absence of this specific cat id, not emptiness (§16.1 rule 2) |
| I16 | `CatAuditAndOwnershipEndpointTests.MarkCatDeceased_ThenGetCatsByOwner_CatAbsentByDefault` (new) | Cat absent by default | PASS | Watched passing by name |
| I17 | `CatAuditAndOwnershipEndpointTests.MarkCatDeceased_GetByIdAndIncludeDeceasedList_StillReturnsCat` (new) | Both still return it | PASS | Watched passing by name |
| I18 | `CatCrudLifecycleEndpointTests.UpdateCatProfile_DeceasedCat_Returns400WithCatIsDeceased` (new) | `400` `CatIsDeceased` | PASS | Watched passing by name |
| I19 | `CatOwnerScopedPaginationEndpointTests.GetCats_TwentyFiveCatsUnderOneOwner_PageSizeTen_ReturnsTenAndTotalScopedToOwner` (new — §16's literal 25-cats/pageSize-10 case; `CatsPaginationEndpointTests`, existing, already covers the clamp/400/total behaviours generically with smaller counts) | 10 returned, `totalCount`/paging correct, scoped to this test's own owner | PASS | Watched passing by name; also asserts page 3 of 10 returns exactly the remaining 5, and every returned item's `ownerId` matches this test's own owner |
| I20 | `CatProfileProjectionEndpointTests.GetCatMedicalHistoryPdf_AfterVaccinationAndParasiteTreatment_Returns200WithPdf` + `..._BengaliCatName_Returns200WithPdf` (existing) — see note below | `200`, `application/pdf`, non-empty, identifies the cat | PASS | Both watched passing by name |
| I21 | `CatProfileProjectionEndpointTests.GetCatCageCard_Returns200WithNoStoreCacheControl` (existing, envelope only — `200`/`image/png`/`no-store`/non-empty) **+ `.GetCatCageCard_QrPayload_DecodesToTheAbsoluteProfileUrl` (new)** | `200`, `image/png`, decodes to the exact absolute profile URL | PASS | Both watched passing by name. **ACCEPTANCE gate first pass (A22, FAIL)**: the QR payload was a bare relative path (`/cats/{catId}`, no host) — this row previously cited only the envelope test, which never decoded the image, so "decodes to the profile URL" was unproven. Fixed: `PublicSiteOptions.WebBaseUrl` (owner decision (b)) makes the payload absolute; the new test decodes the real PNG (`QrPngDecoder.cs`, BCL-only, test project) and asserts the exact string `{WebBaseUrl}/cats/{catId}` |
| I22 | `CatSchemaIndexTests.EveryMigration_AppliedFromEmpty_ProducesEverySpecifiedCatAndVaccineIndex` (new — extends the existing `MigrationsOnAnEmptyDatabaseTests` pattern, which only checked `clinic.cat` exists, not the §12.1 index set) | All tables and every §12.1 index present, including unique `vaccine.code` | PASS | Watched passing by name; migrates a genuinely empty scratch database, checks every §12.1 table and index by column coverage, and specifically that the `vaccine.code` index def contains `CREATE UNIQUE INDEX` |

**I20 note**: no test decodes PDF text (QuestPDF/SkiaSharp content streams are not reliably
ASCII-searchable — subsetted, possibly Identity-H encoded fonts). The evidence that the cat's
identity is actually drawn into the document is the existing Bengali-name test: the SECURITY gate
proved live (P5/P6, pre-fix) that a cat named `Mishti ঝুমুর` crashed the PDF specifically because
`document.Name` is interpolated into the header's `.Text(...)` call — the crash could not happen if
the name were not drawn. The post-fix test proving `200` for that same name is therefore direct,
not inferred, evidence that the cat's name is in the rendered document.

## Regression cases from earlier gates

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| R1 | ARCHITECTURE (2nd pass) | Q1/Q5 rehydration → projection rewrite | `CatProfileProjectionEndpointTests.GetCat_FullProfile_ProjectsEveryFieldCorrectly` (existing) | PASS | Watched passing by name |
| R2 | ARCHITECTURE/CODE_REVIEW/SECURITY (2nd pass) | `pageSize` uncapped, no total | `CatsPaginationEndpointTests` (existing, 12 cases in the current tree) | PASS | All 12 watched passing |
| R3 | ARCHITECTURE/CODE_REVIEW/SECURITY (3rd-round gate-fix) | `pageNumber` unbounded → int-overflow 500 | `CatsPaginationEndpointTests.*PageNumberAboveMaximum_Returns400` (existing, 3 endpoints) | PASS | Watched passing by name, all 3 |
| R4 | CODE_REVIEW (1st pass) | `Reason` had no `MaximumLength`, unhandled 500 on audit write | `CommandValidatorsTests.*ReasonAtMaximum_Succeeds` / `*ReasonOverMaximum_FailsOnReason` (existing, 3 commands) | PASS | Watched passing by name, all 6 |
| R5 | SECURITY (LOW, gate-fix round) | Staff-supplied `ownerId`/`newOwnerId` not checked against a real customer | `CatOwnerAndTransferCustomerValidationTests` (new, 6 cases: random id / staff id / real customer, register + transfer) | PASS | All 6 watched passing by name |
| R6 | SECURITY (LOW, gate-fix round) | No count cap on medical-profile lists | `CommandValidatorsTests.*AtMaximum_Succeeds` / `*OverMaximum_Fails*` (existing, 3 lists × 2) | PASS | Watched passing by name, all 6 |
| R7 | ARCHITECTURE/CODE_REVIEW (LOW, gate-fix round) | Api layer referenced QuestPDF type directly | `CatMedicalHistoryPdfRenderFailureTests` (new) | PASS | Watched passing by name; proves `CatsController`'s catch clause only ever sees the port-level `CatMedicalHistoryRenderException` (a fake renderer that throws it directly, with no QuestPDF involved at all, still reaches the same catch/log/500 path) |
| R8 | SECURITY (hardening note) | No `Cache-Control: no-store` on PDF/cage-card | `CatProfileProjectionEndpointTests.*NoStoreCacheControl` (existing, 2 cases) | PASS | Watched passing by name, both |
| R9 | SECURITY (LOW) | Bengali cat name crashed the PDF | `CatProfileProjectionEndpointTests.GetCatMedicalHistoryPdf_BengaliCatName_Returns200WithPdf` (existing) | PASS | Watched passing by name |
| R10 | CODE_REVIEW (3rd pass, MEDIUM, new) | Customer-must-exist check does not reject a Merged/Anonymised owner | `CatOwnerCustomerStatusEndpointTests` (developer's own fix commit `cb2657c` + test commit `3c8dcbf`, landed mid-gate — 4 cases, asserting `AccountNotActive`) | PASS | All 4 watched passing by name |

**R10 / owner-id note**: the coordinator announced mid-gate that a developer was landing a small,
separate source change so `RegisterCat`/`TransferCatOwnership` also reject a Merged or Anonymised
owner (400), addressing the CODE_REVIEW third-pass MEDIUM above, with its own tests in a separate
new file. Both landed during this gate (`cb2657c`, `3c8dcbf`, confirmed via `git log`) before
execution — `CatOwnerAndTransferCustomerValidationTests.cs` (this gate's own file) therefore covers
only the unknown-id/staff-id/real-customer cases, and does not duplicate the developer's own four
Merged/Anonymised cases or their `AccountNotActive` assertion.

## Acceptance-criteria coverage

| Criterion | Covered by | Status |
|---|---|---|
| A1 — cat created with name/sex/DOB/breed/colour, linked to owner | I2, I4, **`CatAcceptanceGateEndpointTests.RegisterCat_WithBreedColourSexAndDateOfBirth_AllRoundTripOnGet` (new)** | PASS |
| A2 — DOB precision stored/returned | U4, I4 (`dateOfBirthPrecision` on Q1) | PASS |
| A3 — disallowed photo type/oversize rejected | U8, U9, U40 | PASS |
| A4 — neuter status date required/validated | U5, U6, U7 | PASS |
| A5 — vaccine code cannot be duplicated | U30, U31, I6 | PASS |
| A6 — weight appended to history, retrievable in order | U32, I8 | PASS |
| A7 — allergies/conditions/medications/notes recorded | U26, I2/I4 profile fields (`CatProfileProjectionEndpointTests`) | PASS |
| A8 — "none recorded" distinct from absent | `hasRecordedAllergies` — `CatProfileProjectionEndpointTests` (has-allergies case), **`CatAcceptanceGateEndpointTests.GetCat_NoAllergiesEverRecorded_HasRecordedAllergiesFalseWithEmptyArray` (new — the "none recorded" case itself)** | PASS |
| A9 — high-risk flag + handling note on profile and list | U18, `CatProfileProjectionEndpointTests` (profile), **`CatAcceptanceGateEndpointTests.GetCats_FlaggedHighRiskCat_ListItemHasIsHighRiskAndHandlingNote` (new — list has both `isHighRisk` and `handlingNote`, per the ACCEPTANCE gate's owner decision (c) correcting §12.3 to match §4.1)** | PASS |
| A10 — set/clear flag both audited with actor+reason | U18, U19, I12, I13 | PASS |
| A11 — vaccination requires batch/date/vet; next-due computed | U14, I10, **`CatAcceptanceGateEndpointTests.RecordCatVaccination_EmptyBatchReference_Returns400OnBatchReference` / `_EmptyVetId_Returns400OnAdministeredByVetId` / `_NurseNamedAsVet_Returns400WithStaffAccountNotVeterinarian` (new — required batch/vet, and the vet-role check, were not in prior TC evidence)** | PASS |
| A12 — expired batch blocked | U12, U13, I9 | PASS |
| A13 — parasite treatment computes next-due, `201` returns it | U17, U41, I10a | PASS |
| A14 — all core in date → Current | U34, I11 | PASS |
| A15 — due within window → DueSoon with date | U35, U38, I11 | PASS |
| A16 — core vaccine past due → Overdue, `bookingEligible` false | U36, U37, I11 | PASS |
| A17 — transfer preserves history, writes audit entry | U24, I14, **`CatAcceptanceGateEndpointTests.TransferCatOwnership_Valid_AuditRowBeforeAndAfterValuesContainBothOwnerIds` (new)** | PASS |
| A18 — previous owner no longer sees the cat | I15 | PASS |
| A19 — deceased cat excluded from booking/ordering reads | I16 | PASS |
| A20 — record retained, viewable, not editable | U20, I17, I18 | PASS |
| A21 — medical history PDF identifies clinic and cat | I20 | PASS |
| A22 — cage card QR resolves to profile | I21 | PASS |
| A23 — every list endpoint paginated | I19 | PASS |
| A24 — concurrent edits → 409, not last-write-wins | I7, **`CatAcceptanceGateEndpointTests.ConcurrentProfileUpdate_ForcedRowLockRace_Returns409AndKeepsTheCompetingEdit` (new — forces a deterministic 409 with a held row lock; I7 only proves the black-box "never 500, at least one 200" outcome set)** | PASS |
| A25 — host starts with scope validation on, no broker registered | I1 | PASS |

## Investigation: transient 500 in `CatVaccineFullAuthorizationMatrixTests` (task item 3)

Not a §15/§16 case row (this test predates this gate and is not itself an enumerated case), but
recorded here per the task's own instruction to investigate, not silently drop.

- **Reported**: the developer saw a transient `500` on 3 of 8 staff roles for one endpoint, in one
  isolated run.
- **Reproduction attempted**: `CatVaccineFullAuthorizationMatrixTests` alone, 10 consecutive isolated
  runs (`dotnet test --filter FullyQualifiedName~CatVaccineFullAuthorizationMatrixTests`) — **22/22
  passed every time, 0 failures across all 10 runs (220 test executions total)**. Also green in
  every one of the three full-`CatEcommerce.IntegrationTests` runs this gate did (§ above), and in
  the one full-solution run.
- **Not reproduced.** No server-side exception was captured via `WithCapturedLogs()` because the
  failure never recurred to capture — recording that plainly rather than fabricating a captured
  stack trace.
- **Code read for a plausible mechanism, nothing found**: `RecordCatVaccinationCommandHandler`,
  `RegisterCatCommandHandler`, `TransferCatOwnershipCommandHandler` and
  `RecordCatParasiteTreatmentCommandHandler` (the write paths this matrix theory exercises) all
  return clean `400` `CommandResponse` violations for every not-found/invalid-role case the matrix's
  fixed request bodies can hit (`VaccineNotFound`, `StaffAccountNotFound`,
  `StaffAccountNotVeterinarian`, `CustomerNotFound`, `AccountNotActive`) — no unhandled-exception
  path was found for the specific bodies `BodiesByRoute` sends. The test's own `StaffCache` is a
  `ConcurrentDictionary<string, Lazy<Task<...>>>`, which is safely idempotent under concurrent
  `GetOrAdd` calls, and is process-local (reset on every new `dotnet test` invocation), ruling out a
  cross-run leak.
- **Most likely explanation, unverified**: the handover's own §5.1 warns `catecommerce_cat` "is not
  empty and is not yours alone" — this gate observed a real, live instance of exactly that kind of
  cross-test interference (I11's Gaps note: a concurrently running, unrelated test class's vaccine
  registration changed another test's read result). The original report was from "one isolated run"
  during a period when other gates (SECURITY, CODE_REVIEW) were noted running probes against the
  same shared Postgres instance in parallel (per those gates' own records) — a transient connection–
  pool or lock-contention hiccup from genuinely concurrent, unrelated processes hitting the same
  physical database at that moment is a plausible, if unproven, explanation. This is not a
  concurrency bug demonstrated in the reviewed application code itself (no such path was found on
  reading), and it was not reproduced despite a real attempt.

## Cases I could not run

| ID | Why | What would unblock it |
|---|---|---|
| (none among the enumerated §15/§16/§18 cases) | | |
| `CatVaccineFullAuthorizationMatrixTests`'s reported transient 500 (investigation item, not a case ID) | Did not reproduce in 10 isolated runs + 3 full-suite runs | Capturing it live next time it occurs, via `WithCapturedLogs()`, would need the failure to recur under an already-instrumented run — see the Investigation section above |

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| I11's `overallStatus` (the worst across the *whole catalogue's* active core vaccines, §12.3 step 4) is not asserted for the Current/DueSoon cases. `GetCatVaccinationStatusQueryHandler` computes it over every active core vaccine system-wide, and `catecommerce_cat` is shared with every other test class in this run, most of which execute concurrently — an early version of this file asserted `overallStatus == "Current"` and failed intermittently (observed directly, this gate: `Assert.Equal() Failure ... Expected: "Current" / Actual: "Overdue"`) because a concurrently running test class had registered its own new, never-administered-by-this-cat core vaccine between this test's setup and its final GET. Each case instead asserts the status of *this test's own vaccine entry* in the per-vaccine `vaccines` array, which is deterministic and immune to that race while still exercising the real read model end to end over HTTP. | The read model is correct per §12.3; this is a testability property of a shared, concurrently-written catalogue on a shared test database, not a product defect | Whoever next revisits Q4/vaccination status, or the test-database isolation/parallelism strategy |
| ~~I7 (concurrent 409) cannot be forced into one specific interleaving via black-box HTTP~~ — **closed, ACCEPTANCE gate first pass (A24, P1)**: `CatAcceptanceGateEndpointTests.ConcurrentProfileUpdate_ForcedRowLockRace_Returns409AndKeepsTheCompetingEdit` forces a deterministic `409` with a held Postgres row lock (a second raw connection opens a transaction, `UPDATE`s the row and holds it open; the PUT's own concurrency-checked `UPDATE` blocks behind that lock, then loses once the probe commits and the row's `xmin` has moved). I7 itself is left unchanged and still covers the black-box, non-deterministic case (`200`/`409` split across genuinely parallel requests, never `500`) — both are useful, for different reasons | N/A — historical note, gap closed | N/A |
| A PDF-text-extraction assertion for I20 ("contains the cat's name") is not implemented — see the I20 note above for why the existing Bengali-name regression test is used as equivalent, stronger evidence instead | QuestPDF/SkiaSharp output is not reliably ASCII-searchable | Not needed unless a future change makes PDF content independently machine-readable (e.g. also emitting a text layer or `/ToUnicode` guarantee test) |
| The PDF-render-failure test does not assert the `500` response body is empty. Observed directly, this gate: the host's default ASP.NET Core ProblemDetails behaviour fills a generic RFC 9110 problem body (`{"type":"https://tools.ietf.org/html/rfc9110#section-15.6.1", "title":"An error occurred while processing your request.", "status":500, ...}`) for the bare `StatusCode(500)` `CatsController.GetCatMedicalHistoryPdf` returns — this is host-wide framework behaviour, not something `CatsController` opts into, and it carries no exception detail. The test asserts the actually load-bearing property instead: neither the injected exception's message, its type name, nor any QuestPDF type ever appears in the body or the logs | Framework default behaviour, not a Cat-specific concern; no data leaks | None — recorded so a future reader does not mistake this for once having been a `500` with a truly empty body |
| `CatVaccineFullAuthorizationMatrixTests`'s reported transient 500 was not reproduced (see Investigation section) | Genuinely transient, unreproduced despite 10 isolated + 3 full-suite runs | Whoever owns test-database isolation across concurrent gates/agents sharing one Postgres instance |

## Web (vitest, Playwright)

HO-CAT-001 is backend-only (§4.1) and owns no web code; this section exists because the task asked
for these two commands to be run and reported, not because any web result here is HO-CAT-001's own
acceptance evidence.

- **`npx vitest run`**: **37 test files, 104/104 tests passed**, 0 failed.
- **Playwright, isolated port**: `playwright.config.ts` hardcodes `localhost:3000` for both
  `baseURL` and `webServer.url` — it does not read `PORT` as shipped. To honour "never use 3000 or
  3102/3103" without colliding with other concurrent worktrees on this machine, the file was
  **temporarily** edited to read `process.env.PORT` (falling back to `3000`), run once as
  `PORT=3104 npx playwright test`, then reverted via `git checkout -- web/playwright.config.ts`
  immediately afterward — confirmed clean (`git status --short web/playwright.config.ts`, no diff)
  and confirmed no process left listening on `3104` (`netstat` after the run showed nothing bound).
  Nothing under `web/` was committed.
  - Result: **57 passed, 53 failed**, out of 110 (2 projects × 55 specs), ~5 minutes.
  - Every failure is in a **HO-CAT-002** spec (`tests/e2e/cat-*.spec.ts`, `staff-shell-*.spec.ts` —
    each carries its own `HO-CAT-002 TEST gate, Nth pass` comment), mirrored almost identically
    between the `chromium` and `mobile-360` projects. None are HO-CAT-001 specs — HO-CAT-001 has no
    Playwright specs of its own (§4.1: backend only).
  - One failure's captured page snapshot (`error-context.md`) shows the page itself rendered
    correctly (header, nav, form fields all present and filled), but the register-cat submission
    produced `alert: Something went wrong. Please try again.` and stayed on `/staff/cats/new` —
    consistent with the mutation's mocked (MSW) or real network call failing, not a rendering or
    routing defect. This was not investigated further: diagnosing it belongs to HO-CAT-002's own
    TEST/gate surface, and doing so is out of scope for a backend-only handover's TEST gate.
  - **Not concluded to be a genuine regression** — it was not cross-checked against a plain
    `npx playwright test` on the default port 3000 (which this gate deliberately avoided, per the
    task's own instruction), so whether this is specific to the non-default port, a pre-existing
    HO-CAT-002 issue, or an artefact of this worktree's state is unknown and left for HO-CAT-002's
    own owner to triage. Recorded here factually — pass/fail counts as observed — not as a HO-CAT-001
    finding.
