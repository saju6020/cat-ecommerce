# TC-HO-XCT-002 — Cross-cutting and technical (frontend): offline board, retention, erasure, backup/restore, web vitals, perf tooling

Handover: docs/handover/HO-XCT-002.md (sections 15 and 16 are the contract; IDs below are the handover's own)
Commit(s) under test: 0b69692a (branch ho/XCT-002, worktree D:/my-repo/cat_ecommerce-wt/XCT-002)
Written: 2026-10-14  ·  Executed: 2026-10-14 (see the Evidence column)

> This is the TEST gate's sheet. It replaces the builder's own sheet that stood at this path
> (commit 0b69692a and earlier hold it, including the E-B1 / E-B2 figures the builder measured and the
> findings for other owners). Every row below was written as NOT RUN before any test was run.
>
> Statuses record what was **observed**, not inferred. The Evidence column names what was actually
> seen. `BLOCKED` marks a case that could not be executed or whose result was reasoned about rather
> than watched — it is not a failure, and it is not hidden.

## Summary

Rows counted across the unit, end-to-end, regression, mutation and whole-run tables (108, after the ACCEPTANCE follow-up of 2026-10-14). The acceptance table has its own status column.

| | Count |
|---|---|
| Total | 108 |
| PASS | 100 |
| FAIL | 2 (E-B2: four other-slice routes over the 170 kB budget, backlog row 68; E-B1: `/` and `/shop/products/[slug]` over 2500 ms, SHP's; both filed per row 109) |
| PARTIAL | 2 (E-A1 MSW and real: owner-accepted, row 108) |
| BLOCKED | 4 (U-X1, E-C4 MSW, E-C4 real, E-X2) |
| NOT RUN | 0 |

## Environment

- Worktree D:/my-repo/cat_ecommerce-wt/XCT-002, branch ho/XCT-002, head 0b69692a plus this gate's test-only changes. Windows 11, Node via nvm, .NET SDK 9.0.300.
- Unit: vitest 5.0.1 (jsdom), `--maxWorkers=2`.
- MSW Playwright: `next dev`, `NEXT_PUBLIC_USE_MOCKS=true`, chromium, PORT 3113, `--workers=1`.
- Real API: `dotnet run` of src/Api/CatEcommerce.Api on http://localhost:5113 in Development with environment-variable settings; PostgreSQL on localhost:5432, database `catecommerce_xct002e2e` (dropped, recreated and migrated to head `20261013180959_AttachRetentionGuardsToReportingTables` for the final run; two Administrator accounts seeded by SQL with an Argon2id hash I generated). `catecommerce_dev` and `catecommerce_test` were never opened. Web: `next build` with `NEXT_PUBLIC_USE_MOCKS=false`, `NEXT_PUBLIC_API_BASE_URL=http://localhost:5113`, then `next start -p 3113`, `XCT_PROD_BUILD=1`.
- Sweeps: tests/perf/playwright.config.ts, `next dev` with MSW, one audience at a time.
- Runs were sequential. No browser or server was left running; .next, test-results and the logs were removed.
- The sheet that stood here before (the builder's) is in git at 0b69692a.

## Unit cases (handover section 15)

Mapped to the vitest files under `web/tests/components/{offline,retention,operations,observability}`.
A handover case with no matching test is marked at execution time.

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| U-A1 | Monitor: 1 ping failure; 2 consecutive; then a success | online -> offline after the second -> online again | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): connectivity.test U-A1 |
| U-A2 | Monitor: a 500 from ping | degraded, not offline; board not offered | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): connectivity.test U-A2 |
| U-A3 | Monitor: navigator.onLine false plus one failure | offline immediately | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): connectivity.test U-A3 |
| U-A4 | OfflineSnapshotSync with the device flag off | No request, nothing written | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): store-and-sync U-A4 (2 tests) |
| U-A5 | 200 then 304 | Record written once; fetchedAt updated on 304; etag sent second time | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): store-and-sync U-A5 (2 tests) |
| U-A6 | 401 session_invalid during sync; 401 session_idle_timeout | Store cleared; store untouched | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): store-and-sync U-A6 (5 tests, incl. network failure and 403) |
| U-A7 | Typed values while the monitor flips offline; submit | Values intact; submit disabled with reason; no request; same values submit on recovery | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): connectivity.test U-A7 |
| U-A8 | client.ts POST while offline | OfflineError before fetch; mapped message; nothing queued | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): connectivity.test U-A8 (2 tests) |
| U-A9 | OfflineBanner in each state | Offline / degraded / recovered wording; role=status; text not colour; link only with valid snapshot | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): board-and-banner U-A9 (6 tests) |
| U-A10 | OfflineBoard with a valid snapshot | Both lists sorted, saved-at time, no action button | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): board-and-banner U-A10 |
| U-A11 | Expired snapshot; different staffAccountId; no snapshot | Three distinct explanations, no data | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): board-and-banner U-A11 (4 tests) |
| U-A12 | boardingTasks omitted NotPermitted / NotAvailable | Each reason in its own words | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): board-and-banner U-A12 |
| U-A13 | High-risk cat in the board | Badge plus handling note, text and icon | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): board-and-banner U-A13 |
| U-A14 | OfflineIdleLock after idleTimeoutMinutes | Data replaced by locked message; input before resets the timer | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): board-and-banner U-A14; board-security idle timer cases |
| U-A15 | Recovery while on the board | Routed to ?from=, queries invalidated, announcement | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): board-and-banner U-A15 (3 tests) |
| U-A16 | Sign-out; person change | Store cleared in both cases | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): store-and-sync U-A16 (8 wipe-path tests) |
| U-A17 | sw-register in mock mode, development, production with flag off | Never registers; unregisters and purges an existing worker | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): sw-register.test (6 tests) and the new sw-navigation.test (4) |
| U-B1 | RetentionScheduleTable | Every class with period and end action; "Review only" for clinical, financial, regulatory, audit | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): retention.test U-B1 |
| U-B2 | RunRetentionNow confirm; 409 | States what it will not do; in-progress message | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): retention.test U-B2 (2) |
| U-B3 | Run detail with CompletedWithErrors | Error codes glossed in plain language | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): retention.test U-B3 |
| U-B4 | Erasure ledger detail with a held scope | "Held until 12 Mar 2032 — NFR-LEG-5 acceptance evidence"; no name, phone or email in DOM | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): retention.test U-B4 (3) |
| U-B5 | ErasePersonalDataPanel confirmation | Lists erased and kept with period; names customer once; requires channel and identity check | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): retention.test U-B5 (2) |
| U-B6 | Panel for anonymised customer; CustomerRecordMerged | "erased on {date}" state; merged message with link | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): retention.test U-B6 (2) |
| U-B7 | ReapplyAfterRestoreForm with 501 ids; malformed ids | Blocked before submit, with counts | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): retention.test U-B7 (4) |
| U-B8 | RecordRestoreTestForm: valid drill JSON; rubbish | Fields filled and editable; clear parse error that keeps the paste | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): operations.test U-B8 (3) |
| U-B9 | Same form | No RPO, RTO or outcome input; result panel shows the server's values | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): operations.test U-B9 (3) |
| U-B10 | BackupStatusCard with 27-hour-old and fresh backup | Stale as text plus icon / normal | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): operations.test U-B10 (2). ACCEPTANCE follow-up 2026-10-14 (row 114): U-B10 now also asserts the last restore test's achieved RPO/RTO sentence and the no-test sentence; operations.test 18/18 passed. |
| U-B11 | RestoreTestDueCard: never; overdue; due in 40 days | Overdue, overdue, fine | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): operations.test U-B11 |
| U-B12 | WebVitalsSummary rows | Pass / Fail / Not enough data as text; budget beside p75 | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): operations.test U-B12 (2) |
| U-B13 | Every admin screen with a 403 | PermissionDeniedState, no partial data | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): permission-denied.test (8 screens) |
| U-X1 | Every new form: a server 400 | Mapped to its field; errorCode not in DOM | BLOCKED | Only RecordRestoreTestForm has a server-400-to-field test (operations.test U-X1). The erase panel, reapply form, run-now dialog and device settings have none. Unblock: add one 400 test per form (backlog row 98). |
| U-X2 | Storage audit after the component suite | localStorage and sessionStorage empty; IndexedDB only snapshot and device; no secret | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): store-and-sync U-X2 |
| U-X3 | web-vitals payload for /shop/abc-123 | routeTemplate /shop/[slug]; no URL, query, user or session field | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): web-vitals.test U-X3 (2) |
| U-X4 | sendTelemetry options | credentials omit, keepalive true, no X-CSRF-Token | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): web-vitals.test U-X4 (2) |
| U-X5 | Reporter on a (staff) route | Sends nothing | PASS | Full vitest run (232 files) and a verbose run of the four XCT dirs (12 files, 131 tests, 0 failed): web-vitals.test U-X5 and reporter component test |

