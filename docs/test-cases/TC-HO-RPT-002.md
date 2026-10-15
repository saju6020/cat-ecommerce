# TC-HO-RPT-002 - Reporting screens (TEST gate sheet)

Handover: docs/handover/HO-RPT-002.md
Commit(s) under test: a5331c33 (branch ho/RPT-002, worktree D:/my-repo/cat_ecommerce-wt/RPT-002)
Written: 2026-10-14  -  Executed: 2026-10-14

> Statuses record what was **observed**, not inferred. The Evidence column names what was actually seen. `BLOCKED` marks a case that could not be executed or whose result was reasoned about rather than watched. This sheet replaces the builder's sheet (preserved in git history at 09556784). U- and E- IDs are the handover's section 15 and 16 IDs, not renumbered. The E- rows appear twice: prefix `MSW:` (mock suite) and `REAL:` (real API). Rows prefixed T- (tooling), B- (browser behaviour), M- (mutation), RG- (regression) are mine.

## Summary

| | Count |
|---|---|
| Total | 108 |
| PASS | 97 |
| FAIL | 4 |
| BLOCKED | 7 |
| NOT RUN | 0 |

FAIL rows: L-1 (existing e2e regression from the landing change), REAL:E-X2 (shared branch picker overflows 10 px at 360), M-3 and M-4 (two mutants survived the builder's tests; both fixed here by adding or strengthening tests). The 16 acceptance rows below are derived and not counted.

## Environment

- Code under test: branch ho/RPT-002, a5331c33 (web only). This gate added tests under web/tests/components/report-schedules (see the gate record).
- Build: tsc, eslint, next build clean. Unit suite: vitest 227 files / 1674 tests passed.
- MSW e2e: `next dev` on PORT 3114 with `.env.local` NEXT_PUBLIC_USE_MOCKS=true; chromium and mobile-360, --workers=1.
- Real API: API on :5114 from this worktree's Api project, database `catecommerce_rpt002e2e` (full connection string via ConnectionStrings__CatEcommerce; not catecommerce_dev or catecommerce_test), `next dev` on :3114 with NEXT_PUBLIC_USE_MOCKS=false NEXT_PUBLIC_API_BASE_URL=http://localhost:5114. No email provider configured (NotificationWorker warns), so no email was sent.
- Seed (throwaway, deleted after the run): 3 branches (two Asia/Dhaka billing branches with data, one Europe/London) plus one branch with stock; 6 staff (Administrator, 2 Accountants with email, ShopManager, Reception, Nurse) assigned to all four branches, password staffpassword1; in branch A a completed walk-in, 5 issued invoices (4 shop, plus consultation lines), payments by COD, bank transfer, cash and card, one credit note; branch B 2 invoices with payments; the stock branch 3 SKUs with batches (one landed-cost batch, near-expiry batches) and a sale. All data is dated today, so no multi-day trend exists.
- Not available: a capturing email sender, a second real time zone with different calendar date at this hour.
- Process error to disclose: my first full-suite run kept running after I believed it stopped (an `&` inside a background command). It shared port 3114 and test-results with my next runs from about 14:42 to 16:00. Every result in this sheet comes from a run made after I found and killed it (16:05), except where noted. The runs affected were repeated clean.


## Checks

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| T-1 | tsc (npm run typecheck) | Clean | PASS | npm run typecheck (tsc --noEmit) exited 0, no output |
| T-2 | npm run lint | Clean | PASS | npm run lint (eslint . --max-warnings 0) exited 0; eslint and prettier also clean on the 3 test files this gate added/changed |
| T-3 | vitest --maxWorkers=2, whole suite | All pass (known flakes outside RPT rerun alone) | PASS | vitest run --maxWorkers=2 at a5331c33: 227 files, 1674 tests passed (427 s). After this gate's additions the RPT group (8 files, 162 tests) passed 9 of 10 consecutive runs; the 1 failure was not identified (see Gaps) |
| T-3a | Known flake: inventory goods-receipt-form, rerun alone | Pass alone | PASS | goods-receipt-form + shared-pieces rerun alone: 2 files, 24 tests passed (they also passed inside the full run; no flake seen) |
| T-3b | Known flake: inventory shared-pieces, rerun alone | Pass alone | PASS | same run as T-3a |
| T-4 | next build | Clean | PASS | next build completed and printed the route table; .next removed afterwards |
| T-5 | git status clean after runs; seed and temp files removed | Clean | PASS | after cleanup git status showed only the test files this gate added or changed; seeder, throwaway e2e spec, appsettings.Development.json copy, .next and test-results removed |

## Unit cases (handover section 15)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| U-A1 | Dashboard payload with 6 tiles | Exactly those tiles in server order; none invented | PASS | dashboard.test.tsx 'U-A1: renders exactly the tiles sent, in the server order' |
| U-A2 | Nurse payload (no financial tiles) | No financial/stock/sales tile or placeholder (ADR-FE-137) | PASS | dashboard.test.tsx 'U-A2: a clinic-only payload has no money, stock or sales tile and no placeholder' |
| U-A3 | Figure with value null | Dash + sr-only 'Not recorded'; never 0 | PASS | dashboard.test.tsx 'U-A3: a null figure is a dash with Not recorded, never 0' |
| U-A4 | Tile with drillDown null | No link | PASS | dashboard.test.tsx 'U-A4: no link without a drillDown, a link with one' |
| U-A5 | Poll while dialog open and field focused | Values update; dialog and focus kept; polite 'Updated' announcement | PASS | dashboard.test.tsx 'U-A5: a refresh updates the figures without closing an open dialog' (props level; browser level is REAL:E-A4) |
| U-A6 | document.hidden | No refetch until visible (query-options level; browser level is B-POLL) | PASS | dashboard.test.tsx useDashboard block (query options: no background refetch). The browser behaviour is B-POLL |
| U-A7 | Europe/London branch payload viewed in Asia/Dhaka | Money from string; date is payload localDate | PASS | dashboard.test.tsx 'U-A7: money comes from the string and the date is the payload local date'; mutation M-1 fails it |
| U-B1 | Catalogue grouped by category | Plain-language headings; omitted report absent | PASS | viewer.test.tsx 'U-B1: groups by plain-language category and omits what the payload omits' |
| U-B2 | 403 from catalogue | PermissionDeniedState (nav entry hidden adapted to page refused, Amended 9) | PASS | viewer.test.tsx 'U-B2: a 403 renders PermissionDeniedState' (nav entry is not hidden: Amended 9) |
| U-B3 | canSchedule false | No 'Schedule this report' | PASS | viewer.test.tsx 'U-B3: no Schedule this report without canSchedule' |
| U-C1 | Definition with every parameter type | One typed bounded control each; unknown types do not crash | PASS | viewer.test.tsx 'U-C1: one control per parameter, typed and bounded' |
| U-C2 | Empty URL | Seeded from defaultRange; no Date maths | PASS | viewer.test.tsx 'U-C2/U-C4/U-C7/U-C8: seeds from defaultRange...' plus source-scan.test.ts (no date maths) |
| U-C3 | Apply preset/custom range; reload | Filters in URL; reload shows same report | PASS | viewer.test.tsx 'U-C3: Apply writes the filters to the URL'; reload in a second context is MSW and REAL E-B4 |
| U-C4 | Totals + two notes | tfoot Total; notes verbatim | PASS | viewer.test.tsx 'U-C2/U-C4/U-C7/U-C8' (tfoot Total, notes verbatim) |
| U-C5 | Restricted columns absent vs present | Rendered from columns alone; no hidden affordance | PASS | viewer.test.tsx 'U-C5: restricted columns are absent for a shop manager, present for an accountant' |
| U-C6 | 12-column report at 1440 and 360 | Table / cards; no horizontal page scroll (measured in browser: E-X2) | PASS | measured in a browser, not jsdom: MSW E-X2 (chromium + mobile-360) reads scrollWidth - clientWidth at 360/768/1024/1440 on the 12-column report |
| U-C7 | Sort sortable and non-sortable header | aria-sort + URL / no button | PASS | viewer.test.tsx 'U-C7: sorting writes the URL; a non-sortable header has no button' |
| U-C8 | null, money, percent, date, instant, boolean | Per section 8.4; instants in payload zone | PASS | format.test.ts formatCell block; mutation M-1 fails 'renders money from the string pair' |
| U-C9 | Chart toggle on timeBucketed | Hidden by default; hidden table equals visible rows | PASS | viewer.test.tsx 'U-C9: the chart is hidden by default and its hidden table matches the rows'; REAL B-CHART (hidden table 2 rows = API 2 rows) |
| U-C10 | Empty result | Names period and branch; suggests widening | PASS | viewer.test.tsx 'U-C10: the empty state names the period and branch and suggests widening' |
| U-D1 | Export CSV | One GET with current filters; blob saved; URL revoked | PASS | exports.test.tsx 'U-D1' (download seam mocked); the XHR layer itself is B-XHR in a real browser |
| U-D2 | PDF 413 | Message; no download; button idle | PASS | exports.test.tsx 'U-D2: a 413 on PDF shows the CSV advice, downloads nothing and returns to idle' |
| U-D3 | 400 ReportTimedOut | Timeout message | PASS | exports.test.tsx 'U-D3: ReportTimedOut shows the shorter-range advice' |
| U-D4 | Export in flight | Busy, disabled, announced; table usable | PASS | exports.test.tsx 'U-D4: while in flight the button is busy, disabled and announced' |
| U-E1 | Report with limited periods | Only allowed periods + hints | PASS | schedules.test.tsx 'U-E1' and format.test.ts compatiblePeriods; mutation M-2 fails 4 tests |
| U-E2 | Recipient without email | Unselectable with reason | PASS | schedules.test.tsx 'U-E2: a recipient without an email is unselectable, with the reason as text' |
| U-E3 | Any form change | One debounced preview call; no client recurrence maths | PASS | schedules.test.tsx 'U-E3/U-E4' and 'U-E3: typing a name does not call the preview again'; browser level is B-DEB |
| U-E4 | Preview with deliveryHeldUntil | Quiet-hours sentence | PASS | schedules.test.tsx 'U-E3/U-E4' (quiet-hours sentence) |
| U-E5 | 400 ReportRecipientNotPermitted | On recipients field; other values kept (name not shown: Amended 6) | PASS | schedules.test.tsx 'U-E5: a refused recipient lands on the recipients field and the rest keeps its values' (server sentence; name not shown: Amended 6) |
| U-E6 | Cancel, Pause, Run now confirms | State effect and recipient count | PASS | schedules.test.tsx 'ScheduleActions U-E6' block passed |
| U-E7 | Run history Generated/Failed/Purged/attachmentOmitted | Status as text; failure explained; note shown | PASS | schedules.test.tsx 'RunHistoryTable U-E7' block passed |
| U-E8 | Dirty form + navigate | Unsaved-changes confirm | PASS | NO test existed at a5331c33. Added 3 tests in schedules.test.tsx (beforeunload armed only while dirty, off after save); mutations M-6a/M-6b are killed by them |
| U-X1 | axe on viewer, dashboard, schedule form | Zero serious/critical (jsdom has no axe: see E-X1) | PASS | jsdom has no axe; run in the browser: MSW E-X1 passes in both projects, REAL B-DATA/B-OVF/B-CHART/B-SCHED axe clean |
| U-X2 | Every server 400 | Field-mapped or page-level; errorCode never in DOM | PASS | viewer.test.tsx and exports.test.tsx (errorCode never in the DOM, field mapping) |
| U-X3 | Real source scan of components | No arithmetic on money/totals/percent; no Number( on amount; no recurrence | PASS | source-scan.test.ts reads the real files with fs and scans components/{dashboard,reports,report-schedules}; passed. It does not scan lib/format (where toChartNumber uses parseFloat for plotting only) |

## Integration cases: end-to-end on MSW (chromium and mobile-360, workers=1, PORT 3114)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| MSW:E-A1 | Reception signs in | Lands on /staff with reception tiles | PASS | dashboard-home E-A1, both projects. clean run: dashboard-home + reports-viewer + reports-schedules, chromium and mobile-360, --workers=1 PORT 3114: 28 of 28 passed (2.8 min) |
| MSW:E-A2 | Accountant then nurse | Accountant money tiles; nurse none and Reports refused | PASS | dashboard-home E-A2, both projects (accountant money tiles; nurse refused on Reports) |
| MSW:E-A3 | Switch active branch | Tiles reload for the other branch; date label follows its zone | BLOCKED | no MSW spec for the branch switch exists and I did not write one; executed on the real API (REAL row). Component level: U-A7 |
| MSW:E-A4 | Dashboard open past refresh interval | Figures update; dialog stays open | BLOCKED | handover scopes the refresh to the real API; executed there (REAL row) |
| MSW:E-B1 | Daily schedule, vaccination due, queue performance | Rows as seeded | PASS | reports-viewer E-B1 opens the catalogue and the generic viewer; the mock has 2 of the 3 named reports, the rest is REAL E-B1 |
| MSW:E-B2 | Range over limit then valid | Limit message then results | PASS | reports-viewer E-B2, both projects |
| MSW:E-B3 | Export CSV and PDF | Two non-empty files; CSV headers equal screen headers | PASS | reports-viewer E-B3: CSV and PDF both non-empty, PDF 413 explained, both projects |
| MSW:E-B4 | URL in second context, same role | Identical filtered report | PASS | reports-viewer E-B4 (second context, uses waitForMswReady), both projects; also 14 of 14 in a chromium-only rerun |
| MSW:E-C1 | Stock reports (on hand, near expiry, low stock) | Values and valuation shown | BLOCKED | mock has one stock report; executed on the real API (REAL row) |
| MSW:E-C2 | Revenue by line, shop by category/origin: accountant vs shop manager | Margin/coverage accountant only | PASS | reports-viewer E-C2 (margin accountant only), both projects |
| MSW:E-C3 | Payment reconciliation | COD, cash, card, bank transfer rows; variance in words | PASS | reports-viewer E-C3 (variance in words), both projects |
| MSW:E-C4 | Growth reports | Open and show data | BLOCKED | mock has one growth report; executed on the real API (REAL row) |
| MSW:E-D1 | Create daily schedule, two recipients, preview, save | 201; next runs shown; in list | PASS | reports-schedules E-D1, both projects |
| MSW:E-D2 | Run now then run history | Generated run, rows, size, delivery rows | BLOCKED | run-now history needs the real API; see REAL row |
| MSW:E-D3 | Pause, resume, cancel | Status changes; cancel keeps past runs | PASS | reports-schedules E-D3, both projects |
| MSW:E-D4 | Shop manager opens schedules | PermissionDeniedState | PASS | reports-schedules E-D4, both projects |
| MSW:E-X1 | axe on /staff, /staff/reports, report with chart open, export in flight, schedule form | Zero serious/critical | PASS | axe zero serious/critical on dashboard, catalogue, 12-column report with chart open, schedule list and form, both projects |
| MSW:E-X2 | Every route at 360/768/1024/1440 incl. 12-column report | No horizontal page scroll, no clipped control | PASS | no horizontal scroll at 360/768/1024/1440 on the same pages, both projects (mock has a single short-named branch, so the picker overflow seen on the real API cannot appear here) |

## Integration cases: end-to-end on the real API (API :5114, DB catecommerce_rpt002e2e, richer seed)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| REAL:E-A1 | Reception signs in | Lands on /staff with reception tiles | PASS | builder's reports-real-api.spec (clean rerun), richer seed: reception lands on /staff with a live dashboard |
| REAL:E-A2 | Accountant then nurse | Accountant money tiles; nurse none and Reports refused | PASS | builder's reports-real-api.spec (clean rerun), richer seed: accountant sees Payments today and Receivables; nurse refused on Reports |
| REAL:E-A3 | Switch active branch | Tiles reload for the other branch; date label follows its zone | PASS | gate spec: one administrator assigned to 4 branches switched A -> London Clinic (Europe/London) -> A in the picker; each dashboard request carried that branch's X-Branch-Id; header showed 'Times are in Europe/London.' then 'Asia/Dhaka.'; tile text differed. Limit: both branches' localDate was 2026-10-14 (same calendar date at this hour), so a differing date is not shown; the date label follows the payload (U-A7) |
| REAL:E-A4 | Dashboard open past refresh interval | Figures update; dialog stays open | PASS | builder's reports-real-api.spec (clean rerun), richer seed: 'Updated HH:mm' changed after 65 s (refreshSeconds 60). The open-dialog half is U-A5 (component level) |
| REAL:E-B1 | Daily schedule, vaccination due, queue performance | Rows as seeded | PASS | B-DATA: clinic-daily-schedule 1 row, clinic-queue-performance 31 rows, vaccination-due-list 0 rows (no vaccines seeded: empty state, no error); UI row count equals API row count |
| REAL:E-B2 | Range over limit then valid | Limit message then results | PASS | builder's reports-real-api.spec (clean rerun), richer seed: 2020-01-01..2026-12-31 refused with 'covers at most N days' on the field; Reset restores results |
| REAL:E-B3 | Export CSV and PDF | Two non-empty files; CSV headers equal screen headers | PASS | builder's reports-real-api.spec (clean rerun), richer seed: CSV and PDF downloaded non-empty; B-EXP: PDF starts %PDF-, CSV headers equal the screen's 11 headers plus one trailing 'Currency' column the server adds |
| REAL:E-B4 | URL in second context, same role | Identical filtered report | PASS | gate spec: second browser context, same accountant, same URL: identical table text |
| REAL:E-C1 | Stock reports (on hand, near expiry, low stock) | Values and valuation shown | PASS | stock-position 4 rows, note 'Valuation covers 1 of 4 stocked batch lines (25.0 %)' shown verbatim; stock-near-expiry (server default range) 2 rows, 'Within 30 days', unrecorded landed cost shown as dash + 'Not recorded' not 0; stock-low-reorder 1 row |
| REAL:E-C2 | Revenue by line, shop by category/origin: accountant vs shop manager | Margin/coverage accountant only | PASS | builder's reports-real-api.spec (clean rerun), richer seed: accountant sees Margin on revenue-by-business-line (2 rows, 3 notes); shop manager refused. revenue-shop-by-category and -by-origin 1 row each with a coverage note |
| REAL:E-C3 | Payment reconciliation | COD, cash, card, bank transfer rows; variance in words | PASS | payment-reconciliation 4 rows (CashOnDelivery, BankTransfer, CashInStore, CardInStore as recorded), variance shown as the word 'Mismatch' beside the amount (expected 1,700.00 vs received 0.00) |
| REAL:E-C4 | Growth reports | Open and show data | PASS | cohort-retention 1 row, customer-lifetime-value 4 rows, customers-new-vs-returning 31 rows open without an error; axe clean |
| REAL:E-D1 | Create daily schedule, two recipients, preview, save | 201; next runs shown; in list | PASS | builder's reports-real-api.spec (clean rerun), richer seed and B-SCHED: schedule created with two recipients, server preview ('The next one covers...') shown, schedule appears in the list |
| REAL:E-D2 | Run now then run history | Generated run, rows, size, delivery rows | BLOCKED | Part observed: run-now 200, 'Generated - 4 rows', run row Generated, 01 Oct to 14 Oct, 4 rows, 39 KB. Not observed: per-recipient delivery rows and the email. The API returned deliveries: [] for every manual run (checked with curl) and no capturing sender was available. Needs a host with the capturing email sender, or an answer from RPT-001 on when delivery rows appear |
| REAL:E-D3 | Pause, resume, cancel | Status changes; cancel keeps past runs | PASS | builder's reports-real-api.spec (clean rerun), richer seed: pause, resume, cancel with confirms; cancelled schedule keeps Run history |
| REAL:E-D4 | Shop manager opens schedules | PermissionDeniedState | PASS | builder's reports-real-api.spec (clean rerun), richer seed: shop manager on /staff/admin/report-schedules gets the permission message |
| REAL:E-X1 | axe on /staff, /staff/reports, report with chart open, export in flight, schedule form | Zero serious/critical | PASS | axe zero serious/critical: /staff (B-OVF), schedule list (B-OVF), schedule detail after run-now (B-SCHED), all 21 catalogue reports at 1440 (B-DATA), revenue report with chart open (B-CHART). Not run on the real API: the /staff/reports catalogue page and an export in flight (MSW only) |
| REAL:E-X2 | Every route at 360/768/1024/1440 incl. 12-column report | No horizontal page scroll, no clipped control | FAIL | builder's E-X real failed with a 10 px horizontal scroll at 360 px on /staff (and the same on every staff page): the shared ActiveBranchPicker <select> (right edge 370 px) overflows when staff have several branches with 22-character names. Reproduced, settled, in OVF-DIAG on 5 pages. With the picker hidden, no overflow at 360/768/1024/1440 on the dashboard, schedule list and detail, all 21 reports and the chart page (B-OVF, B-DATA, B-CHART, B-SCHED). Not an RPT-002 file; backlog row |

## Gap cases found by this gate

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| B-POLL | Polling pauses while the tab is hidden (real browser, count dashboard requests) and resumes when visible | No request while hidden; request after visible | PASS | refreshSeconds=60. Visible 68 s: 1 dashboard request. Tab made hidden (document.visibilityState overridden + visibilitychange, TanStack reads it) for 130 s: 0 requests. Made visible: a request fired. Hidden state was simulated, not a real tab switch |
| B-DEB | Preview is debounced (burst of edits in schedule form, count POST /preview) | One call for a burst | PASS | six edits of the send time 100 ms apart: exactly 1 POST /preview; two edits 1.5 s apart: 2 calls (debounce 500 ms) |
| B-XHR | XHR export sends credentials (cookie) and X-Branch-Id in a real browser | Request on the wire has both | PASS | export request: resourceType xhr, X-Branch-Id = the active branch id, Cookie contained pc_staff_session, status 200, saved as purrcare-revenue-by-business-line-2026-10-01-to-2026-10-31.csv (fallback name; Content-Disposition is unreadable cross-origin, backlog 88) |
| B-429 | Real 429 limiter: run-now 6 times | 429 shown as hedged wait text, no errorCode in DOM, UI recovers | PASS | run-now six times: 200 x5 then 429. Page text: "You're running reports too quickly. Try again in a minute or so." No errorCode or '429' in the DOM; run history and Run now button still usable; axe clean |
| B-CHART | Chart with real data on a time-bucketed report | Chart renders, hidden table equals rows, ticks formatted, axe clean | PASS | revenue-by-business-line with data: 2 bars, hidden table 2 rows = API 2 rows, axe clean. Tick text since 2026-10-14 (owner: show the taka sign): compact labels with the sign ("৳14K", "৳10.5K"), pinned by `chart-axis.test.ts`; screenshots at 360 and 1440 px |
| B-DATA | Reports with data (several invoices, payments, credit notes, two branches, stock) | Rows, totals, notes from the API; no client maths | PASS | all 21 catalogue reports, administrator, data branch (stock reports on the stock branch): all HTTP 200, no alert, UI table rows = API rows (13 reports had rows), axe clean at 1440, no overflow at 360 and 1440 (picker hidden) |
| B-WMR | waitForMswReady (E-B4 helper) works | Second context opens same filtered report, no retry loop | PASS | waitForMswReady is used by openReportsAs; E-B4 passed in 3 clean runs (28/28; 14/14 chromium; 28/28). In two runs that overlapped with a leftover runner of mine (see Environment) E-A1/E-A2/E-C3 failed intermittently on sign-in/redirect timing; I cannot attribute those to the helper |
| B-EXP | Export with data: CSV headers equal screen headers | Match | PASS | CSV header line equals the screen's 11 column headers in order (case-insensitive) plus 'Currency'; PDF begins %PDF- |
| B-SCHED | Schedule create then run-now with data against real API | Generated run with rows | PASS | payment-reconciliation schedule, period Month to date, 2 recipients: run-now 200, toast 'Generated - 4 rows', run row Generated 01 Oct to 14 Oct, 4 rows, 39 KB; axe clean; no overflow (picker hidden). Delivery cell empty: see REAL:E-D2 |
| B-OVF | No sideways scroll at 360/768/1024/1440 with data-bearing reports; axe | scrollWidth <= clientWidth; zero serious/critical | PASS | dashboard with data and schedule list at 4 widths, axe clean, with the picker hidden; the unhidden failure is REAL:E-X2 |

## Existing suite versus the landing change (ADR-FE-130)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| L-1 | Existing e2e home.spec 'signed in as staff lands on /staff/cats' after the staff landing moved to /staff | Still passes, as Amended (12) and ADR-FE-130 state ('no existing test broke') | FAIL | Deterministic in 3 runs: `expect(page).toHaveURL(/\/staff\/cats/)` received `http://localhost:3114/staff`. The test asserts exactly what ADR-FE-130 changed; the handover's claim is false and the fix is a one-line expectation in a file outside section 13 |
| L-2 | Existing e2e specs that sign staff in or redirect: staff-auth, cat-smart-redirect, staff-admin, notifications-log, ordering-staff, shop-staff, admin-branch-calendar, cat-a11y, accounts-a11y-responsive, home (chromium) | Unchanged by the landing change | PASS | 164 passed, 1 skipped, 4 failed, 3 flaky (14.3 min). Failures other than L-1: shop-staff x3 ('Country of origin' label not found in the product editor, timeout), ordering-staff x3 flaky (passed on retry). None touches a file this branch changed; not re-run on master, so 'pre-existing' is unverified |
| L-3 | The whole MSW Playwright suite (2678 tests, chromium + mobile-360) once, --workers=1 | All pass | BLOCKED | Started and abandoned: at about 5 tests per minute in the long a11y specs it needs more than 5 hours of a held dev server, and my first attempt was invalidated because a runner I thought I had stopped kept running and shared port 3114 and test-results with my later runs (502 failures at 1133 of 2678, all noise). Not used as evidence. Unblock: run per spec group on a quiet machine, or split by project |

## Mutation spot-checks (each restored; git status clean afterwards)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| M-1 | Mutation: formatCell money | A test fails | PASS | formatCell money returned value.amount: 2 tests failed (format.test 'renders money from the string pair', dashboard U-A7) |
| M-2 | Mutation: compatiblePeriods | A test fails | PASS | compatiblePeriods forward rule 'forward && definitionForward' -> '||': 4 tests failed (format.test x3, schedules U-E1) |
| M-3 | Mutation: branch reset in chooseReport | A test fails | FAIL | branch reset in chooseReport disabled: the builder's test STILL PASSED (jsdom shows the first option when the value has no option, so toHaveValue passed with the bug). Strengthened the test to submit and assert the sent branchId; the mutant is now killed (1 failed). Decoration found and fixed |
| M-4 | Mutation: post-save timer cleanup | A test fails | FAIL | post-save timer cleanup removed: NO test failed (no test covered unmount inside the 400 ms). Added new-page.test.tsx (control + leave-inside-delay); the mutant is now killed (1 failed) |
| M-5 | Mutation: safeFilename | A test fails | PASS | three mutants of safeFilename (no basename split, no try/catch, no leading-dot strip): each failed 1 test in filenames.test.ts |

## Regression cases from earlier gates

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| RG-1 | ARCH BLOCKER | mockServiceWorker.js restored | git diff 5a187b6b -- web/public/mockServiceWorker.js empty | PASS | git diff 5a187b6b HEAD -- web/public/mockServiceWorker.js is empty |
| RG-2 | ARCH 75 | decisions.md only appends ADR-FE-129..138 | diff removes no lines | PASS | git diff --numstat 5a187b6b HEAD -- web/docs/decisions.md: 82 added, 0 removed |
| RG-3 | CODE 67 | 429 fallback wording is 'a minute or so' not 60 s as fact | errors test; B-429 | PASS | real 429 text 'Try again in a minute or so' (B-429); errors unit test |
| RG-4 | CODE 68 | Post-save timer cleared on unmount; saved form not re-submittable | schedules test; M-4 | PASS | new-page.test.tsx (unmount inside delay) and schedules 'disables submit once a save succeeded'; M-4 now killed. The original suite did not cover the unmount |
| RG-5 | CODE 69 | Branch '' with non-all-branches report resets to a real branch | schedules test; M-3 | PASS | strengthened schedules test; M-3 now killed. The original test was decoration |
| RG-6 | CODE 70 | openReportsAs: one attempt, waitForMswReady | B-WMR | PASS | openReportsAs makes one attempt; 3 clean runs passed, see B-WMR |
| RG-7 | CODE 71 | Variance edge values pinned | format test | PASS | format.test.ts pins '-0.00' and the other edge values |
| RG-8 | SEC 85 | safeFilename never throws; basename only | filenames test; M-5 | PASS | filenames.test.ts; M-5 killed |
| RG-9 | SEC 86 | Fallback filename from server-normalised filters, stripped | exports/filenames test | PASS | filenames.test.ts 'strips anything else from raw URL values'; real fallback name observed in B-XHR |
| RG-10 | SEC 87 | scheduleId encoded on all six routes | grep + test | PASS | NO test existed. Added api-paths.test.ts: 7 id routes receive 'a/../b?x=1#frag' and the URL on the wire is path-encoded; a mutant without encoding in runScheduleNow is killed |
| RG-11 | ARCH 84 | Chart ticks go through the money formatter | B-CHART | PASS | Superseded: ticks are `formatAxisTick` compact labels with the currency sign, not `formatCell` (see rows 90, 101); `chart-axis.test.ts` |
| RG-12 | SEC | No report data in storage; no dangerouslySetInnerHTML | grep + browser storage inspection | PASS | static only: no localStorage, sessionStorage or dangerouslySetInnerHTML in the RPT components, download helper, API modules or hooks (source-scan enforces the last). Browser storage was not inspected |

## Acceptance-criteria coverage

| Criterion | Covered by | Status |
|---|---|---|
| A1: Home dashboard shows role figures | U-A1, U-A7, E-A1 (MSW+real), E-A3. ER E-A1/E-A3 real; U-A1, U-A7 | PASS |
| A2: Nothing not permitted, no placeholder | U-A2, U-C5, E-A2. U-A2, U-C5, E-A2 on both runs | PASS |
| A3: Refresh keeps dialog/focus, pauses when hidden | U-A5, U-A6, B-POLL, E-A4 real. U-A5, U-A6, B-POLL, real E-A4 | PASS |
| A4: Daily schedule, vaccination due, queue performance | E-B1 real. real E-B1 (vaccination due list had no data) | PASS |
| A5: Filter by range and branch, one catalogue-driven screen | U-C1, U-C2, U-C3, E-B2, E-B4. U-C1..C3, E-B2, E-B4 on both runs | PASS |
| A6: CSV and PDF with failures explained | U-D1..D3, E-B3, B-XHR, B-EXP. U-D1..D3, E-B3, B-XHR, B-EXP | PASS |
| A7: Stock reports usable | E-C1 real, B-DATA. real E-C1 | PASS |
| A8: Margin only to permitted, never client-side | U-C4, U-C5, U-X3, E-C2 real. U-C4, U-C5, U-X3, real E-C2 | PASS |
| A9: Growth reports usable | E-C4 real. real E-C4 | PASS |
| A10: Payment reports with variances in words | E-C3 real, B-DATA. real E-C3, B-DATA (the word 'Mismatch'; no Over/Short: Amended 7) | PASS |
| A11: Schedule with server next runs, manage | U-E1..E6, E-D1, E-D3, B-DEB. U-E1..E6, E-D1, E-D3, B-DEB | PASS |
| A12: Run outcome and delivery visible | U-E7, E-D2 real, B-SCHED. run outcome and file size visible (B-SCHED); per-recipient delivery not observable (REAL:E-D2) | BLOCKED |
| A13: Non-admins cannot reach schedules | E-D4. E-D4 on both runs | PASS |
| A14: No figure computed in browser | U-C2, U-E3, U-X3, M-1. U-C2, U-E3, U-X3; M-1 shows the money path is covered. B-DATA: UI rows equal API rows | PASS |
| A15: axe zero, 360 px, chart equivalent table | U-C6, U-C9, U-X1, E-X1, E-X2, B-OVF, B-CHART. 360 px usability fails with a multi-branch user and long branch names because of the shared picker (REAL:E-X2); the RPT screens themselves pass. axe and the chart's hidden table pass | FAIL |
| A16: Errors say what to do; values survive | U-C3, U-D2, U-E5, U-X2, B-429. U-C3, U-D2, U-E5, U-X2, B-429 | PASS |

## Cases I could not run

| ID | Why | What would unblock it |
|---|---|---|
| MSW:E-A3, MSW:E-A4, MSW:E-C1, MSW:E-C4, MSW:E-D2 | No MSW spec exists for them (the mock has one stock report, one growth report, no run history over time); the handover assigns them to the real API, where they ran | Nothing needed unless the owner wants MSW coverage too |
| REAL:E-D2, A12 | Delivery rows per recipient and the email were not observable: the API returned `deliveries: []` for every manual run and no capturing sender was attached | Run the API with the capturing email sender, or confirm with RPT-001 when delivery rows are created |
| L-3 | Full MSW suite of 2678 tests too long for one held dev server on this machine; first attempt invalidated | Quiet machine, or run it per spec group in CI |

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| One failure of the RPT component group (1 of 162 tests, 1 run in 10) was not identified | Seen once while eslint/tsc/prettier ran at the same time; 9 later runs passed | RPT-002 follow-up: rerun under load and name it |
| Hidden-tab test simulates visibility (overrides `document.visibilityState`), no real tab switch | Playwright headless cannot hide a tab | None |
| E-A3 shows the zone label and a different tile set but not two different calendar dates | Both zones are on the same date at this hour | Rerun after 18:00 UTC |
| Chart with data has one day only (2 bars) | The seed cannot back-date invoices | Seeder with a controlled clock |
| `/staff/reports` catalogue page and an export in flight were not axe-checked on the real API | The builder's E-X real stops at the first overflow | Fix the picker, rerun E-X real |
| Browser storage was not inspected for report data (RG-12 is static) | Time | Security follow-up |
| The three reports' weak `length > 0` assertions (backlog 72) remain | Out of this gate's focus | RPT-002 |
| The gap-spec and seeder I used are deleted per instruction; copies kept only in the session scratch folder | tests/** is outside the slice | If wanted, add as reports-real-api cases (backlog 93) |

## Variance direction (HO-RPT-001-FU1, owner 2026-10-14: "Over / Short")

Built against the FU1 contract (R13 and R14 `varianceDirection`; depends on FU1 being merged first). Component tests (`tests/components/reports/viewer.test.tsx`, `format.test.ts`, `dashboard.test.tsx`): table and card list show Short, Over and Matched as words; `null` is a dash with "Not recorded" and never "Matched"; unknown values and a bare number are not directions; the tile shows the amount only. MSW e2e E-C3 asserts Short, Over and Matched on the payment reconciliation report. Not run against the real API: FU1 is unmerged (the real API of master has no such column).
