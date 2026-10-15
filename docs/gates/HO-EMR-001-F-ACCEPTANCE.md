GATE: ACCEPTANCE
HANDOVER: HO-EMR-001 (Phase F only -- US-CLN-15 treatments, consumables and stock; §18 A10, A11)
COMMITS: b12fd39d..0db86031 (branch ho/EMR-001-F): d6ad74e1 79341a82 57b66215 dd5d9abc cd09c041 37e8c469 (build), 17aad6be ae8e8555 04d8b118 (fix round 1), 01c0f4a1 (fix round 2), e82dd46e (row 62 position fix), 0db86031 (TEST gate)
DATE: 2026-10-13
STATUS: PASS

PRIOR GATES (read from docs/gates/ and docs/test-cases/, not from commit messages)
- ARCHITECTURE: PASS -- HO-EMR-001-F-ARCHITECTURE.md. First pass FAIL (HIGH 1, 37e8c469); combined re-check 1 FAIL (BLOCKER 1, records-only, 04d8b118); combined re-check 2 PASS (01c0f4a1). The re-checks cover SECURITY and CODE_REVIEW for the fix rounds (gate scope rule).
- SECURITY: PASS -- HO-EMR-001-F-SECURITY.md (37e8c469; 3 LOW, now backlog 55-57).
- CODE_REVIEW: PASS -- HO-EMR-001-F-CODE_REVIEW.md (37e8c469; 4 MEDIUM, 2 LOW, now backlog 49-53).
- TEST: PASS -- HO-EMR-001-F-TEST.md and docs/test-cases/TC-HO-EMR-001.md "Phase F -- TEST gate" (head e82dd46e): 20 rows, 19 PASS, 1 BLOCKED (M-8), 0 FAIL, 0 NOT RUN.
- e82dd46e (the `position` column and the regenerated migration, after re-check 2) had no architecture re-check. It was gated by TEST alone (M-3 kills it; TG-4), which is what the gate scope rule allows for a small fix. Accepted.

TESTS RE-RUN BY THIS GATE (targeted, database catecommerce_emrf, CATECOMMERCE_TEST_DB = full Npgsql string; scratch prefix emrf-acc-)
- Application `TreatmentHandlerTests`: 38/38 passed.
- Domain `VisitTreatmentTests|VisitAggregateTests`: 55/55 passed.
- Integration `TreatmentEndpointTests`: 32/32 passed (the filter also matched 4 `ParasiteTreatmentEndpointTests`, all passed; 36 total, 49 s). I_F1..I_F8, TG1..TG5 and all split, replay and order cases are green.

CRITERIA
- A10 (CLN-15 AC1: the exact batch is deducted with a ledger entry in the same commit; an expired batch is refused) -- MET.
  - The FEFO batch is chosen when none is named: U-F1 `Record_WithNoBatchGiven_UsesTheFefoFirstBatch`. It asserts the EARLY batch's id and lot, one `DeductAsync` with an explicit line for that batch, a reference of (Visit, visitId, lineId), and the party as (cat, owner).
  - A non-FEFO batch needs a reason (BR-4): U-F2, two tests. The integration twin `ANonFefoBatch_NeedsAReason_AndTheLedgerEntryIsFlagged` asserts the 400, then a `FefoOverridden` entry for the named batch.
  - An expired batch is refused with no override (BR-3): U-F3 `Record_AnExpiredBatch_IsRefused_WithAndWithoutAReason` (a Theory with a null and a non-null reason: `BatchExpired`, nothing deducted or saved). I-F2 `I_F2_AnExpiredBatch_IsRefused_AndNothingIsWritten` tries with and without a reason, gets 400 `BatchExpired` with the lot, writes no movement and no line, leaves on-hand at 10, and the picker does not offer the batch. `Record_TheFefoChoice_IgnoresABatchInventoryWouldNotUse` covers the unnamed expired batch.
  - The exact batch carries a ledger entry that references the line: I-F1 `I_F1_AConsumableWithFefo_DeductsTheFefoBatch_AndRefersToTheLine`. It asserts a single movement on the EARLY batch, delta -2, kind `ClinicalUse`, reference = visit + line, cat, customer and dispensary location. On-hand drops to 8, and the visit line carries that batch.
  - Same commit: I-F6 `I_F6_AFailedSave_LeavesNeitherTheLineNorTheEntry`. A real trigger forces the ledger insert to fail; the result is a 500 with 0 `clinic.visit_treatment` rows, no movement and on-hand unchanged. In source, `TreatmentHandlers.cs:115-133` enlists `DeductAsync` and `RecordShortfall` and saves once with `UpdateAsync`, and reversal does the same at `:396-408`.
  - Auto-split (owner decision C) keeps "exact batch" per line. `ARequestLargerThanTheFefoBatch_IsSplitAcrossBatches_OneLinePerBatch` asserts lines [early, late], deducted ["3", "2"], one movement per batch with the matching delta, no override flag, and both lines on the visit. `ANamedNonFefoBatch_IsDrawnFirst_ThenTheDrawContinuesInFefoOrder` asserts the named batch -4, then the FEFO batch -2. Mutation M-1 (FEFO order reversed) failed 5 Application and 8 integration tests.
