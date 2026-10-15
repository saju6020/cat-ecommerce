GATE: TEST
HANDOVER: HO-RPT-001
COMMITS: ec3b4662 (code under test, unchanged); TEST work: e4f2d088 (cases first) and the commit holding this file
DATE: 2026-10-13
STATUS: PASS

No BLOCKER or HIGH findings. One MEDIUM and one LOW are new rows in docs/backlog/RPT.md (58, 59, prefixed "TEST:"). Production code was not changed.
Case sheet: docs/test-cases/TC-HO-RPT-001.md (section "TEST gate (2026-10-13)" and the run record at the end).

COUNTS
- Earlier rows: 95 PASS (re-observed: every named test passed in this run's trx), 11 DEFERRED with recorded reasons (BRD, SUB, XCT unmerged; BIL Phase J and EMR Phase F items listed as not delivered), 0 NOT RUN.
- TEST-gate cases T-U1..T-U6 and T-I1..T-I11: 17 total, 16 PASS, 1 FAIL (T-I3, shape "throws"), 0 BLOCKED, 0 NOT RUN.
- Regression rows R-1..R-14: 13 PASS, 1 FAIL (R-2, the same T-I3 case).
- Mutation spot-checks M-1..M-14 (plus M-2b, M-4b, M-6b, M-7b): all killed; every file restored.

EVIDENCE (observed, DB catecommerce_rpt only)
- Build 0 errors; has-pending-model-changes: none.
- Domain.Tests 2070, Application.Tests 503, Infrastructure.Tests 174: all passed.
- Full IntegrationTests once: 1719 passed, 3 failed, 2 skipped (of 1724), 32 m 29 s. Failures: T_I3 (throws) = RPT defect; XI_9 = known non-RPT false positive (backlog 38), not counted; Waitlist IG3 = load flake, class re-run alone 8/8 passed.
- Before the full run no testhost process was running.

FINDINGS
- [MEDIUM] src/Api/CatEcommerce.Api/Background/ScheduledReportRunner.cs (StoreFileAsync) -- if HO-FIL-001's register command throws, the object already put in the file store is left with no register row, so no retention sweep reaps it. It holds report data (R4 contacts, R3 clinical text). The returns-a-failure path removes it correctly. Proved by T_I3 (throws): blob count 9, expected 8. The test is left failing on purpose. Backlog 58.
- [LOW] src/Api/CatEcommerce.Api/RateLimiting/ReportRateLimitPolicies.cs (run-now chain) -- the fixed window counts refused attempts (one in-flight refusal or a host-wide refusal burns a permit): 1 run + 1 refused leaves 3 more, so 4 runs a window. Backlog 59.

CHECKED AND HELD (each with a case that fails when the fix is removed)
- Projected schedule reads (T-I1; M-7, M-7b). C5 store-then-record: a save that fails for any reason except the duplicate slot, including a unique violation on a different constraint, gives a Failed run, no requests, no register row, no orphan blob (T-I2; M-2, M-3); a throw before the save (M-2b); register returning a failure (T-I3).
- R3 clinical access on view, CSV, PDF, scheduled runs and run-now, failing closed whether the write throws or is refused (T-I4, T-I5, T-I11 and the existing R3 tests; M-4, M-4b, M-5). Accountant R10 scheduling (I_G6). Per-account limiter incl. a real 11.5 s idle gap and another account unaffected (T-I6, ReportLimiterTests; M-1). Recipient branch and cross-branch re-check at send time (T-I7 and the existing test; M-6, M-6b, M-14). Merge chain (I_F7; M-8 by swapping the view to one hop). Boarding absent, not 0 (M-9). ReportingTestDatabase guard and its two call sites (M-10, M-11).
- ARCH re-check items: the non-duplicate C5 failure path (above) and no duplicate rv_batch rows, including after two re-allocations and database-wide (T-I8).
- Report maths on hand-built fixtures worked out before running (credit note netting, margin on costed lines only, coverage, null not zero, payments per method; T-U3, T-U4; M-12) and Asia/Dhaka boundaries at the tick level and on real invoices at 23:59:30 / 00:00:30 (T-U1, T-U2, T-I10; M-13).

WEAK TESTS FOUND AND STRENGTHENED
- The R3 failing-closed tests only made the handler throw (M-5 survived): refused-response cases added. No existing test was weakened or deleted.
