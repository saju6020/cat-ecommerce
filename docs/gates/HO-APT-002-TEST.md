GATE: TEST
HANDOVER: HO-APT-002
COMMITS: code head bb183b8 (branch ho/APT-002); HEAD at task start eeaa4f1 (docs and gate records only)
DATE: 2026-10-05
STATUS: PASS

Test-case sheet: docs/test-cases/TC-HO-APT-002.md, rewritten before any run with every row NOT RUN, then
set one row at a time from what was observed. 76 rows: 76 PASS (several with notes), 0 FAIL, 0 BLOCKED,
0 NOT RUN. The real-API column (16 cells) is "not run — no API". Gate rule (owner, 2026-09-27): only
BLOCKER or HIGH fail the gate; there are none.

SUITES (web/, PORT=3102, --workers=1, one dev server at a time)
- npx tsc --noEmit: exit 0.
- npm run lint: exit 0, no warnings.
- npx vitest run --maxWorkers=3: 109 files, 477 tests, all passed. (CODE_REVIEW reported 108 / 501; not reproducible, 109 test files exist on disk.)
- Playwright chromium, every spec except appointments-a11y-responsive and real-api: 406 tests, 401 passed, 5 failed.
  Failures and reruns on a fresh server:
  - appointments-calendar E-E1 (drag to free) and E-E1 (drag, busy target): FAILED in the full run; passed 6/6 in an isolated run (repeat-each=2) and again in a 27-test sequence rerun. Original and rerun both recorded.
  - files-upload C1/E1 and staff-customers E-H1: failed in the full run on locator timeouts; passed on rerun (cold-compile or long-lived-server timeouts).
  - cat-vaccinations E9: failed in the full run and the rerun. Pre-existing: it also fails on master cffaa79 (checked in a scratch worktree, E6/E7/E8/E10 pass, E9 fails identically; spec and cat mocks have no diff to this branch). Not counted against this branch.
- Playwright chromium, appointments-a11y-responsive alone: 73 passed.
- Playwright mobile-360, appointments-{booking,calendar,frontdesk,staff}: 13 passed, 1 skipped (drag below 768 px, by design).
- Playwright mobile-360, appointments-a11y-responsive alone: 72 passed, 1 failed (first test, fixture sign-in returned 404 on a cold server); rerun on a warm server passed.
- Port 3102 free at the end; the scratch master worktree was removed.

SPECIFIC VERIFICATIONS
- U-A6 / E-A3 countdown and expiry (page.clock): watched pass on both projects; unit test asserts 01:40 then 01:30 on a +90 s server clock.
- E-A2 lost race: exact message "Someone has just taken that time. Please choose another.", list focused, taken slot removed, recovery to a hold. Passed on both projects (MSW race only).
- Calendar drag on a wide viewport: passes (real pointer events), with the intermittent full-run failure noted below.
- Front-desk transitions (E-D3, U-D4): pass.
- CODE_REVIEW claim (Move dialog resends a stale version after a 409; U-E2 does not exercise it): CONFIRMED with a temporary probe test (now deleted). After a genuine StaleCalendarVersion 409 the calendar is refetched but both POST bodies carry expectedVersion 1 while the server is at 2; the assertion that the second differs failed. Reported, not fixed.

FINDINGS
- [MEDIUM] web/components/calendar/MoveDialog.tsx + web/app/(staff)/staff/appointments/CalendarView.tsx:79,104-113 — confirmed (above). The version is captured once when the dialog opens, so a resubmit after a stale 409 resends it and the dialog is a dead end until reopened. Already on the CODE_REVIEW list; this gate adds the observation.
- [MEDIUM] web/tests/components/calendar/calendar.test.tsx:259-290 (U-E2) — passes while the behaviour is broken; asserts only the message. Add the refetch and second-submit assertions (and a drop-path variant) together with the fix.
- [MEDIUM] web/tests/e2e/appointments-calendar.spec.ts:38,94 — both drag tests failed once in a 29-minute full chromium run and passed on 8 further executions. Not diagnosed. Make the drag helper wait for the grid to settle and keep a trace on retry.
- [LOW] web/tests/e2e/appointments-calendar.spec.ts:38-60 — on mobile-360 the first E-E1 test asserts only the page heading, so it passes without testing anything; make the narrow branch drive the Move… dialog.
- [LOW] web/components/appointments/StaffBookingForm.tsx:305 — React logs "two children with the same key" (`startsOn|roomId`) for many slots during U-C2. Slots from different vets in one room at one time share a key, which can drop or duplicate options. Include performerId in the key and in the select value.
- [LOW] web/tests/e2e/appointments-frontdesk.spec.ts E-D2 — takes 33-34 s, past the 30 s default; it passed, so a per-test timeout is set, but it is close to unusable on a slower machine.
- [LOW] web/tests/e2e/appointments-a11y-responsive.spec.ts first test on a cold server — the sign-in fixture got a 404 before MSW was ready (cold-start race, passed warm). Same class as the fixture note in fixtures.ts.
- [LOW] CODE_REVIEW record — its vitest counts (108 files, 501 tests) do not match this run (109 files, 477 tests).

SCOPE CHECK
- files in allowed scope: this gate changed docs/test-cases/TC-HO-APT-002.md and docs/gates/HO-APT-002-TEST.md only
- files outside allowed scope: none. No product code was changed and no test was weakened. The temporary probe test was deleted.
