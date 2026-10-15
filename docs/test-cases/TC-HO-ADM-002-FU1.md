# TC-HO-ADM-002-FU1 — Branch calendar view (frontend follow-up)

Handover: docs/handover/HO-ADM-002-FU1.md
Branch: `ho/ADM-002-FU1` (worktree `ADM-002-FU1`), baseline `9eb87445`
Written: 2026-10-09 (before the tests, handover §17 step 2)  ·  Executed: see Run log

> Statuses record what was **observed**. `NOT RUN` = not yet executed. `BLOCKED` = could not be
> executed (reason in Evidence). MSW column = Vitest (MSW node server) or Playwright on
> `NEXT_PUBLIC_USE_MOCKS=true`. Real-API column = Playwright on `NEXT_PUBLIC_USE_MOCKS=false`
> against the API from this worktree (E-CAL8 only).

## Unit and component cases (handover §15)

Vitest 5 + Testing Library + MSW node server. "PASS" = observed passing on 2026-10-09 at the commit under test.

| ID | Case | MSW / Vitest | Real API | Evidence |
|---|---|---|---|---|
| U-CAL1 | Model: Dhaka Monday, two server intervals | PASS | n/a | `branch-calendar-model.test.ts` |
| U-CAL2 | Model: public holiday, no intervals | PASS | n/a | same |
| U-CAL3 | Model: 3-day closure, next day unaffected | PASS | n/a | same |
| U-CAL4 | Model: Sunday, weekly closed | PASS | n/a | same |
| U-CAL5 | Model: part-day closure, reduced hours (plus U-CAL5b: part-day covering all hours) | PASS | n/a | same |
| U-CAL6 | Model: server wins (a)(b)(c) | PASS | n/a | same |
| U-CAL7 | Model: zone and DST. Also run with `TZ=America/Los_Angeles`: 12/12 pass | PASS | n/a | same |
| U-CAL8 | Month helpers (bounds, shift, blanks, param with fake clock) | PASS | n/a | same |
| U-CAL9 | `BranchMonthCalendar` semantics (3 tests) | PASS | n/a | `branch-calendar-grid.test.tsx` |
| U-CAL10 | Grid keyboard and focus clamp (5 tests, incl. 31 Jan to 28 Feb and remount restore) | PASS | n/a | same |
| U-CAL11 | Page states: ready, bad month param, links, PageDown push, 403, pending, 500, closures 500, empty hours (9 tests) | PASS | n/a | `branch-calendar-page.test.tsx` |
| U-CAL12 | Closure mutations invalidate calendar (2 tests) | PASS | n/a | `branch-calendar-invalidation.test.tsx` |
| U-CAL13 | `BranchCalendarDayList` | PASS | n/a | grid test file |
| U-CAL14 | Mock parity (8 tests; calendar instants, holiday, London BST/GMT, 62/63 days, unknown branch, closures from/to) | PASS | n/a | `branch-calendar-mock-parity.test.ts` |
| U-CAL15 | Closure reason rendered as text | PASS | n/a | grid test file |

New Vitest cases: 44 in 5 files (8 + 12 + 2 + 13 + 9). Full suite at the final commit: 187 files, 1182 tests, all passed (`--maxWorkers=3`). The existing admin, branches and roster suites passed (19 files, 79 tests) straight after the mock rewrite (step 3), so the seed date move and handler rewrite broke nothing (§20.4 not triggered).

## End-to-end cases (handover §16)

Playwright, `--workers=1`, one fresh `next dev` on PORT 3114 per run, mocks on.

| ID | Case | MSW (chromium) | MSW (mobile-360) | Real API | Evidence |
|---|---|---|---|---|---|
| E-B1 (calendar half) | Holiday skipped, Mon open, Tue weekly closed, zone caption | PASS | n/a (admin-functional is a chromium suite) | NOT RUN (E-CAL8) | `admin-functional.spec.ts` 9/9 |
| E-CAL2 | Seeded main branch Jan/Feb 2027, Next month | PASS | PASS (list item) | NOT RUN (E-CAL8) | `admin-branch-calendar.spec.ts` |
| E-CAL3 | London across DST, browser zone America/Los_Angeles | PASS | PASS | n/a | same |
| E-CAL4 | Keyboard at 1024 px | PASS | PASS (spec sets 1024) | n/a | same |
| E-CAL5 | axe on the calendar route (E-X1 extension) | PASS | PASS | n/a | `admin-a11y-responsive.spec.ts` route added |
| E-CAL6 | 360/768/1024/1440 overflow (E-X2 extension) and list/grid switch | PASS | PASS | n/a | same, plus 2 tests in the calendar spec |
| E-CAL7 | Create and cancel closure updates calendar | PASS | PASS | n/a | calendar spec |
| E-CAL8 | Real API: E-B1 plus E-CAL2 Jan 1 / Sunday / Saturday | n/a | n/a | **NOT RUN** | see below |

Counts: calendar spec 6/6 on chromium and 6/6 on mobile-360 (E-CAL8 is skipped unless `E2E_REAL_CAL8=1`); `admin-functional.spec.ts` 9/9 chromium; `admin-a11y-responsive.spec.ts` chromium 136 passed first run plus 1 cold-compile failure (`/staff/admin/audit-log` axe, 5 s timeout), re-run on a fresh server: passed; mobile-360 132 passed first run plus 5 cold-compile/timeout failures (services/new, content, branch detail and rooms at 360, policy-dialog case; none touch the calendar route), `--last-failed` re-run on a fresh server: 5/5 passed.

Not run: the full `npm run test:e2e` over every area (handover §17 last paragraph).

### E-CAL8 (real API) - NOT RUN

API from this worktree on http://localhost:5011 against `catecommerce_dev` (not migrated), web on :3114 with `NEXT_PUBLIC_USE_MOCKS=false`. The test (`E2E_REAL_CAL8=1`, `-g E-CAL8`) signed in as `e2e.admin@purrcare.test` with `E2E_STAFF_PASSWORD` from `web/.env.e2e.local`. Both attempts were refused by the API ("That mobile number or email and password don't match an account"). Stopped after two refused sign-ins to avoid a lockout. No branch was created in `catecommerce_dev`. Per handover §20.8, E-CAL8 is recorded not run; A2 and A10 must not be marked met on MSW alone. The owner/integrator needs to confirm that the `e2e.admin@purrcare.test` account exists in `catecommerce_dev` with the password in `.env.e2e.local`, then re-run the command above.

## Other checks

| Check | Result |
|---|---|
| `npx tsc --noEmit` | clean |
| `npm run lint` (`--max-warnings 0`) | clean |
| `npx next build` | succeeded |
| `prettier --check` on every file changed by this slice | clean (192 other files in the repo already fail it on this Windows checkout; not touched) |

## Run log

2026-10-09, implementer (ho/ADM-002-FU1). Dev servers and the API were stopped after each run; `web/.env.local` restored to mocks-on.
