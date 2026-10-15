GATE: TEST
HANDOVER: HO-PHM-001
COMMITS: master...da720334 (branch ho/PHM-001) plus the two test files committed with this report
DATE: 2026-10-10
STATUS: PASS (no BLOCKER/HIGH; 1 MEDIUM to backlog)

Sheet: docs/test-cases/TC-HO-PHM-001.md (129 rows: 127 PASS, 2 FAIL, 0 BLOCKED, 0 NOT RUN). Cases were enumerated before this run; results below were watched.

Suites (DB catecommerce_phm, build 0 warnings / 0 errors):
- Domain 1581/1581, Application 335/335, Infrastructure 161/161.
- Integration 1237: 1230 passed, 2 skipped, 5 failed. Re-run alone: XI_9 (Billing), I_G5_AChangedFollowUp (Discharge) and I_E11 (perf) pass; none is Pharmacy. I_C7 and I_F8 (Pharmacy) fail alone.

Findings
- [MEDIUM] I_C7 and I_F8 are time-zone fragile. Observed at 18:19 UTC (00:19 local, UTC+6): production uses the branch-local date (PharmacyWorkCycle.cs:111) while the tests expect `UtcToday`-derived values ("40 days ago"; 10/12 vs 10/13). Both passed in the earlier UTC-daytime run. Production is correct; the tests need the clinic-local date. Backlog PHM 29. Does not block (medium).

Tests added this gate (kept from the interrupted run, reviewed, executed, passing)
- PharmacyDispensingEndpointTests: WhenTheSecondReturnFailsDuringAReversal_NothingIsSaved_TheDispensingStaysDispensed (FailingLedger extended with failReturnAtCall). Assertions are strong: the injected failure is confirmed by the ReturnRefused error code, and ledger, register and dispensing state are compared with their pre-reverse values.
- PharmacyCatalogueReaderEndpointTests (new, 2 tests): SKU-less product invisible then returned with all SKUs; search `take` limits products not SKU rows.

Weak tests judged: none strengthened. Known gaps unchanged and recorded in the sheet (U-A3 template half, U-R5, U-D4, U-W1/W2, U-G1 are integration-level only; no Application-level handler tests) - accepted by Amended(build) item 10 / CODE_REVIEW medium.

No production bug found.
