GATE: TEST
HANDOVER: HO-XCT-002
COMMITS: 0b69692a (branch ho/XCT-002) plus the test-only commit made by this gate
DATE: 2026-10-14
STATUS: PASS

No BLOCKER or HIGH finding. One case is FAIL (E-B2) and five are BLOCKED; none of them is a defect in this slice's code and none is HIGH. The sheet is `docs/test-cases/TC-HO-XCT-002.md`. This is the TEST gate's verdict only; the ACCEPTANCE gate decides the handover.

COUNTS (107 rows): PASS 101, FAIL 1, BLOCKED 5, NOT RUN 0.

PRODUCTION CODE: not changed. Only tests, the sheet, the backlog and this record.

TEST CHANGES (re-check 2 points)
- `online-lock.test.tsx`: the clock is faked before the recorder mounts and moved on 5 minutes before the key and mouse events, so the 60 s throttle no longer hides the check. Removing `&& !locked` now fails it (M2).
- `connectivity.test.tsx` "stop() during an in-flight ping": fake timers are enabled before `startMonitor()`. Removing both `!running` guards now fails it (M8). Removing either guard alone does not: they cover each other.
- `board-security.test.tsx` "failed sync leaves the lock": a network failure (`HttpResponse.error()`), asserts the copy is still on and `toBe(staffAccountId)`.
- `board-security.test.tsx`: new `isBoardLocked` assertion (hours and 5 minutes ahead locked, 30 s of skew allowed, 31 minutes idle locked). Removing the future-time check fails it (M9).
- New `sw-navigation.test.ts` (4 tests): the sw.js reject-only fallback. The mutation "give up after 4 s" passed the whole delivered suite (79/79); it now fails (M7).
- `xct-real-api.spec.ts`: new E-A9 real-API case (the spec had none), `signIn` takes an account, and the E-C3 case waits for the run POST before leaving the page (it failed once on a fresh database).

MUTATION SPOT-CHECKS (each restored; `git status` clean of production changes)
- lockOfflineBoard call removed: caught (2 failed).
- activity gate removed: caught after the strengthening (1 failed).
- scheduleNext clearTimer removed: caught (2 failed).
- second-outage baseline removed: caught (1 failed).
- drillOutput requirement removed: caught (1 failed). The `pasted || JSON.stringify(values)` variant survives; it is an equivalent mutant (unreachable behind the empty-paste check).
- withoutCustomerPhones neutered: caught (3 failed).
- sw.js reject-only fallback replaced by a timeout: SURVIVED the delivered suite, caught by the new test.

RUNS
- tsc clean; eslint clean (`--max-warnings 0`); `next build` compiled (mocks on, and mocks off against API 5113).
- Full vitest: 232 files, 1648 tests, 2 failed, both outside this area: `inventory/goods-receipt-form` U-C6 and `appointments/staff-booking-slots` "SlotNoLongerAvailable". Rerun alone: staff-booking-slots 2/2; goods-receipt-form failed once more alone, then passed twice. `shared-pieces` did not fail. The four XCT directories alone: 12 files, 131 tests, 0 failed.
- MSW Playwright (PORT 3113, workers=1, chromium): xct-offline and xct-operations 14/14; staff-auth, staff-customers, appointments-frontdesk, admin-functional 20/20.
- Real API (production build, API 5113, DB `catecommerce_xct002e2e` recreated and migrated to head for the final run, never dev or test): 10 of 11 in one run; the eleventh (new E-A9) got `429` on sign-in and passed alone after 70 s. Includes the board, lock, offline reload, wipe, erasure, retention, restore-test and web-vitals cases.
- Sweeps, once each over the inventory (235 routes: public 19, customer 31, staff 183, none 2): axe 235/235 with zero serious or critical; 360/768/1024/1440 px 235/235 with no overflow or clipped control.
- `perf:js` (E-B2): `/` 150.0 kB and `/shop` 160.3 kB pass; `/shop/products/[slug]` 203.6, `/cart` 207.3, `/checkout` 241.9, `/portal` 194.2 are over 170 kB. Owned by SHP, ORD and POR (ADR-FE-148, backlog row 68).

FINDINGS (none blocking; all in docs/backlog/XCT.md)
- [MEDIUM] row 98 — only RecordRestoreTestForm has a server-400 test (U-X1); four other new forms have none. U-X1 is BLOCKED.
- [MEDIUM] row 101 — E-C4 (change `retention.clinicalYears`, return) is in no spec. BLOCKED.
- [MEDIUM] row 102 — E-X2 keyboard-only pass has no test; criterion A15 cites it. BLOCKED.
- [LOW] row 99 — the real-API spec makes 11 sign-ins and cannot pass in one run under the sign-in limit.
- [LOW] row 100 — the E-C3 race in the real-API spec. Resolved in this gate.
- [LOW] row 103 — two flaky tests outside this area (goods-receipt-form, staff-booking-slots).
- [LOW] row 104 — E-C1 audit-log cross-check and E-C3 cat cross-check are not in the specs.

NOT OBSERVED BY THIS GATE
- E-B1 (LCP lab budget): not re-measured. The builder's figures are in git at 0b69692a; the handover defers storefront rows to HO-SHP-002. BLOCKED on the sheet.
- E-A9 on the real stack was not mutation-checked.

CHECKED, NO FINDING
- Every re-check 2 mutation (G1 to G7) is now caught by the suite except the redundant single `!running` guards and the unreachable `pasted ||` fallback, which are equivalent mutants.
- The 3 s recheck after a failure (owner decision) is what the ping-count tests expect.
- The phone-number removal (owner decision) holds: stripped on write and on read, and the opt-in text no longer mentions phones.
- No browser, API or dev server left running; .next, test-results, playwright-report and the scratch logs removed. The database `catecommerce_xct002e2e` is left in place (throwaway; drop it after the merge).
