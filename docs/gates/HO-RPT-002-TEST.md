GATE: TEST
HANDOVER: HO-RPT-002
COMMITS: a5331c33 (code under test, production code unchanged); TEST work: 3dd10e3c (cases first) and the commit holding this file
DATE: 2026-10-14
STATUS: FAIL

One HIGH: the staff landing change (ADR-FE-130) breaks an existing e2e test, `tests/e2e/home.spec.ts` 'signed in as staff lands on /staff/cats', although the handover's Amended (12) and ADR-FE-130 say no existing test broke. The fix is one line in a file outside section 13 (expect `/staff`), so it needs the integrator or a scope amendment; nothing in the RPT-002 code is wrong. Every other finding is MEDIUM or LOW and is a new row in docs/backlog/RPT.md (89-94). Production code was not changed.
Case sheet: docs/test-cases/TC-HO-RPT-002.md (written first, every row NOT RUN, commit 3dd10e3c; then filled one row at a time).

COUNTS (108 rows)
- PASS 97, FAIL 4, BLOCKED 7, NOT RUN 0.
- FAIL: L-1 (the home.spec regression), REAL:E-X2 (branch picker overflow, below), M-3 and M-4 (mutants that survived the builder's tests; fixed in this gate by tests, now killed).
- BLOCKED: five MSW rows for cases that exist only on the real API (E-A3, E-A4, E-C1, E-C4, E-D2), REAL:E-D2 (delivery rows not observable), L-3 (full 2678-test MSW suite).

FINDINGS
- [HIGH] web/tests/e2e/home.spec.ts:33. The test asserts `toHaveURL(/\/staff\/cats/)` after `signInAsStaff` then `goto('/')`; with ADR-FE-130 the landing is `/staff`, received `http://localhost:3114/staff`. Deterministic. The handover's Amended (12) ("No existing test broke on the landing change") and ADR-FE-130's Status are therefore wrong. Fix: change the expectation and the test name to `/staff`; outside section 13, so owner or integrator.
- [MEDIUM] web/components/branches/ActiveBranchPicker.tsx (HO-ADM-002). With a staff member assigned to several branches with 22-character names, the branch `<select>` overflows 10 px at 360 px on every staff page. This fails the builder's own E-X real case against a multi-branch seed. The RPT screens add no overflow of their own: with the picker hidden, the dashboard, schedule list and detail, all 21 reports and the chart page have none at 360/768/1024/1440. Not caused by this slice. Row 89.
- [MEDIUM] web/components/reports/ReportChartCanvas.tsx. With real data the money-formatted Y-axis labels are clipped on the left (axis width 60) and overlapped by the axis title; the screenshot is unreadable at the scale. Row 90.
- [LOW] Run history showed no delivery rows: the API returned `deliveries: []` for every manual run to two recipients on a host without an email provider. E-D2's delivery half is unverified. Row 91.
- [LOW] Weak and missing tests found by mutation, fixed here (row 92): the branch-reset test passed with the reset removed; the post-save timer cleanup, U-E8 and scheduleId encoding had no behavioural test.
- [LOW] reports-real-api.spec.ts assumes one short-named branch, trips the sign-in limiter, and lacks the cases run here (row 93). [LOW] The full MSW suite cannot be run serially in reasonable time (row 94).

WEAK TESTS FOUND AND STRENGTHENED (no test was deleted or weakened)
- schedules.test.tsx 'moves off "All branches"...': the DOM-only assertion passed with the production reset removed. It now selects a recipient, submits and asserts the submitted `branchId`. Mutant M-3 is killed.
- Added web/tests/components/report-schedules/new-page.test.tsx (2 tests: navigates after the settle delay; does not navigate when the page is left inside it). Mutant M-4 is killed.
- Added three U-E8 tests to schedules.test.tsx (guard armed only while dirty, off after save). Mutants M-6a/M-6b are killed.
- Added api-paths.test.ts (7 id routes, hostile id `a/../b?x=1#frag`, URL asserted on the wire). Mutant M-7 is killed.
- Still weak and left (row 72): `length > 0` assertions in viewer.test.tsx and dashboard.test.tsx.

MUTATION SPOT-CHECKS (each file restored; git status clean after each)
- M-1 formatCell money returns the raw amount: 2 tests fail. M-2 compatiblePeriods forward rule inverted: 4 tests fail. M-5 safeFilename (no basename split, no try/catch, no dot strip): each fails 1 test.
- M-3 branch reset removed: survived at a5331c33, killed after strengthening. M-4 timer cleanup removed: survived, killed after the new test.

CHECKED AND HELD
- tsc, eslint and next build clean. Vitest 227 files / 1674 tests passed at a5331c33. The two known flakes (inventory goods-receipt-form, shared-pieces) passed in the full run and alone (24 tests).
- MSW Playwright, clean run: dashboard-home, reports-viewer, reports-schedules, chromium + mobile-360, workers=1, PORT 3114: 28 of 28 passed. E-B4 (waitForMswReady) passed in every clean run. U-C6 and E-X2 measure scrollWidth in a real browser.
- Real API (DB catecommerce_rpt002e2e, API :5114, four branches, two with invoices, payments and a credit note, one with stock, one in Europe/London):
  - E-A3: branch switch sent the other branch's X-Branch-Id, zone label and tiles changed.
  - Polling: 1 request per 60 s interval while visible, 0 in 130 s while hidden, resumed when visible.
  - Debounce: six edits in 600 ms gave one preview call.
  - XHR export: an XHR with the staff-session cookie, X-Branch-Id and status 200; CSV headers equal the screen's 11 plus a trailing Currency column; PDF is `%PDF-`.
  - Real limiter: five run-now calls succeeded, the sixth returned 429, and the page said "You're running reports too quickly. Try again in a minute or so." with no error code in the DOM.
  - Chart with data: 2 bars, hidden table equals the API rows, ticks go through the money formatter.
  - All 21 catalogue reports opened with data: HTTP 200, no alert, UI rows equal API rows, axe clean.
  - Schedule create then run-now over Month to date: "Generated - 4 rows", 39 KB.
  - Landing-affected e2e (10 specs): 164 passed; failures other than the HIGH are shop-staff x3 (product editor label) and 3 retried ordering-staff cases, none in a file this branch touched (not re-run on master).

PROCESS NOTE
My first full-suite Playwright run kept running after I believed I had stopped it (an `&` inside a background command), and shared port 3114 and test-results with my next runs for about 80 minutes. All evidence above is from runs made after I found and killed it; earlier results (including 502 false failures at 1133/2678) were discarded. The full 2678-test MSW suite was therefore not completed (L-3, BLOCKED).

CLEANUP
Real-API API stopped; seeder, throwaway e2e spec, appsettings.Development.json copy, `.next`, `test-results`, seed and log files removed. The database catecommerce_rpt002e2e is left as found (extra throwaway rows added). Copies of the seeder and spec are only in the session scratch folder.

SCOPE CHECK
- Added (tests only, inside section 13): web/tests/components/report-schedules/{new-page.test.tsx, api-paths.test.ts}; changed schedules.test.tsx. docs/test-cases/TC-HO-RPT-002.md, this file, docs/backlog/RPT.md rows.
- No production code, no src/**, no tests/** (the root test project) in the commit.