- A11 (CLN-15 AC2: insufficient stock warns; the shortfall can be recorded for reordering) -- MET.
  - The warning: U-F4 `Record_MoreThanAvailable_WithoutAcceptShortfall_ReportsTheAvailableQuantityAndUnit` asserts `InsufficientStock`, an `InsufficientStockDetail` of 3 "each", the message, and nothing deducted, shortfalled or saved. In I-F3 `I_F3_OverStock_*` the first call answers 400 `{ availableQuantity: "3", unit: "each" }` with no movement and no alert.
  - The shortfall is recorded: U-F5 `Record_MoreThanAvailable_WithAcceptShortfall_DeductsWhatIsThere_AndRecordsTheRest` asserts deducted 3, shortfall 2, ledger demand 3, and one `RecordShortfall` (SKU, quantity 2, dispensary, visit, line). It also asserts the call order Deduct then RecordShortfall, one save, and `ClinicalStockShortfallRecordedEvent` (2/3). In I-F3, the second call returns 201 3/2, one movement of -3, on-hand 0, and **one `inventory.stock_alert` of kind Shortfall with `quantity_at_raise` 2** (the reorder input).
  - Nothing is available: U-F6 `Record_NothingAvailable_WithAcceptShortfall_*` asserts deducted 0, no `DeductAsync` call, a shortfall of 2 and the event. The integration twin `AnItemWithNoStockAtAll_CanBeSearched_AndRecordedAsAWholeShortfall` finds the empty ledger and one alert.
  - Whole-dispensary semantics (decision C): `ARequestLargerThanTheFefoBatch_*` warns with "13", the sum of both batches. `AcceptShortfall_DeductsEverythingAcrossBatches_AndRecordsOnlyTheRemainder` asserts 7 deducted across 2 movements and an alert of 3. Mutation M-6 kills it.
  - Last-unit race: I-F4 `I_F4_TwoRequestsForTheLastUnit_OneWins_OneGets409`. Both requests are gated until both have read "1 available". The result is one 201 and one 409 with errorCode `InsufficientStock` (TG-6), on-hand 0, one movement, and exactly one visit holding a line.
- A12 (CLN-16; was PASS "without treatments", and Phase F supplies the missing part). Not a Phase F row, but now evidenced. `I_F8_ChargeLines_*` asserts that the discharge summary lists the 3 non-reversed lines and that `medicinesAdministered` holds the 1 medicine. The reversed line is absent from the charge lines, and the PDF answers 200 `application/pdf`. The TC sheet's A12 and U-G5 rows still read "without treatments" and "n/a until F" (proposed row 67).

RULINGS REQUESTED
1. M-8 BLOCKED (dropping `tr_visit_treatment_frozen` to prove that TG-1/TG-2 would fail): **legitimate, not counted as a pass, and not blocking.**
   - M-8 is a mutation spot-check of a test's strength. It is not a §15/§16 case and not evidence that A10 or A11 cites.
   - The trigger itself is evidenced behaviourally. TG-1 asserts the exact SQLSTATE `CR001` (a custom code that only the clinic triggers raise) for UPDATE x2, DELETE, re-parent, TRUNCATE and INSERT under a finalised visit, with the row unchanged. TG-2 adds a positive control: the in-place UPDATE on an open visit succeeds while DELETE and the move are refused.
   - A dropped trigger therefore cannot pass TG-1. The mutation would add confidence, not coverage.
   - Backlog row 65 (LOW) already tracks the psql / DISABLE TRIGGER helper.
