GATE: TEST
HANDOVER: HO-EMR-001 (Phase F, treatments / consumables / stock, US-CLN-15)
COMMITS: b12fd39d..e82dd46e (branch ho/EMR-001-F) plus the gate's test additions
DATE: 2026-10-13
STATUS: PASS

SHEET: docs/test-cases/TC-HO-EMR-001.md, "Phase F -- TEST gate (2026-10-13...)". Written before any run; every row has a status.
COUNTS (gate rows): 20 total -- PASS 19, FAIL 0, BLOCKED 1 (M-8), NOT RUN 0.

ENVIRONMENT
Database `catecommerce_emrf` only (CATECOMMERCE_TEST_DB the full Npgsql string; `catecommerce_dev` / `catecommerce_test` never touched), migrated to head `20261013110937_AddVisitTreatments`. No psql on the machine. The C: drive hit 100% mid-gate (a run log I wrote filled it); I removed three stale run logs from the session scratch folder (surg.log, srg-accept.trx, it_test.trx) to continue. A sweep of `%TEMP%\catecommerce-file-store-tests` (2 GB of old per-run stores) was refused by the permission system and left alone.

OBSERVED RUNS
- Build: 0 errors. Domain 2000/2000, Application 483/483, Infrastructure 174/174.
- `dotnet ef migrations has-pending-model-changes`: no changes.
- `TreatmentEndpointTests`: 32/32, run 3 times plus inside the full suite.
- Full integration suite once: 1639 pass, 2 fail, 2 skipped (1643), 17 m 9 s. `WaitlistFlowTests.IG3` (known flake) and `DischargeSummaryAndFollowUpEndpointTests.I_G5` (not on the list) both PASSED rerun alone; the I_G5 class passed 8/8. Skips are the two pre-existing (`ClinicalRecordGateTests.G5`, `BillingTestGateEndpointTests.XI_15`).

REQUESTED VERIFICATIONS
- Auto-split, FEFO and named-batch-first: `ARequestLargerThanTheFefoBatch_*`, `ANamedNonFefoBatch_IsDrawnFirst_*`; mutation M-1 (continuation order reversed) fails 5 Application + 8 integration tests.
- acceptShortfall across batches: `AcceptShortfall_DeductsEverythingAcrossBatches_*`; M-6 fails exactly that test and its handler twin.
- Idempotent lineId retry; lineId on another visit 400: `ARetryWithTheSameLineId_*` (M-4 kills it), `ALineIdUsedOnAnotherVisit_Is400_NotA500` (M-5 kills it).
- ServiceDate as the Dhaka finalised date: `I_F8` (23:50 opened, 00:10 finalised, expects the next date) and new TG-5 (23:50 finalised, expects the same date); M-2 (UTC) kills I_F8, M-2b (+1 day) kills both.
- I-F4 last-unit race: sound (gated so both read "1 available"); strengthened to assert the loser's code is `InsufficientStock` (TG-6).
- Frozen triggers: no test touched `clinic.visit_treatment` directly before; added TG-1 (finalised: UPDATE, status flip, DELETE, re-parent, TRUNCATE, INSERT all `CR001`) and TG-2 (open: DELETE and move refused, in-place edit allowed). Trigger-drop mutation BLOCKED (M-8).
- Double reverse: the existing test does not truly overlap (backlog 60(b)); added TG-3 which holds both requests at `ReceiveReturnAsync`. One 204, the loser 400/409, one Return entry, stock back once. With the aggregate's own already-reversed check mutated out (M-7) TG-3 still passes, so the database-level guard is independently proven.
- Draw order on reads: `ASplitOfThreeBatches_*`, `ANamedNonFefoBatchFirst_*`, new TG-4 for charge lines; M-3 (drop `ThenBy(Position)`) fails all three.

WEAK TESTS STRENGTHENED
- I_F4 asserted only HTTP 409; now also the error code. `TwoConcurrentReversalsOfOneLine` is not a real overlap; kept, and TG-3 added beside it.

FINDINGS
No BLOCKER or HIGH. Defects in production code: none found.
- [LOW] DischargeSummary I_G5 failed once under full-suite load, passed alone (backlog 63).
- [LOW] `position` ties across two requests with an identical `RecordedOn`; only a frozen test clock can produce it (backlog 64).
- [LOW] No mutation proof for the treatment triggers (backlog 65).

NEW BACKLOG ROWS: 63, 64, 65 (docs/backlog/EMR.md).
Files restored after every mutation (`git status` showed only the sheet and `TreatmentEndpointTests.cs` modified afterwards). Scratch files used the `emrf-test-` prefix.