## End-to-end cases (handover section 16)

Two runs where the handover says so: MSW (`xct-offline`, `xct-operations`; `next dev`, PORT 3113) and real API
(`xct-real-api`; production build, API 5113, DB catecommerce_xct002e2e). Each row names the run.

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| E-A1 (MSW) | Reception opted in, snapshot, go offline | Within 30 s banner; list stays; actions disabled with reason | PARTIAL | Corrected 2026-10-14 (ACCEPTANCE row 110): xct-offline E-A1/E-A2/E-A6 asserts the banner and goes straight to the board; it never asserts that the front desk keeps today's list or that its actions are disabled with a reason. The front desk's check-in and walk-in buttons are not disabled (they fail fast, XCT row 65); owner accepted as built 2026-10-14 (row 108; APT.md W21). Shared controls: U-A7. |
| E-A1 (real) | Same against the real API | Same | PARTIAL | Corrected 2026-10-14 (row 110): xct-real-api "E-A1/E-A4/E-A5/E-A6" passed, but asserts the board heading and zero buttons, not the front desk's list or disabled actions; same owner acceptance as the MSW row. |
| E-A2 (MSW) | Open the board from the banner | Both lists read-only with saved-at | PASS | MSW run: xct-offline + xct-operations, chromium, PORT 3113, workers=1, next dev: 14 passed. xct-offline E-A1/E-A2/E-A6 (board opened while online, then the connection dropped) |
| E-A3 (MSW) | Try a write while offline | Blocked before any request; nothing queued | PASS | MSW run: xct-offline + xct-operations, chromium, PORT 3113, workers=1, next dev: 14 passed. xct-offline E-A3 |
| E-A4 (real, prod) | Reload while offline | Service worker serves the shell; board renders from IndexedDB | PASS | Real run: xct-real-api, production build, API 5113, DB catecommerce_xct002e2e, XCT_PROD_BUILD=1: "E-A1/E-A4/E-A5/E-A6" passed (worker registered, offline reload served the board) |
| E-A5 (real, prod) | Navigate directly to /staff/front-desk while offline | Redirected to the offline board | PASS | Real run: xct-real-api, production build, API 5113, DB catecommerce_xct002e2e, XCT_PROD_BUILD=1: same test (direct visit to /staff/front-desk offline ended on /staff/offline?from=) |
| E-A6 (MSW) | setOffline(false) | "Back online"; queries refetch | PASS | MSW run: xct-offline + xct-operations, chromium, PORT 3113, workers=1, next dev: 14 passed. xct-offline E-A1/E-A2/E-A6 |
| E-A6 (real) | Same against the real API | Same | PASS | Real run: xct-real-api, production build, API 5113, DB catecommerce_xct002e2e, XCT_PROD_BUILD=1: same real test |
| E-A7 (MSW) | Offline, idle past the timeout | Board locks; lock saved; input does not unlock | PASS | MSW run: xct-offline + xct-operations, chromium, PORT 3113, workers=1, next dev: 14 passed. xct-offline E-A7 (lock saved on the device; input does not unlock) |
| E-A7 (real, prod) | Lock, reload offline, still locked; signing in lifts it | Locked until sign-in | PASS | Real run: xct-real-api, production build, API 5113, DB catecommerce_xct002e2e, XCT_PROD_BUILD=1: "SECURITY lock, reload offline, still locked; signing in again lifts it" passed |
| E-A8 (MSW) | Sign out then open /staff/offline | "No offline copy" | PASS | MSW run: xct-offline + xct-operations, chromium, PORT 3113, workers=1, next dev: 14 passed. xct-offline E-A8 |
| E-A8 (real) | Same; store wiped, no token readable | Same | PASS | Real run: xct-real-api, production build, API 5113, DB catecommerce_xct002e2e, XCT_PROD_BUILD=1: "E-A8/E-X3" passed |
| E-A9 (MSW) | Staff B signs in on A's device | A's snapshot removed before B's is written | PASS | MSW run: xct-offline + xct-operations, chromium, PORT 3113, workers=1, next dev: 14 passed. xct-offline E-A9 |
| E-A9 (real) | Same against the real API (person change) | Same; needs a second seeded staff account | PASS | New test added by this gate (the real-API spec had none). Run alone after a 70 s wait: 1 passed. In the full-file run it hit the 429 sign-in limit (11 sign-ins), see backlog row 99. Not mutation-checked on the real stack. |
| E-A10 (MSW) | Opt-in off | No snapshot request, no worker, no board link | PASS | MSW run: xct-offline + xct-operations, chromium, PORT 3113, workers=1, next dev: 14 passed. xct-offline E-A10 |
| SEC-1 (MSW) | Cold load of /staff/offline while the server answers | Goes to the staff app; no data | PASS | MSW run: xct-offline + xct-operations, chromium, PORT 3113, workers=1, next dev: 14 passed. xct-offline SECURITY cold load |
| SEC-1 (real) | Same against the real API | Same | PASS | Real run: xct-real-api, production build, API 5113, DB catecommerce_xct002e2e, XCT_PROD_BUILD=1: "SECURITY a fresh load of the board..." passed |
| E-B1 | Lab LCP budget over storefront routes and /portal | Median LCP <= 2500 ms | FAIL (other owners) | ACCEPTANCE follow-up 2026-10-14 (integrator, XCT row 105): `npm run perf:lcp`, `next build` with NEXT_PUBLIC_USE_MOCKS=false and NEXT_PUBLIC_API_BASE_URL=http://localhost:5113, `next start` on PORT 3113 (PERF_SERVER=start), API `dotnet run` on 5113, DB catecommerce_xct002e2e; /checkout and /portal signed in as a throwaway-seeded customer with 3 cats, 3 orders and 2 invoices (portal page checked: all three sections render). Medians: `/` 2964 ms (re-run 2736 of 2772, 2900, 2720, 2736, 2696) OVER; `/shop` 1260 (1516, 1360, 1084, 1092, 1260) ok; `/shop/products/[slug]` 2548 (2548, 2600, 2472, 2392, 2744) OVER; `/cart` 2464 (2624, 2748, 2236, 2464, 2224) ok, 36 ms headroom; `/checkout` 1072 (1212, 1060, 1064, 1120, 1072) ok; `/portal` 1436 (1436, 1308, 1560, 1740, 1328) ok. The spec exits non-zero as designed; the budget was not relaxed. `/` and `/shop/products/[slug]` are SHP's (`/` is ADR-FE-029's client redirect to /shop after two session checks; the sample slug `royal-canin-indoor-adult` is not in the seeded database, so the product row measures the not-found page; the seeded real product page measured 860 ms median by the same method, outside the spec). Filed as SHP.md row 73 (ADR-FE-148); /portal meets HO-POR-002 A13. |
| E-B2 | First-load JS budget (`npm run perf:js` after build) | Every budget route <= 170 KB gz; table attached | FAIL | `node scripts/check-js-budget.mjs` after next build: / 150.0 and /shop 160.3 ok; /shop/products/[slug] 203.6, /cart 207.3, /checkout 241.9, /portal 194.2 OVER 170 kB; the script exits non-zero as designed. Owned by SHP/ORD/POR (ADR-FE-148, backlog row 68); not a gate failure for this handover. |
| E-B3 | 360 px sweep over every route (235) | No horizontal scroll at 360/768/1024/1440 | PASS | tests/perf viewport-sweep, next dev + MSW, 360/768/1024/1440: public 19, customer 31, staff 183, none 2 = 235 passed, 0 failed (staff 36.5 min) |
| E-B4 (MSW) | Storefront samples, performance page | p75 rows; "Not enough data" below 50 | PASS | MSW run: xct-offline + xct-operations, chromium, PORT 3113, workers=1, next dev: 14 passed. xct-operations E-B4/E-B5 |
| E-B4 (real) | Same against the real API | Samples listed, no verdict | PASS | Real run: xct-real-api, production build, API 5113, DB catecommerce_xct002e2e, XCT_PROD_BUILD=1: "E-B4/E-B5" passed |
| E-B5 (MSW) | Telemetry request inspection | No cookie, no CSRF, no URL/id/user in body | PASS | MSW run: xct-offline + xct-operations, chromium, PORT 3113, workers=1, next dev: 14 passed. xct-operations E-B4/E-B5 (no cookie, no CSRF, no URL/id/user in body) |
| E-C1 (MSW) | Administrator erases a customer | Confirmation names them; placeholder; ledger lists it | PASS | MSW run: xct-offline + xct-operations, chromium, PORT 3113, workers=1, next dev: 14 passed. xct-operations E-C1/E-C2 |
| E-C1 (real) | Same against the real API | Same | PASS | Real run: xct-real-api, production build, API 5113, DB catecommerce_xct002e2e, XCT_PROD_BUILD=1: "E-C1/E-C2" passed. The audit-log cross-check (HO-ADM-002) is not in the spec; see gaps. |
| E-C2 | Ledger detail | Scopes with statuses; held scope; no personal data | PASS | MSW xct-operations E-C1/E-C2 and real "E-C1/E-C2": ledger holds no personal data |
| E-C3 (MSW) | Run retention now, run detail | Confirm text; nothing clinical deleted | PASS | MSW run: xct-offline + xct-operations, chromium, PORT 3113, workers=1, next dev: 14 passed. xct-operations E-C3/E-C4: confirm text, run detail. The cat cross-check is not in the spec (gap). |
| E-C3 (real) | Same against the real API | Same | PASS | Real run: xct-real-api, production build, API 5113, DB catecommerce_xct002e2e, XCT_PROD_BUILD=1: "E-C3/E-C4" passed after the spec fix to wait for the POST (before the fix it failed once, no runs table, backlog row 100). The cat cross-check is not in the spec (gap). |
| E-C4 (MSW) | Schedule; change retention.clinicalYears; return | New period; Review only unchanged | BLOCKED | No spec changes retention.clinicalYears. xct-operations "E-C3/E-C4" only checks the schedule text. Unblock: a case that edits the setting and returns (backlog row 101). |
| E-C4 (real) | Same against the real API | Same | BLOCKED | Same: the real spec checks "Review only" for Clinical records but never changes the setting. |
| E-C5 (MSW) | Reapply with three ids | Four counts | PASS | MSW run: xct-offline + xct-operations, chromium, PORT 3113, workers=1, next dev: 14 passed. xct-operations E-C5 |
| E-C5 (real) | Same; counts match the API | Same | PASS | Real run: xct-real-api, production build, API 5113, DB catecommerce_xct002e2e, XCT_PROD_BUILD=1: "E-C5" passed (four counts match the API) |
| E-D1 (MSW) | Stale backup, no restore test | Both visible; overdue banner on admin routes | PASS | MSW run: xct-offline + xct-operations, chromium, PORT 3113, workers=1, next dev: 14 passed. xct-operations E-D1 (banner on admin routes) |
| E-D1 (real) | Fresh database | Both problems visible | PASS | Real run: xct-real-api, production build, API 5113, DB catecommerce_xct002e2e, XCT_PROD_BUILD=1: "E-D1/E-D2" passed on a freshly migrated database |
| E-D2 (MSW) | Paste drill JSON | 201; server-computed RPO/RTO/outcome; failing drill Failed | PASS | MSW run: xct-offline + xct-operations, chromium, PORT 3113, workers=1, next dev: 14 passed. xct-operations E-D2; U-B9 "a failing drill shows Failed" |
| E-D2 (real) | Same against the real API | Same | PASS | Real run: xct-real-api, production build, API 5113, DB catecommerce_xct002e2e, XCT_PROD_BUILD=1: "E-D1/E-D2" passed (server-computed RPO/RTO/outcome) |
| E-X1 | axe over every route (full 235-route sweep) | Zero serious/critical | PASS | tests/a11y app-sweep, next dev + MSW: public 19, customer 31, staff 183, none 2 = 235 passed, 0 serious/critical (staff 22.0 min). Corrected 2026-10-14 (row 110): at /staff/offline the sweep sees only the "Checking the connection…" loader; the board and banner states are the next row. |
| E-X1 (board states) | axe over the offline board with data, locked, refusal, and the banner offline (with and without a copy), degraded and recovered | Zero serious/critical | PASS | ACCEPTANCE follow-up 2026-10-14 (row 106): new `web/tests/e2e/xct-offline-a11y.spec.ts`, MSW `next dev`, chromium, PORT 3113, --workers=1: 4 passed (2.1 min), 7 states, 0 serious/critical (wcag2a/2aa/21a/21aa, the sweep's tags). The refusal state is "saved by someone else" (the stored copy is re-attributed in IndexedDB while the server is down). |
| E-X2 | Keyboard-only pass over the offline board and new admin screens | Operable, visible focus | BLOCKED | No keyboard-only test exists for the offline board or the new admin screens (only one Space press in xct-offline). Nothing was run. Unblock: a keyboard spec (backlog row 102). |
| E-X3 (MSW) | Storage audit | localStorage empty; only sanctioned IndexedDB; session cookie unreadable | PASS | MSW run: xct-offline + xct-operations, chromium, PORT 3113, workers=1, next dev: 14 passed. xct-offline E-X3 |
| E-X3 (real) | Same | Same | PASS | Real run: xct-real-api, production build, API 5113, DB catecommerce_xct002e2e, XCT_PROD_BUILD=1: "E-A8/E-X3" passed (cookie, localStorage, sessionStorage, one IndexedDB database) |

## Regression cases from earlier gates

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| R1 | ARCHITECTURE H1 | Ping loop duplicated itself on each failure | connectivity.test "one ping loop" (10-minute outage count, none after stop) | PASS | connectivity.test "one ping loop" passed; M3 (clearTimer removed) fails it |
| R2 | ARCHITECTURE H2 | drillOutput made up from JSON | operations.test "drillOutput is the pasted drill text" | PASS | operations.test H2 (4 tests) passed; M5 (requirement removed) fails it |
| R3 | SECURITY HIGH | Board bypass by reload / cold load | board-security cold-load, persisted-lock and opens-locked cases | PASS | board-security cold-load/persisted-lock cases passed; xct-offline SECURITY and real SECURITY passed |
| R4 | CODE_REVIEW HIGH | Stale recoveredAt bounced the board | board-security "second outage in one page load" | PASS | board-security second-outage case passed; M4 fails it |
| R5 | ARCHITECTURE re-check HIGH | Online idle lock fed the offline board | online-lock.test (activity gate, lockOfflineBoard call, outage after online lock opens locked) | PASS | online-lock.test 3 tests passed; M1 and M2 each fail it |
| R6 | Re-check MEDIUM | Any sync lifts the board lock; failed sync leaves it | board-security sync-lift and failed-sync cases | PASS | board-security sync-lift and (strengthened) failed-sync cases passed |
| R7 | Owner decision 2026-10-14 | No customer phone on the device (stripped on write and on read) | store-hardening phone cases; online-lock "records written before phone numbers were dropped" | PASS | store-hardening phone cases and online-lock cleaning case passed; M6 fails 3 tests |
| R8 | CODE_REVIEW LOW | AbortError counted as unreachable | store-hardening abort cases | PASS | store-hardening abort cases (2) passed |
| R9 | Re-check 2 MEDIUM | "No activity while locked" cannot fail (60 s throttle) | online-lock.test strengthened: advance past 60 s before the events; M2 must now fail | PASS | Test changed (clock faked before mount, +5 min before the events). With `&& !locked` removed it now fails (M2). With the code intact: 3 passed. |
| R10 | Re-check 2 LOW | stop() during in-flight ping: fake timers installed too late | connectivity.test: fake timers before startMonitor(); M8 must now fail | PASS | Test changed (fake timers before startMonitor). With both `!running` guards removed it fails (M8); each guard alone is redundant and survives. |
| R11 | Re-check 2 LOW | Failed-sync test proves nothing | board-security: network failure, assert toBe(staffAccountId) | PASS | Test changed (HttpResponse.error(); asserts offlineEnabled true and toBe(staffAccountId)); passes with the code intact |
| R12 | Re-check 2 LOW | Future lastActivityAt never reaches isBoardLocked in tests | New unit assertion on isBoardLocked with a future instant; M9 must fail | PASS | New isBoardLocked unit assertion (3 h and 5 min ahead locked; 30 s skew and recent past unlocked; 31 min idle locked). With the future check removed it fails (M9). |
| R13 | Owner decision 2026-10-14 | Recheck stays at 3 s after a failure | connectivity tests pin the 3 s recheck | PASS | The ten-minute outage count (35 to 48 pings) and the "3 s re-check then 15 s" test pass; not mutation-tested for the delay value |
| R14 | CODE_REVIEW MEDIUM | SW treated slow navigation as offline | sw.js falls back only on a rejected fetch (M7) | PASS | New sw-navigation.test.ts (4 tests): a 10 s navigation is answered by the network, a rejection redirects, the shell is served from cache, /api/ and sign-in untouched. M7 fails it. |

## Mutation spot-checks

Each mutation is applied to production code, the targeted tests run, and the file restored (git status clean after each).
PASS means the suite caught it (a test failed). A surviving mutation is a FAIL of the suite.

| ID | Mutation | Expected | Status | Evidence |
|---|---|---|---|---|
| M1 | Remove the lockOfflineBoard call | A test fails | PASS | Removed `void lockOfflineBoard(staffAccountId)`: 2 failed (online-lock.test x2), 117 passed. Restored. |
| M2 | Remove `&& !locked` from the activity gate (after R9) | A test fails | PASS | Removed `&& !locked`: with the strengthened test 1 failed (online-lock "records activity while unlocked..."). Restored. (The re-check reported it survived the old test; I did not re-run the old test.) |
| M3 | Remove scheduleNext's clearTimer | A test fails | PASS | Removed clearTimer() at the top of scheduleNext: 2 failed (ping count over an outage; healthy stretch). Restored. |
| M4 | Remove the second-outage baseline | A test fails | PASS | useRef(recoveredAt) -> useRef(null): 1 failed (second outage). Restored. |
| M5 | Remove the drillOutput requirement | A test fails | PASS | Removed the empty-paste check: 1 failed (operations "refuses to submit without a paste"). The other mutation, restoring `pasted // JSON.stringify(values)`, survives (25 passed) because the earlier check makes it unreachable: an equivalent mutant, not a test gap. Restored. |
| M6 | Remove withoutCustomerPhones | A test fails | PASS | withoutCustomerPhones returns its input: 3 failed (store-hardening x2, online-lock). Restored. |
| M7 | sw.js: reject-only fallback replaced by a timeout fallback | A test fails | PASS | Fallback made timeout-based (Promise.race with 4 s): the suite as delivered passed 79/79 (SURVIVED: a test gap in sw.js). I added sw-navigation.test.ts; with it the mutation fails (1 failed). Restored. |
| M8 | Remove the `!running` guards in the monitor (after R10) | A test fails | PASS | Removed both `!running` guards (scheduleNext, checkAndContinue): connectivity.test "stop() while a ping is in flight" fails with the strengthened test. Removing either guard alone survives (12 passed): they cover each other. Restored. |
| M9 | Remove the future-time check in isBoardLocked (after R12) | A test fails | PASS | Removed the future-time check in isBoardLocked: 1 failed (new isBoardLocked test), 78 passed. Restored. |

## Whole-suite and tooling runs

| ID | Run | Expected | Status | Evidence |
|---|---|---|---|---|
| T1 | `tsc --noEmit` | 0 errors | PASS | `npx tsc --noEmit` clean (after my test changes) |
| T2 | `eslint . --max-warnings 0` | 0 problems | PASS | `npx eslint . --max-warnings 0` clean |
| T3 | `vitest run --maxWorkers=2` (full); known flakes rerun alone | All pass | PASS | Full run: 232 files, 1648 tests, 2 failed: goods-receipt-form U-C6 and staff-booking-slots "SlotNoLongerAvailable" (neither in this area). Alone: staff-booking-slots 2/2 passed; goods-receipt-form failed once alone (6/7), then 7/7 twice. shared-pieces did not fail. See backlog row 103. |
| T4 | `next build` | Succeeds | PASS | `next build` (mocks) compiled in 57 s, lint and types clean; a second build with NEXT_PUBLIC_USE_MOCKS=false also compiled |
| T5 | MSW Playwright xct-offline, xct-operations (workers=1, PORT 3113) | All pass | PASS | 14 passed (4.5 min) chromium, PORT 3113, workers=1 |
| T6 | MSW Playwright regression: staff-auth, staff-customers, appointments-frontdesk, admin-functional | All pass | PASS | 20 passed (4.5 min): admin-functional 9, appointments-frontdesk 3, staff-auth 6, staff-customers 2 |
| T7 | xct-real-api on a production build | All pass or documented skips | PASS | Final clean run on a freshly migrated DB: 10 passed, 1 failed (E-A9 only because of the 429 sign-in limit); E-A9 alone passed after 70 s. Earlier runs: 401 (my seed hash was wrong, fixed), and one E-C3 failure (spec race, fixed). |
| T8 | Full 235-route 360 px sweep | No overflow | PASS | 235 routes x 4 widths, 0 failed |
| T9 | Full 235-route axe sweep | Zero serious/critical | PASS | 235 routes, 0 failed |

## Acceptance-criteria coverage (handover section 18)

| Criterion | Covered by | Status |
|---|---|---|
| A1 lists visible offline, read-only | U-A10, U-A12, E-A1, E-A2 | PASS |
| A2 survive reload offline | E-A4, E-A5 (real, prod) | PASS |
| A3 offline announced, writes disabled, nothing lost or queued | U-A7, U-A8, U-A9, E-A3, E-A1 | PASS (owner): shared controls disabled (U-A7); the front desk's own buttons fail fast, accepted as built by the owner 2026-10-14 (row 108) |
| A4 refresh on return | U-A15, E-A6 | PASS |
| A5 bounded, person-bound, wiped | U-A11, U-A14, U-A16, E-A7..E-A10, R5, R9, M1, M2 | PASS |
| A6 never reaches customers or breaks mock/dev | U-A17, M7 | PASS |
| A7 LCP measured in the field, p75 per route | U-X3, E-B4 | PASS |
| A8 lab budget fails the build | E-B1, E-B2 | PASS for the mechanism: both budgets exit non-zero on other-slice routes (E-B1 `/` and `/shop/products/[slug]`; E-B2 four routes), filed to SHP/ORD/POR (row 109) |
| A9 360 px | E-B3, T8 | PASS |
| A10 telemetry anonymous | U-X4, U-X5, E-B5 | PASS |
| A11 erasure anonymises and appears in the ledger | U-B5, U-B6, E-C1, E-C2 | PASS |
| A12 nothing clinical deleted | U-B1, U-B3, E-C3, E-C4 | PARTIAL (U-B1, U-B3 pass; E-C3 pass without the cat cross-check; E-C4 BLOCKED) |
| A13 backup health visible | U-B10, E-D1 | PASS |
| A14 restore test recordable; overdue unmissable | U-B8, U-B9, U-B11, E-D2, M5 | PASS |
| A15 zero serious/critical axe | E-X1, E-X1 (board states), E-X2, T9 | PASS for axe (E-X1 and the board states); E-X2 keyboard BLOCKED (MEDIUM, row 102) |
| A16 errors say what happened | U-X1, U-A8 | PARTIAL (U-A8 pass; U-X1 BLOCKED for four forms) |
| A17 only sanctioned persistence | U-X2, E-X3, R7, M6 | PASS |

## Cases I could not run

| ID | Why | What would unblock it |
|---|---|---|
| U-X1 | Only one of the new forms has a server-400 test | One 400 test per form (backlog row 98) |
| E-C4 (MSW, real) | No spec edits `retention.clinicalYears` | A case that edits the setting and returns (row 101) |
| E-X2 | No keyboard-only test exists | A keyboard spec over the offline board and the new admin screens (row 102) |

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| E-C1 audit-log cross-check (HO-ADM-002 shows the staff action) | Not in either spec | XCT-002 builder |
| E-C3 cross-check that nothing clinical is deleted (a cat created by the test) | Not in either spec; the run only shows its pass results | XCT-002 builder |
| E-A9 on the real stack was not mutation-checked | A mutation needs a production rebuild (2.5 min); the MSW case and the unit case cover the wipe | XCT-002 builder |
| M8/M5 equivalent mutants | Each `!running` guard alone is redundant with the other; `pasted \|\| JSON.stringify(values)` is unreachable behind the empty-paste check. Nothing to test; the dead fallback could be removed | XCT-002 builder |
| E-B2 over budget | Four storefront and portal routes exceed 170 kB (ADR-FE-148) | SHP, ORD, POR owners (row 68) |
| The real-API spec cannot run all its tests in one go | Each test signs in; 11 sign-ins in a window hit the 429 limit | XCT-002 builder (row 99) |