2. Does auto-split change §18 wording? **No §18 edit is required.**
   - "Exact batch deducted with a ledger entry in the same commit" still holds per line: each line keeps its own batch and has its own movement, and the whole request is one `DeductAsync` and one commit.
   - "Insufficient stock warns" holds. The figure is now the whole dispensary's, which §1 Amended (C) and §12.4 already state.
   - The superseded wording sits in §8.6 steps 4, 5 and 7, U-F5 ("ledger -3", which is now the sum across batches) and I-F1 ("one Dispense ledger entry"). The kind is `ClinicalUse`, as §8.6 step 7's `Purpose: ClinicalUse` says, and there is one entry per batch drawn. §1 Amended (C) and (d) override all of these explicitly, so a reader of §18 is not misled.
   - An optional clarifying sentence is proposed as row 66 (LOW). No owner decision is needed.
3. BIL Phase J fit, backlog row 43: **outside this gate, not blocking.**
   - The only Phase F obligation toward Billing is the I-F8 contract: "billable, non-reversed lines with snapshots". I_F8 meets it (2 lines: Medicine 120.00 BDT, SKU, tax class; Procedure with service and tax class; the reversed and non-billable lines are excluded; ServiceDate is the Dhaka finalised date, pinned on both sides of midnight with TG-5; M-2 and M-2b are killed).
   - Row 43 covers the Read-vs-Infrastructure typing, the missing `CustomerId`/`BoardingStayId` on `VisitFinalisedEvent`, the unpriced diagnostics and the kind mapping. All of that is HO-BIL-001 Phase J design, and the row correctly says "decide before BIL Phase J".
   - The TC sheet's BIL-fit table still says ServiceDate is the "branch-local date opened". That has been superseded by row 49 (finalised) and is part of proposed row 67.
