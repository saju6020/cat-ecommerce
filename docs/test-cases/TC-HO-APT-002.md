# TC-HO-APT-002 — Clinic Appointments (frontend)

Handover: docs/handover/HO-APT-002.md (§15 component cases, §16 end-to-end cases, §1 Amended)
Commit(s) under test: code head bb183b8 (branch ho/APT-002; HEAD eeaa4f1 adds only docs and gate records)
Written: 2026-10-04 (TEST gate; every row started NOT RUN, superseding the developer's sheet)  ·  Executed: 2026-10-04 to 2026-10-05

> Statuses record what was **observed** by the TEST gate in this session, not what the developer's
> sheet claimed. `BLOCKED` marks a case that could not be executed. The real-API column is
> "not run — no API": there is no API in this environment (handover §1 Amended item 14).
> Unit rows are PASS because the Vitest file that carries the case (found by its U-ID) passed in the
> full run S3; Playwright rows are PASS because the named test was seen passing in the runs listed.

## Summary

| | Count |
|---|---|
| Total rows (S 7, U 28, E 16 x 2 projects = 32, V 6, R 3) | 76 |
| PASS (incl. PASS-with-note; V5 and V6 are CONFIRMED observations) | 76 |
| FAIL | 0 (the only failing test, cat-vaccinations E9, is pre-existing and confirmed on master) |
| BLOCKED | 0 |
| NOT RUN | 0 (the real-API column, 16 cells, is "not run — no API" and is not counted) |

PASS-with-note rows: S4, S5, E-E1a and E-E1c (chromium: one failure in the full run, pass on rerun), E-E1a mobile-360 (vacuous), U-E2 (weak), V3 (flake). V5 is a confirmed product defect (backlog MEDIUM), not a test failure.

Suite counts, exact:
- `npx tsc --noEmit`: exit 0.
- `npm run lint`: exit 0, no output (`--max-warnings 0`).
- `npx vitest run --maxWorkers=3`: **109 files, 477 tests, all passed** (129 s). CODE_REVIEW reported 108 files / 501 tests; this run counts 109 / 477 on the same 109 `*.test.*` files on disk. Not a failure, but the CODE_REVIEW figures could not be reproduced.
- Playwright chromium, all specs except `appointments-a11y-responsive` and `real-api` (406 tests, one server, 29.0 min): **401 passed, 5 failed**. Failures: two `appointments-calendar` drag tests, `cat-vaccinations` E9, `files-upload` C1/E1, `staff-customers` E-H1. Reruns on a fresh server of those spec files plus the other appointments specs (27 tests): **26 passed, 1 failed (E9)**. The 4 non-E9 failures did not reproduce (see Failures).
- Playwright chromium, `appointments-a11y-responsive` alone: **73 passed, 0 failed**.
- Playwright mobile-360, `appointments-{booking,calendar,frontdesk,staff}`: **13 passed, 1 skipped (drag at 360 px, by design), 0 failed** (14 tests).
- Playwright mobile-360, `appointments-a11y-responsive` alone: **72 passed, 1 failed** (cold-start sign-in 404 on the first test); warm rerun of that test: passed. Net 73/73.
- Port 3102: no listener after the last run.

## Environment

Windows 11, Node via npm, `web/` in the worktree D:/my-repo/cat_ecommerce-wt/APT-002, branch ho/APT-002.
Run with `PORT=3102`, `--workers=1`, one `next dev` at a time (started by Playwright's `webServer` per invocation, or by me once for the warm rerun and killed afterwards). MSW only: no API, no database, no real-API project (`E2E_REAL_API` unset). `web/.env.local` untouched. A scratch worktree of master cffaa79 was created for S7 (node_modules junctioned from this worktree) and removed afterwards; `git worktree list` shows no leftover and this worktree's node_modules is intact.

## Suite-level checks

| ID | Check | Expected | Status | Evidence |
|---|---|---|---|---|
| S1 | `npx tsc --noEmit` in web/ | exit 0 | PASS | TSC_EXIT=0 |
| S2 | `npm run lint` in web/ | exit 0, no warnings | PASS | LINT_EXIT=0, no output |
| S3 | `npx vitest run --maxWorkers=3` | all pass | PASS | 109 files, 477 tests passed, EXIT=0 |
| S4 | Playwright chromium, whole tests/e2e minus a11y-responsive and real-api | all pass except E9 | PASS with note | 401/406 first run; the 4 non-E9 failures passed on rerun (calendar spec 6/6 on a fresh server with repeat-each=2; then 26/27 across appointments + the three other specs, only E9 failing). Both results recorded |
| S5 | Playwright mobile-360 appointments specs, a11y-responsive alone | all pass | PASS with note | 13 passed + 1 skipped; a11y 72/73, 1 cold-start fixture 404, warm rerun passed |
| S6 | Port 3102 free, no dev server left | free | PASS | netstat shows no LISTEN on 3102 after the final kill |
| S7 | cat-vaccinations E9 also fails on master cffaa79 | fails on master | PASS (pre-existing confirmed) | On a scratch worktree at cffaa79: E6, E7, E8, E10 passed, E9 failed with `getByText('Due soon').first()` not found, identical to this branch. The spec and cat mocks have no diff between cffaa79 and HEAD. Not counted against this branch |

## Unit cases (handover §15; Vitest, all in the S3 run)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| U-A1 | One cat, one branch | Steps 1 and 3 skipped | PASS | booking/booking-flow.test.tsx (8 tests passed) |
| U-A2 | Deep link by category | Filtered service step | PASS | booking-flow.test.tsx |
| U-A3 | London slots in London time | Labelled | PASS | booking/format-appointment.test.ts (5) |
| U-A4 | Full day | Waitlist and next day | PASS | booking-flow.test.tsx |
| U-A5 | Hold 409 | Message; refetch; focus | PASS | booking-flow.test.tsx |
| U-A6 | Countdown with server clock +90 s | 01:40 then 01:30, not browser clock | PASS | booking/hold-countdown.test.tsx (4), server-clock.test.ts (2); assertion read: it fails if the offset is ignored |
| U-A7 | Announcements | Exactly 5:00, 1:00, 0:00 | PASS | hold-countdown.test.tsx; asserts the full heard list by sampling each second |
| U-A8 | Countdown at 0 | Back to slots; confirm gone | PASS | booking-flow.test.tsx |
| U-A9 | Back / route change | DELETE sent | PASS | booking-flow.test.tsx |
| U-A10 | Pay at the clinic | No payment control | PASS | booking-flow.test.tsx, leave-and-forms.test.tsx (5) |
| U-A11 | .ics | Escaped, UTC, no HTML | PASS | booking/ics.test.ts (3) |
| U-B1 | canChange true/false | Buttons / contact card | PASS | appointments/my-appointments.test.tsx (9), reschedule-flow.test.tsx (6) |
| U-B2 | Reschedule 409 | Message; time unchanged | PASS | reschedule-flow.test.tsx |
| U-B3 | Waitlist offer | Countdown, confirm, ended | PASS | my-appointments.test.tsx, reschedule-flow.test.tsx |
| U-C1 | No-show badge | Count as text | PASS | appointments/staff-appointments.test.tsx (9) |
| U-C2 | Out-of-hours slot | Marked in text | PASS | staff-appointments.test.tsx (React duplicate-key warnings logged during this test, see Findings) |
| U-D1 | Queue order and badges | As served | PASS | front-desk/queue-and-today.test.tsx (6) |
| U-D2 | Poll while StartDialog open | Values kept | PASS | front-desk/front-desk-view.test.tsx (5) |
| U-D3 | WalkInPanel keyboard-only | Focus returns | PASS | front-desk-view.test.tsx |
| U-D4 | Actions from allowedTransitions | Only those | PASS | queue-and-today.test.tsx, staff-appointments.test.tsx |
| U-E1 | Drop onto busy time | Snap back, toast, live region | PASS | calendar/calendar.test.tsx (8) |
| U-E2 | Drag 409 stale | Stale message; refetch | PASS but WEAK | calendar.test.tsx; asserts only the message text. It does not assert a refetch or a resubmit, which is why V5 passes undetected. Not strengthened (see V5: the fix is product code) |
| U-E3 | Poll during a drag | Applied after drop | PASS | calendar.test.tsx (`useFrozenWhile`) |
| U-E4 | MoveDialog request | Same as a drag | PASS | calendar.test.tsx |
| U-E5 | 360 px agenda | No grid | PASS | calendar.test.tsx |
| U-E6 | Off-shift and closures | Without colour | PASS | calendar.test.tsx |
| U-X1 | Server 400 mapped | Code not in DOM | PASS | appointments/errors.test.ts (4), leave-and-forms.test.tsx, staff-appointments.test.tsx |
| U-X2 | Unsaved-changes confirm | Booking confirm step included | PASS | booking/leave-and-forms.test.tsx (`LeaveHoldGuard`) |

## Integration / end-to-end cases (handover §16; Playwright against MSW)

Real API column: **not run — no API** for every row. chromium rows are from the 406-test run unless noted; mobile-360 from the 14-test and 73-test runs.

| ID | Case | Expected | chromium | mobile-360 | Real API | Evidence |
|---|---|---|---|---|---|---|
| E-A1 | Register, book vaccination, Upcoming, budget | Done page in budget; in Upcoming | PASS | PASS (21.4 s) | not run — no API | appointments-booking.spec.ts. Does not add a cat or check the inbox (V6) |
| E-A2 | Lost race, second context | Clear message; recovery | PASS | PASS | not run — no API | See V2 |
| E-A3 | Hold expiry via page.clock | Expiry message; slot selectable | PASS | PASS | not run — no API | See V1 |
| E-B1 | Reschedule / contact card after cut-off | Moved / card | PASS | PASS | not run — no API | |
| E-B2 | Cancel | Status Cancelled | PASS | PASS | not run — no API | Inbox not asserted |
| E-B3 | Full day, waitlist, claim | Booked (MSW part) | PASS | PASS | not run — no API | Clinic pass not covered |
| E-C1 | Phone booking, no-show badge | Badge; booked | PASS | PASS | not run — no API | appointments-staff.spec.ts |
| E-D1 | Walk-in under 60 s | Card in queue | PASS (8.6 s) | PASS (7.4 s) | not run — no API | |
| E-D2 | Emergency first; estimates rise | As stated | PASS (32.8 s) | PASS (34.4 s) | not run — no API | Close to a 30 s default timeout; the spec must set a larger one (it passed) |
| E-D3 | Check in, start, complete | Card moves | PASS | PASS | not run — no API | See V4 |
| E-E1a | Drag to a free slot (wide) | Dialog then moved | PASS on rerun (FAIL once in the full run) | PASS but vacuous (no drag below 768 px; asserts only the heading) | not run — no API | See V3 |
| E-E1b | Move… dialog busy then free | Refusal kept; moved | PASS | PASS | not run — no API | |
| E-E1c | Drag to busy slot | Snaps back; reason announced | PASS on rerun (FAIL once in the full run) | skipped by design | not run — no API | See V3 |
| E-E2 | Flagged list and banner | Reason; banner | PASS | PASS | not run — no API | Starts from an already flagged mock appointment |
| E-X1 | axe: every route, countdown, move dialog, error state | 0 serious/critical | PASS (73/73 with E-X2) | PASS (72/73 first run; the one failure was a cold-start fixture 404, warm rerun passed) | not run — no API | |
| E-X2 | 360/768/1024/1440 | No h-scroll, no clipped control | PASS | PASS | not run — no API | Same two runs |

## Gate-specific verifications

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| V1 | U-A6 / E-A3: countdown and expiry via page.clock | Timer visible; at expiry message and confirm button gone; slot selectable | PASS | E-A3 passed on chromium and mobile-360 (spec: install clock, hold 11:00, `getByRole('timer')` visible, `fastForward(75_000)`, expiry text visible, `Confirm booking` count 0, 11:00 button visible again). U-A6 asserts 01:40 then 01:30 against a +90 s server clock |
| V2 | E-A2: lost race gives a clear message | Message; list focused; taken slot removed; recovery | PASS | Spec asserts the exact text "Someone has just taken that time. Please choose another.", the list focused, the 10:30 slot gone, then a second time reaches the timer. Passed on both projects. Only MSW's deterministic race, not a true race |
| V3 | Calendar drag on a wide viewport | Pointer drag opens the dialog; free target moves; busy snaps back | PASS with flake note | chromium (1280 px): passed in the isolated run (6/6 with repeat-each=2, cold then warm) and in the sequence rerun. In the 29-minute full run both drag tests failed once (first: dialog text not found after 11.1 s; second: `Move appointment` button not found, 30 s timeout) while the non-drag E-E1b between them passed. Not reproduced in 3 further runs (0 failures of 8 executions). Cause not isolated; consistent with a long-lived dev server (883 MB) or a pointer-timing race in the drag helper |
| V4 | Front-desk transitions | Check in, start, complete; actions only from allowedTransitions | PASS | E-D3 passed on both projects; U-D4 passed |
| V5 | CODE_REVIEW: Move dialog resends a stale version after a 409; U-E2 misses it | Confirm or refute | CONFIRMED | Temporary Vitest probe (deleted; `git status` shows only this sheet changed). Steps: open Move for Tiger, bump Tiger's version to 2 out of band with a direct valid move, submit in the dialog (409 StaleCalendarVersion, message shown, calendar refetched as the probe observed), submit again. The two POST bodies were `expectedVersion: 1` and `expectedVersion: 1` while the server is at 2. The assertion that the second differs from the first FAILED. The dialog is stuck until closed and reopened |
| V6 | E-A1 does not add a cat or check the inbox | Prerequisite gaps stated in Amended 12 | CONFIRMED | Spec read; the developer's earlier sheet said so too. Not a defect |

## Regression cases from earlier gates

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| R1 | ARCHITECTURE | CustomerSessionGuard.tsx edited (owner-approved, b3377df) | Reminder link `?next=` carries the query; sign-in flows keep working | PASS | The existing account/auth specs that exercise CustomerSessionGuard (accounts-auth, account-area, cat-smart-redirect) passed in the 406-test run; E-A1 on both projects signs in and reaches /book |
| R2 | fix bb183b8 | Mocks importing lib/api/client broke api-client-with-branch.test.ts | The test passes | PASS | S3 run: 109/109 files pass |
| R3 | ARCHITECTURE | Nav registry edit (appointments.ts) | Shell and nav specs pass on chromium | PASS | staff-shell-* and nav-registry unit tests passed in S3/S4 |

No MEDIUM/LOW finding of the earlier gates has been fixed (all are on the backlog), so none has a regression row.

## Acceptance-criteria coverage

| Criterion | Covered by | Status |
|---|---|---|
| AC-1 / NFR-USA-1 booking within budget | E-A1 | PASS (MSW only) |
| NFR-USA-2 walk-in under 60 s | E-D1 | PASS (MSW only) |
| Honest countdown on the server clock | U-A6, U-A8, E-A3, V1 | PASS (against the mock `Date` header only; real API lacks Expose-Headers, Amended 10) |
| Lost race gives a clear message | U-A5, E-A2, V2 | PASS (MSW race) |
| No payment step | U-A10 | PASS |
| Actions only from allowedTransitions | U-D4, E-D3 | PASS |
| Calendar move re-validated by server; keyboard path | U-E1, U-E4, E-E1a-c | PASS (drag flake noted) |
| Stale-version recovery on Move | U-E2 | GAP: U-E2 passes while the behaviour is broken (V5) |
| UI-1 360 px, axe | E-X1, E-X2, U-E5 | PASS |
| NFR-USA-5 values survive errors | U-X1, U-D2 | PASS for validation errors; not for hold expiry (CODE_REVIEW MEDIUM, backlogged) |

## Cases I could not run

| ID | Why | What would unblock it |
|---|---|---|
| Real-API column, every E row | Not run — no API | A running HO-APT-001 host with `NEXT_PUBLIC_USE_MOCKS=false` and `E2E_REAL_API=1` |
| E-A1 inbox and add-a-cat; E-B2 inbox; E-B3 clinic pass; E-E2 leave approval | HO-NTF-002 not merged in the worktree; no add-a-cat screen; needs backend | Merge of NTF-002; real API |
| Genuine two-customer same-slot race | MSW's deterministic race only | Real API |

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| Stale-version resubmit has no permanent test | A passing test would need the product fix first; the temporary probe was deleted as instructed | HO-APT-002 builder with the V5 fix |
| Drag (E-E1a/c) is not exercised at 360 px and E-E1a passes vacuously there | Dragging needs the wide grid by design (ADR-FE-026); the Move… dialog covers the path | Builder: make the mobile branch assert the Move… dialog instead of passing on the heading |
| Drag flake in long runs | Not reproduced in 8 further executions | Builder: wait for the grid to settle before the first pointer-down; capture a trace on retry |
| Real-API run of E-A3 (Date header skew) | No API | Integrator (Expose-Headers: Date) |
