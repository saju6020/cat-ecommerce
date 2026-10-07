GATE: TEST
HANDOVER: HO-CAT-002-ADDCAT
COMMITS: 82ae258 (range 578dc79..82ae258); sheet 40f40de
DATE: 2026-10-07
STATUS: PASS

Sheet: docs/test-cases/TC-HO-CAT-002-ADDCAT.md, written with every row NOT RUN before execution.
33 cases: 33 PASS, 0 FAIL, 0 BLOCKED, 0 NOT RUN. All 7 acceptance criteria have a covering case.

Run in web/: `tsc --noEmit` clean; eslint (app components lib tests mocks) clean; full vitest
`--maxWorkers=3` 140 files / 715 tests passed. Slice tests (add-cat-customer, booking-return-path,
cat-step) 26/26 with U-AC1..U-AC11, U-RP1, U-RP2 each seen passing by name.
Playwright (PORT=3105, --workers=1, mocked API): cat-add-customer 6/6 (chromium + mobile-360).
Wide set (cat-*, appointments-*, shop-portal, account-area, both projects): 312 tests, 300 passed,
1 skipped, 11 failed.

MUTATION CHECKS (both reverted with git checkout; tree clean apart from the untouched mockServiceWorker.js)
- safeBookingPath without the post-parse checks: 5 U-RP1 payload tests failed.
- `opening` guard removed: U-AC11 failed.

FAILURE CLASSIFICATION (11 in run 1)
- pre-existing: cat-vaccinations E9, chromium and mobile-360. Fails on master efc107e in every master run too.
- flake (passed on warm rerun): appointments-calendar E-E1 and E-E1 busy (chromium); cat-parasite-control
  (chromium); cat-profile-header E3 (chromium) and E4 (mobile); cat-vaccinations E8 (mobile); cat-a11y
  smart redirect (mobile); cat-add-customer E-AC3 (mobile).
- flake, with a note: shop-portal E-D3 failed in run 1 (chromium) and in the first rerun (both projects),
  but passed on master and passed on the branch in three later runs (isolated; the identical 10-test
  sequence; cat-add-customer plus E-D3). Not reproducible, and this slice touches neither
  /portal/cats/{id} nor the food page.
- regression: none.

FINDINGS
- [MEDIUM] web/tests/e2e/shop-portal.spec.ts:79 — E-D3 failed on 2 consecutive runs on this branch and
  not on master, then passed three times; load- or order-sensitive. Backlog: watch it, and rerun it
  if the integrator sees it fail.
- [MEDIUM] web/tests/e2e/cat-add-customer.spec.ts:55 — E-AC3 failed once on mobile-360 in the 312-test
  run (cold or loaded dev server), passed in every other run. Backlog: consider a more patient wait.
- [LOW] web/app/(portal)/book/BookingFlow.tsx:94 — the `addCatHref` wiring has no BookingFlow-level test
  (CODE_REVIEW M3 was addressed with the helper test U-RP2; the component call is not itself tested).
- [LOW] web/tests/components/booking/cat-step.test.tsx:7 — U-AC9 still passes `addCatHref` in as a prop,
  so it proves rendering only.
- [LOW] cat-vaccinations E9 fails on master and branch alike; it needs its own fix (known).
- [LOW] CODE_REVIEW and ARCHITECTURE LOWs still open: the mock treats a blank ownerId as the customer,
  and `safeBookingPath` runs twice on one value (page.tsx and resolveAddCatReturn).
- [LOW] Playwright runs against the MSW mock only; the live POST /api/cats owner replacement (F19)
  was not exercised here.

SCOPE CHECK
- files in allowed scope: 17/17 (slice files, the two amended rows, gate records, this sheet)
- files outside allowed scope: none; no src/, tests/, .ai/, package.json or lockfile change.
  Uncommitted web/public/mockServiceWorker.js left untouched and not committed.