4. The 7 out-of-§13 files and the HO-IMP-001 edits: **all recorded.**
   - The changed-file list (54 files, `git diff --stat b12fd39d..0db86031`) contains exactly the seven files that HO-EMR-001 §1 Amended 2026-10-13 (A)(a) authorises by name: `DomainServices/IClinicalSkuReader.cs`, `DomainServices/DispensaryBatchPicker.cs`, `Infrastructure/Consultation/ClinicalSkuReader.cs`, `Migrations/20261013110937_AddVisitTreatments.cs` and `.Designer.cs` (the timestamp matches the file on disk), `Controllers/ClinicalResponses.cs` and `docs/backlog/EMR.md`.
   - Two HO-IMP-001 source files were edited: `IStockLedger.cs` and `EfStockLedger.Consumer.cs`. HO-IMP-001 §1 "Amended (HO-EMR-001 Phase F gate fix)" ratifies both by name, citing 17aad6be, and says no other HO-IMP-001 edit is authorised. CONTRACTS row 34 mirrors it.
   - HO-IMP-001.md has two edits: the Amended row, and the §8.8 listing, whose default body arrived in e82dd46e. HO-IMP-001 §14 allows "Amended rows only", but the ratifying row itself states "The §8.8 listing gains the member", and the listing now matches the row's text (a default member answering `NotSupported`). Accepted as recorded.
   - The edits to `HO-EMR-001.md` and `CONTRACTS.md` (rows 32-34) are §14 "Inputs" and are authorised by owner decision (A), as the Amended row states.
   - The new `PK_visit_treatment` mapping in `UniqueConstraintExceptionFilter.cs` goes beyond §13's "§12.5 mappings only". It is part of the owner-authorised §12.4 amendment ("a `lineId` used on another visit -> `400 TreatmentLineAlreadyRecorded`"), and ARCH re-check 2 counted it in scope.
   - Everything else is inside §13: the Visit/** globs, Entities/TreatmentLine, Models/**, the Read Visit/** folders, ViewModels, Configurations/VisitConfiguration, Migrations/**, Contracts/Visit/**, StaffVisits and StaffDispensary controllers, tests/**, the TC sheet and docs/gates/HO-EMR-001-*. The per-area partials (ConsultationModule, BusinessViolationCodes.Consultation, RecordClassification.Consultation) are counted in scope under ADR-341/C-ALL, as at the first EMR-001 gate.
   - No `web/**`, `.ai/**`, `.claude/**`, `*.csproj`/`*.sln`, HO-SHP-001 file, `billing.*` table or `inventory.*` row written by EMR appears in the changed files.

SCOPE
- out-of-scope work found: none. A scan of new types in the src diff for prescription, formulary, register, label, invoice, payment, billing, surgery and anaesthesia types found none; the only hit was the `ControlledDrugNotSupportedHere` message text. Nothing deducts stock from an event handler (§20.7) and nothing enqueues (§20.8).
- files outside allowed scope: none left unrecorded (the 7 named files and the 2 HO-IMP-001 source files are authorised by recorded decisions; see ruling 4).

§20 BLOCKED CONDITIONS
- §20.2 (SHP-1 differs from §8.12) was hit. The builder adapted instead of stopping, which is what ARCHITECTURE HIGH 1 found. It was then **reported and ratified** by the owner ("Ratify the adapter", "Yes, formulary flag") and recorded in HO-EMR-001 §1 Amended and CONTRACTS rows 32-33 before this gate. It was not silently worked around. The other differences (IMP-4/IMP-7 reference kind, no ledger-entry ids) are recorded in (d).

UNDECLARED DEVIATIONS
- none found. Every deviation I found (the adapter, the formulary flag, auto-split, whole units, the `Visit` reference kind, no stored entry ids, the eighth migration, the `position` column, the idempotent `lineId`, ServiceDate from FinalisedOn, the paged list, the PK mapping and the HO-IMP-001 member) is declared in §1 Amended, §12.4, CONTRACTS, the TC sheet or the backlog.

FINDINGS
- [LOW] docs/handover/HO-EMR-001.md §8.6 steps 4-5 and 7, §15 U-F5, §16 I-F1 -- the wording predates auto-split: one batch per request, "ledger -3" as a single entry, "one Dispense entry", and "the line stores the returned StockMovementLine ids". §1 Amended (C)/(d) overrides it, and §18 A10/A11 need no change. Proposed row 66.
- [LOW] docs/test-cases/TC-HO-EMR-001.md -- stale rows. The "Fit with HO-BIL-001" table gives ServiceDate as "branch-local date opened" (now finalised, row 49). The A12 row says "PASS (without treatments)" and U-G5 says "treatments: n/a until F", although I_F8 now asserts the summary's treatments, the exclusion of the reversed line and `medicinesAdministered`. Proposed row 67.
- [LOW] docs/backlog/EMR.md rows 52 and 60(b) -- both still "open", although TG-6 pins the I-F4 loser's code and TG-3 proves a real overlapping double reverse (row 65 already says 60(b) is closed). Proposed row 68.

PROPOSED docs/backlog/EMR.md ROWS (highest existing is 65; not written -- this gate writes only docs/gates)
| 66 | LOW | After owner decision C (auto-split), HO-EMR-001 §8.6 steps 4-5 and 7, U-F5 ("ledger -3") and I-F1 ("one Dispense ledger entry") still describe one batch per request. §1 Amended (C)/(d) overrides them and §18 is unaffected. Optionally add one sentence to the Amended row: "A10's 'exact batch' is per line; a request writes one `ClinicalUse` movement per batch drawn, in one commit." | ACCEPTANCE F |
| 67 | LOW | TC-HO-EMR-001 stale rows. (a) The BIL-fit table gives ServiceDate as the "branch-local date opened"; it is now the finalised date (row 49). (b) The A12 row ("PASS (without treatments)") and U-G5 ("treatments: n/a until F") should cite `I_F8_ChargeLines_*`, which asserts the summary's treatments, the reversed-line exclusion and `medicinesAdministered`. | ACCEPTANCE F |
| 68 | LOW | Row hygiene at merge: close row 52 (TG-6 pins the I-F4 loser's `InsufficientStock`; TG-3 overlaps two reversals for real) and mark row 60(b) resolved (row 65 says so). | ACCEPTANCE F |

OWNER QUESTIONS
- none blocking. Still open from earlier gates and already in the backlog: the row 43 BIL Phase J design choice (a Domain read port, or BIL reads `clinic.visit_treatment`) before HO-BIL-001 Phase J starts; and the row 55 report of dispensary SKUs with no formulary link (owner-accepted risk).

SCOPE CHECK
- files in allowed scope: 54/54 (42 inside §13 or counted in scope by ADR-341/C-ALL and the gate rulings; 7 authorised by HO-EMR-001 §1 Amended 2026-10-13 (A)(a); 2 HO-IMP-001 source files ratified in HO-IMP-001 §1 Amended (HO-EMR-001 Phase F gate fix); 3 handover docs (HO-EMR-001, CONTRACTS, HO-IMP-001) edited under owner decision (A) and the integrator ratification)
- files outside allowed scope: none unrecorded
