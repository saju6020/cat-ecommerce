# TC-HO-ADM-002 — Administration and Configuration (frontend)

Handover: docs/handover/HO-ADM-002.md
Commit(s) under test: `08e873e..e535110` (worktree `ADM-002`, branch `ho/ADM-002`), TEST gate
**fifth pass** — adds `fa80c47` (`fix(HO-ADM-002): 403 shows PermissionDeniedState on detail pages
and forms`, §18 A3, ACCEPTANCE F-H1) on top of the fourth pass's `bf7d3d0`; ARCHITECTURE and
CODE_REVIEW delta records `9ce0541`, `e535110` are docs only
Written: 2026-09-28  ·  Executed: 2026-09-28 (first pass); updated and re-executed 2026-09-28
(second pass); updated and re-executed 2026-09-28 (third pass); updated and re-executed 2026-09-29
(fourth pass); updated and re-executed 2026-09-29 (fifth pass)

> Statuses record what was **observed**, not inferred. The Evidence column names what was actually
> seen. `BLOCKED` marks a case that could not be executed or whose result was reasoned about rather
> than watched — it is not a failure, and it is not hidden.
>
> This sheet supersedes the version committed at `994305c` (written and self-executed by the
> implementer alongside the code, not by this TEST gate run). Every row below was reset to
> `NOT RUN` and re-executed independently in this session before being filled in.
>
> **Second pass:** `ce14242` fixes the first pass's HIGH (U-G4 back-button safety — see
> `docs/gates/HO-ADM-002-TEST.md`'s first-pass FINDINGS). U-G4 and the rows it touches were reset
> to `NOT RUN` and re-executed in a real browser; a new row (U-G5) and a new regression row (R5)
> were added. Re-executing at real keystroke speed also surfaced a new, unrelated race — see the
> second-pass Findings entry and the gate report.
>
> **Third pass:** `8498378` targets the second pass's HIGH (U-G6 — filter fields
> could silently drop keystrokes or lose a quick successive edit). U-G4, U-G5, U-G6, R5 and E-G1
> were reset to `NOT RUN` and re-executed independently, in a real browser, with the gate's own
> temporary probe spec (not the implementer's committed `admin-filters.spec.ts`) driving the same
> race timings the second pass used, repeated at least 3× each, on both chromium and mobile-360.
> **Result: `8498378` narrows the race dramatically but does not close it** — an intermittent,
> reproducible single-character loss still occurs at ordinary typing speeds (confirmed on both
> projects; see U-G6's third-pass evidence and the third-pass gate report's FINDINGS). U-G6 stayed
> `FAIL`.
>
> **Fourth pass:** `ec00965` targets the third pass's HIGH (U-G6 — the
> single-character-loss variant of the filter race). U-G4, U-G5, U-G6, R5 and E-G1 were reset to
> `NOT RUN` and re-executed independently, in a real browser, with a new temporary probe spec
> (`tmp-race-probe-4th.spec.ts`, not the implementer's committed `admin-filters.spec.ts`), against a
> freshly started `next dev` (the paused-and-resumed session's own prior server and temp spec were
> discarded first — recreated from scratch, not resumed) — `action` "FirstValue" at 50/80/100/150
> (×10)/200/250 ms/key, `entityId` "abc12345" at 30/80 ms/key (×3 each), two text fields back to
> back then a date, `kind`→`status` with no pause, Back/Forward/deep-link field-freshness, and a new
> debounce-edge probe (Back pressed within the 300ms debounce window). **Result: `ec00965` closes
> the race U-G6 tracked across the second and third passes — zero drops or corruptions in 30
> `action`-typing attempts (10 at 150ms/key) and 6 `entityId` attempts per project, on both chromium
> and mobile-360.** U-G6 is now `PASS`. This pass's own debounce-edge probe surfaced one new,
> narrower, not-blocking anomaly — see the fourth-pass Findings below (new, MEDIUM).

> **Fifth pass (this update):** ACCEPTANCE failed §18 A3 (403s not shown as `PermissionDeniedState`);
> `fa80c47` fixes it. A new section, "Permission-denied and error-state cases (A3)", was written with
> every row `NOT RUN` first (P1–P23, B1–B3, X-d), then executed with committed component tests,
> temporary component tests, and a temporary real-browser spec (all removed). Because
> `describeApiError`/`submitWithServerErrors` are shared with other areas, the cat and account e2e
> specs were also run (X-d) and a new row X-e records a pre-existing failure found by that run.
> **Result: A3 is met** on every fixed page and form; one new MEDIUM (X-e) is not caused by the fix.

## Summary — fifth pass

| | Count |
|---|---|
| Total | 77 (49 carried + 28 new: P1–P23, B1–B3, X-d, X-e) |
| PASS | 74 |
| FAIL | 2 (U-G7 carried, MEDIUM; X-e new, MEDIUM, pre-existing at `bf7d3d0`) — neither blocking |
| BLOCKED | 1 (E-E1, partial, carried unchanged) |
| NOT RUN | 0 |

Fourth-pass summary follows for reference.

## Summary — fourth pass

| | Count |
|---|---|
| Total | 49 (28 unit/component [27 + new U-G7] + 12 e2e + 3 extra checks + 6 regression [5 + new R6]) |
| PASS | 47 |
| FAIL | 1 (U-G7, new, MEDIUM — not blocking, see Findings) |
| BLOCKED | 1 (E-E1, partial, carried unchanged) |
| NOT RUN | 0 |

Third-pass summary (for reference; superseded by the row above): 47 total, 46 PASS, 1 FAIL (U-G6,
narrowed not closed), 1 BLOCKED, 0 NOT RUN. This pass: U-G4/U-G5/R5/E-G1 re-executed against
`ec00965` and stay PASS. **U-G6, re-executed against `ec00965` with a new, independent temporary
probe (not a single-shot check, not the committed suite) — including 10 repeat-each attempts at
150ms/key per project, the specific parameter that reproduced the third pass's HIGH — reproduced
zero drops or corruptions on either chromium or mobile-360. U-G6 is now PASS**, and is tracked as
fixed via a new regression row, R6. A new case row, U-G7, records a distinct, narrower anomaly this
pass's own probe design (a debounce-edge case the task specifically asked to be tried) surfaced —
see Findings — classified MEDIUM, not blocking, and counted as this pass's one FAIL.

## Environment

- `web/` on worktree `D:\my-repo\cat_ecommerce-wt\ADM-002`, HO-CAT-002 scaffold, HO-ACC-002 merged.
- Node/npm as configured in the worktree; no changes made to `package.json`/lockfile.
- Unit/component suite: `npx vitest run` (jsdom, MSW-free component tests).
- E2E: Playwright against **MSW mocks only**, `next dev`, `PORT=3102`, serial
  (`--project=chromium --project=mobile-360 --workers=1`) per this gate's own instructions
  (backlog row 1: parallel workers against one cold `next dev` time out).
- No `dotnet` API and no PostgreSQL were started this session — the real-API re-run every E-case's
  own §5.1 rule asks for is **BLOCKED** for all of them, same as the previous pass.
- Port 3102 confirmed free before starting and confirmed free again after every server this
  session started was stopped (`netstat`).

**Second pass, additionally:**
- `npx tsc --noEmit` — clean. `npm run lint` (`eslint . --max-warnings 0`) — clean.
- `npx vitest run` — **80 files / 293 tests, all passed** (39.9s; up from 79/289 — `ce14242` added
  one file, `tests/components/audit/use-filter-url-update.test.tsx`, 4 tests). All 4 watched
  individually too (`--reporter=verbose` on that file alone).
- `tests/e2e/admin-functional.spec.ts`, `PORT=3102`, `--project=chromium --project=mobile-360
  --workers=1`: **18/18 passed** (4.0m), one serial run, no rerun needed.
- `admin-a11y-responsive.spec.ts` **not re-run this pass.** `ce14242` touches only
  `useFilterUrlUpdate.ts` and the two pages' filter-wiring (`handleFilterChange`/`setParam` calls,
  no markup, no roles/labels/landmarks changed) — nothing in the diff can plausibly move an axe
  result or a viewport/overflow result, and the first pass's own 132/132-per-project clean rerun
  already covers every route this file exercises, including the two touched pages, at every
  breakpoint. Re-running a 13-14-minute (both projects) suite for a diff with no a11y/DOM-shape
  surface would not add information proportionate to the cost. If the second-pass HIGH below
  (U-G6) is fixed by moving to local input state, that fix's own diff should get this spec re-run,
  since it does touch the input elements directly.
- New: three real-browser back-button/deep-link cases (temporary, uncommitted
  `tests/e2e/tmp-filter-back-button.spec.ts`, removed after this run) run individually per project,
  `--workers=1`: chromium `3 passed`, mobile-360 `3 passed`. Four more temporary, uncommitted debug
  specs (`tmp-debug-typing*.spec.ts`) used only to characterise the new U-G6 race (see Findings),
  also removed after this run.
- Port 3102 confirmed free before this pass started (only `TIME_WAIT` entries from prior closed
  connections, no `LISTENING` entry) and confirmed free again after every server this pass started
  was stopped.

**Third pass, additionally:**
- `npx tsc --noEmit` — clean. `npm run lint` (`eslint . --max-warnings 0`) — clean.
- `npx vitest run` — **82 files / 299 tests, all passed** (46.3s; up from 80/293 — `8498378` added
  two files, `tests/components/audit/filter-url-state.test.tsx` (3 tests) and
  `tests/components/content/content-filter-url-state.test.tsx` (3 tests)). First-run clean, no
  flake observed in this session, including `idle-lock.test.tsx` (the developer's own noted
  possible flake did not reproduce here).
- `tests/e2e/admin-functional.spec.ts`, `PORT=3102`, `--project=chromium --project=mobile-360
  --workers=1`: **18/18 passed** (4.2m), one serial run, no rerun needed.
- `tests/e2e/admin-filters.spec.ts` (the implementer's own new, committed regression spec),
  `PORT=3102`, `--project=chromium --project=mobile-360 --workers=1`, run **twice** per this
  pass's own instructions: **10/10 passed** both times (≈1.1m each).
- `admin-a11y-responsive.spec.ts`, run this pass (the second pass had skipped it as
  disproportionate to `ce14242`'s diff — `8498378` touches the input/select elements directly, so
  that reasoning no longer applies): each project on its own fresh `next dev` process
  (`rm -rf test-results` before each). **First isolated chromium run: 130/132 passed, 2 failed**
  (`E-X2` `/staff/admin/content/new` at 360px — stuck past the `Loading…` timeout; `E-X1` with the
  policy dialog open — dialog never became visible within 30s), matching backlog row 1's
  already-documented cold/degraded-`next dev`-under-load pattern, not a route this fix touches.
  Immediate isolated rerun of just those two tests, fresh dev server: **2/2 passed.**
  **Mobile-360 first run (accidentally overlapped with a chromium retest sharing the same `.next`
  build directory — see the gate report's environment note): 131/132 passed, 1 failed**
  (`/staff/admin/branches` axe) with a different, dev-server-instability-flavoured error than the
  chromium failures, consistent with cross-run `.next` cache contention rather than a real result.
  Discarded and **re-run cleanly and fully in isolation** (that test's whole parametrised group,
  26 routes, one fresh dev server, nothing else running against this directory): **26/26 passed**,
  including `/staff/admin/branches`. Per this gate's own "first-hit timeout is environment only if
  an immediate isolated rerun passes" rule: **both projects are PASS, 132/132 each**, confirmed by
  clean isolated reruns; no product-code finding follows. E-X1/E-X2 stay PASS.
- New, independent real-browser race verification: a temporary, uncommitted spec
  (`tests/e2e/tmp-race-probe.spec.ts`, removed after this run), written by this gate, not adapted
  from the implementer's own `admin-filters.spec.ts` — same race timings the second pass used
  (50/80/100/150ms/key for `action`, 80ms/key for `entityId`, no-pause `kind`→`status`), each
  repeated ≥3× per case, run on chromium and mobile-360 separately, plus extra `--repeat-each`
  reruns to characterise the still-open race (see gate report FINDINGS). Several more temporary,
  uncommitted debug specs (`tmp-debug-history*.spec.ts`, `tmp-debug-typing-sweep.spec.ts`) used
  only to isolate two candidate explanations (a genuine edit-loss race vs. this gate's own flawed
  Back/Forward test expectation) — all removed after this run.
- Port 3102/3103 confirmed free before this pass started and confirmed free again after every
  server this pass started was stopped (`netstat`).

**Fourth pass, additionally:**
- Session was paused and resumed mid-gate; the coordinator confirmed the prior (still-running)
  `next dev` on port 3102 was stopped and the prior temporary probe spec deleted while paused, and
  the worktree was clean (`git status`) on resume. Every server and probe spec in this pass's own
  section below was started/written fresh after resume, none reused from before the pause.
- `npx tsc --noEmit` — clean, no output.
- `npm run lint` (`eslint . --max-warnings 0`) — clean, no output.
- `npx vitest run` — **82 files / 302 tests, all passed** (50.0s). Unchanged file count from the
  third pass (`ec00965` added tests to the three files the third pass's own fix already touched,
  not new files) — test count up from 299 to 302 (three new assertions across
  `filter-url-state.test.tsx`, `content-filter-url-state.test.tsx`,
  `use-filter-url-update.test.tsx`, matching `ec00965`'s diff). First run clean, no flake observed
  (`Not implemented: navigation to another Document` × 4 is jsdom's own known stderr noise for
  full-page-navigation-shaped test cases, not a failure — all 302 tests still reported passed).
- `tests/e2e/admin-functional.spec.ts`, fresh `next dev`, `PORT=3102`, `--project=chromium
  --project=mobile-360 --workers=1`: **18/18 passed** (2.1m), one serial run, no rerun needed.
- `tests/e2e/admin-filters.spec.ts` (the implementer's own committed regression spec, now 6 tests/
  project — `ec00965` added the 150ms/key×5 case), same server, run **twice** per this pass's own
  instructions: **12/12 passed both times** (55.1s, then 52.3s).
- `admin-a11y-responsive.spec.ts`, each project on its own fresh `next dev` process (`rm -rf
  test-results` before each), per this gate's standing "run separately" rule: **chromium: 132/132
  passed (7.1m), clean on the first attempt** — no cold-timeout this time, so no rerun was needed
  (reported here per the task's "report both" instruction even though there was nothing to rerun).
  **Mobile-360: 132/132 passed (6.3m), also clean on the first attempt.** Both projects PASS,
  confirmed by direct observation of the `X passed` summary line, not inferred.
- New, independent real-browser race verification: a temporary, uncommitted Playwright spec
  written by this gate for this pass specifically
  (`tests/e2e/tmp-race-probe-4th.spec.ts` — a fresh file, not a reuse of the third pass's own
  `tmp-race-probe.spec.ts`, and not adapted from the implementer's committed `admin-filters.spec.ts`),
  run one project at a time (`--project=chromium`, then separately `--project=mobile-360`),
  `--workers=1`, `PORT=3102`: **12/12 passed on chromium (1.5m), 12/12 passed on mobile-360
  (1.4m)** — see U-G6/U-G7 below and FINDINGS for the case-by-case detail (10× at 150ms/key, the
  third pass's own reproduction parameter, included in both runs). Removed after this run
  (`rm -f tests/e2e/tmp-race-probe-4th.spec.ts`); `git status` confirms nothing left uncommitted in
  `web/` at the end of this pass.
- Port 3102 confirmed free before this pass started (post-resume) and confirmed free again after
  every server this pass started was stopped (`netstat`, `taskkill` on the listening PID each time,
  one `next dev` process at a time throughout — never two against the same `web/` directory
  simultaneously, avoiding the third pass's own noted `.next`-cache-contention trap). Port 3103 was
  not used this pass (no need arose).

**Fifth pass, additionally:**
- `git status` clean at start; `docs/test-cases/TC-HO-ADM-002.md` and `docs/gates/HO-ADM-002-TEST.md` are the only files this pass changes. Port 3102 free before starting and after the last server was stopped; one `next dev` at a time throughout.
- `npx tsc --noEmit` clean; `npm run lint` clean; `npx vitest run` — **84 files / 311 tests, all passed** (41.0s; up from 82/302: `fa80c47` added `detail-pages-403.test.tsx` (6) and `form-403-message.test.tsx` (3)). The 9 new tests were also run alone with `--reporter=verbose` and each watched pass.
- Temporary component test `tests/components/admin/tmp-a3-gate.test.tsx` (18 tests: 17 passed first go, 1 was my own too-short timeout, rerun passed) — removed.
- Temporary real-browser spec `tests/e2e/tmp-a3-browser.spec.ts` (B0 control, B1, B2, 500, 404), `PORT=3102`, `--workers=1`, one project at a time: chromium 5/5 (35s), mobile-360 5/5 (20s) — removed.
- `admin-functional.spec.ts` + `admin-filters.spec.ts`, both projects, `--workers=1`: **30/30 passed** (18 + 12, 3.1m).
- Cat and account e2e specs (X-d): 43 tests per project. chromium 37 passed / 6 failed; mobile-360 37 passed / 6 failed. Failures: 5 `account-area` per project (pre-existing, X-e, reproduced identically at `bf7d3d0` in a temporary detached worktree with a junctioned `node_modules`, removed afterwards); plus one flake per project that passed on isolated rerun (chromium `staff-admin` E-F1: 2/2, then 10/10 with `--repeat-each=5` across both projects; mobile-360 `staff-customers` E-H1: 2/2 in isolation, chromium 4/4 for both files).
- `admin-a11y-responsive.spec.ts`, each project on its own fresh `next dev` (`rm -rf test-results` first): **chromium 132/132 (6.7m), mobile-360 132/132 (6.6m)**, both clean on the first attempt (no cold-timeout, nothing to rerun).

## Unit / component cases (§15)

Evidence is "`npx vitest run` — PASS" against the file named, observed directly in this session's
own run output (not carried over from the prior pass).

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| U-A1 | `AdminNav` for a nurse; a `403` mock on a hidden page | Admin-only items hidden; `PermissionDeniedState` rendered | PASS | `npx vitest run` green (79 files/289 tests); `tests/components/admin/admin-nav.test.tsx`, `tests/components/admin/permission-denied-on-403.test.tsx` |
| U-B1 | `WeeklyHoursEditor`: overlapping intervals; 4 intervals; reversed times | Row-level errors before submit; whole week sent on save | PASS | `tests/components/branches/weekly-hours-editor.test.tsx`, part of the green run |
| U-B2 | "Copy Monday to weekdays" | Tue–Fri equal Monday; Sat/Sun untouched | PASS | `tests/components/branches/weekly-hours-editor.test.tsx`, part of the green run |
| U-B3 | Closure form: half-day times with a multi-day range | Times disabled/cleared; message explains | PASS | `tests/components/branches/closure-form.test.tsx`, part of the green run |
| U-B4 | Hours of the `Europe/London` branch viewed from any browser zone | Labels and times in Europe/London | PASS | `tests/components/branches/weekly-hours-editor.test.tsx` ("U-B4: labels every time input with the branch time zone") — a wall-clock string with no timezone-conversion, so a browser-zone-independence proof beyond the label is not applicable here; E-B1 exercises the real Europe/London create-and-save path |
| U-B5 | `ActiveBranchPicker` with one branch / two | Hidden / shown; change sends `X-Branch-Id` on the next request | PASS (component, own `queryClient`) | `tests/components/branches/active-branch-picker.test.tsx` — read directly: uses its own test-scoped `QueryClient`, not the real `registerQueryClient()`/`getActiveBranchId()` path (ARCHITECTURE backlog row 11); that path is proven only by X-a/E-B2 below |
| U-C1 | `EffectiveDatedHistory` | Current / Scheduled / Past groups; no edit or delete control anywhere | PASS | `tests/components/admin/effective-dated-history.test.tsx`, part of the green run |
| U-C2 | `ScheduleChangeDialog` | Past date not selectable; amount kept as a string; confirm text names the date | PASS | `tests/components/admin/schedule-change-dialog.test.tsx`, part of the green run |
| U-C3 | `ServiceTable` unpriced service | "Not priced" badge with text | PASS | `tests/components/admin/service-table.test.tsx`, part of the green run |
| U-D1 | `RosterWeekView` at 1024 px / 360 px | `<table>` with headers / day list; no horizontal scroll | PASS (component: both DOM shapes present, CSS-toggled) + E-X2 (real viewport, no horizontal scroll) | `tests/components/roster/roster-week-view.test.tsx` |
| U-D2 | Cell with leave, extra shift and outside-hours shift | Each shown as text | PASS | `tests/components/roster/roster-week-view.test.tsx`, read directly — asserts on text content per cell kind |
| U-D3 | `RosterPatternEditor` schedule next | Effective date must be after today in branch time | PASS | `tests/components/roster/roster-pattern-editor.test.tsx`, part of the green run |
| U-D4 | `LeaveApprovalsTable` reject (with note) | Reject dialog collects a note; `onReject` called with the typed note (blank → `undefined`) | PASS | `npx vitest run tests/components/leave/leave-approvals-table.test.tsx --reporter=verbose` — watched directly: "U-D4: rejecting sends the note the administrator typed" and "rejecting without a note sends no note rather than an empty string" both pass |
| U-E1 | `SettingsForm` from a payload with a new unknown group | Rendered — nothing hardcoded | PASS | `tests/components/settings/settings-form.test.tsx` ("U-E1: renders a brand-new, previously-unknown group — nothing hardcoded"), part of the green run |
| U-E2 | Branch scope: override, then "Use global value" | Override cleared via `DELETE`; non-overridable read-only | PASS | `tests/components/settings/settings-form.test.tsx` ("U-E2/U-E3: …"), part of the green run |
| U-E3 | Save | Only changed keys sent; confirm lists old → new | PASS | `tests/components/settings/settings-form.test.tsx`, part of the green run |
| U-F1 | `LimitedMarkup` with `<script>alert(1)</script>`, `[x](javascript:alert(1))`, headings, bullets, bold, https link | Script shown as literal text; the javascript link not a link; the rest rendered as elements | PASS | `tests/components/content/limited-markup.test.tsx`, read directly — asserts `container.querySelector('script')` absent, link has no `href`/is not rendered as `<a>`, heading/list/bold/https-link all present with correct attrs |
| U-F2 | Editor preview vs public page for the same body | Identical DOM | PASS | `tests/components/content/limited-markup.test.tsx` ("U-F2: … identical DOM"), asserts `innerHTML` equality between two independent renders |
| U-F3 | `PolicyAcceptancePrompt` with two pending | Dialog with both; focus trapped; nothing pre-ticked; accepting both closes it; sign-out reachable | PASS | `tests/components/content/policy-acceptance-prompt.test.tsx`, part of the green run |
| U-G1 | `AuditLogSearch` without a date range | Default last 7 days applied; > 366 days blocked with the server's rule | PASS | `tests/components/audit/audit-log-search.test.tsx` (>366-day block only, unit); the "default last 7 days" half is NOT unit- or e2e-tested anywhere in the suite as committed — I wrote a temporary, uncommitted Playwright check (`page.goto('/staff/admin/audit-log')` with no querystring) and watched it default to `2026-09-21`–`2026-09-28`, a genuine 7-day span. Correct behaviour, but the committed suite does not prove it — flagged under Gaps |
| U-G2 | Diff row with a value `=HYPERLINK(…)` | Rendered as plain text | PASS | `tests/components/audit/diff-table.test.tsx`, read directly — asserts the literal text is present and `queryByRole('link')` is null |
| U-G3 | Export with `413` | The narrow-the-range message | PASS (component: standing note always shown, not a `413`-triggered re-render — `AuditLogSearch`'s own doc comment explains why) + E-G3 exercises the real `413` trigger | `tests/components/audit/audit-log-search.test.tsx`, `tests/e2e/admin-functional.spec.ts` |
| U-G4 | Filters | Round-trip through the URL; back button restores them | **PASS (re-executed fourth pass)** | Reset to `NOT RUN` and re-executed independently against `ec00965`, fresh `next dev`. This pass's own temporary, uncommitted `tests/e2e/tmp-race-probe-4th.spec.ts` (removed after this run): "Back/Forward field freshness…" case — typed `action` at 60ms/keystroke ("StepOne"), settled, edited `from`; `goBack()` restored the pre-`from`-edit state (`action` kept, `from` reverted) staying on `/staff/admin/audit-log`; `goForward()` replayed the `from` edit; deep link pre-filled all seven fields. Watched pass on both chromium and mobile-360. See U-G5 for the content list page, U-G6 for the race this pass closed, and U-G7 for a new, distinct, not-blocking anomaly this pass's own probe found |
| U-G5 (second pass; re-executed third and fourth passes) | `useFilterUrlUpdate` hook (§8.3, shared by audit-log AND content list) | A filter change pushes a history entry and resets `page`; same-field keystrokes inside 1s replace their own entry instead of adding one each; a different field, or the same field after the burst window, adds a new entry; clearing a filter removes its param | PASS | `npx vitest run tests/components/audit/use-filter-url-update.test.tsx --reporter=verbose` — all cases watched individually, passing (also part of the full green `vitest run`, 82 files/302 tests, fourth pass). Content-list page's real back-button behaviour (`kind` then `status`, no gap, both landing in the URL) re-watched directly on both chromium and mobile-360, this pass's own temporary spec, against `ec00965`'s code |
| U-G6 (second pass; re-executed third and fourth passes) | Typing at a realistic-to-fast pace (or two quick, back-to-back filter edits) into `useUrlFilterState`/`useFilterUrlUpdate`-driven fields, real browser, **repeated at least 3× per timing to catch an intermittent race (≥10× at 150ms/key per project, this pass)** | Every keystroke/edit lands in the field and in the URL, every time — no silent data loss | **PASS — race closed, fourth pass** | Reset to `NOT RUN`, re-executed against `ec00965` with a new temporary, uncommitted probe (`tests/e2e/tmp-race-probe-4th.spec.ts`), fresh `next dev`, one project at a time, `--workers=1`. `action` "FirstValue" at 50/80/100/200/250ms/key (×3 each) and, specifically, **150ms/key ×10** (the exact parameter that reproduced the third pass's HIGH 1/5–4/4 and 2/3 of the time) — **0/10 failures on chromium, 0/10 on mobile-360**, every attempt leaving the exact string in both the field and the URL after blur. `entityId` "abc12345" at 30ms/key and 80ms/key, ×3 each — 0/6 failures per project. Two text fields typed back to back with no pause, then a date change — all three landed in the URL, both projects. `kind`→`status` with no pause — both landed, both projects. Regression: the implementer's own committed `admin-filters.spec.ts`, including its 150ms/key×5 case, run twice per project — 12/12 both times. **U-G6 is closed**: `ec00965`'s `pendingWrites` list (tracking every write a hook has made, not just the last) plus debouncing the five free-text fields' own URL writes (~300ms after the last keystroke, so a typing burst produces at most one write to race instead of one per character) together removed the window the third pass's single-character-loss race depended on. See U-G7 for a new, distinct, narrower anomaly this pass's own probe design (specifically prompted by the task's "debounce edge" instruction) surfaced while testing this fix, classified MEDIUM and not blocking |
| U-G7 (new, fourth pass) | Debounce edge: type into a debounced field, then press native Back **within** the 300ms debounce window, before that field's URL write has even been requested | Not specified by §8.3; task asked to "record what actually happens and whether it is sensible (no data corruption; state consistent with the URL after settling)" | **FAIL (MEDIUM, new — not blocking, see Findings)** | This pass's own temporary probe, `tests/e2e/tmp-race-probe-4th.spec.ts` ("debounce edge — Back pressed within 300ms of typing, before the write flushes"), run and watched on both chromium and mobile-360 (identical result both times, not a race — deterministic given this exact sequence): typed `action`="Edge" (40ms/key, no blur), called `goBack()` immediately (well inside the 300ms window), waited 500ms to let anything pending settle. Observed: the field reset to `""` (the resync-from-Back correctly reverted the *displayed* value, since no write for "Edge" had been registered as this hook's own yet), but the debounce timer — a plain `setTimeout` on a ref, unaffected by the Back navigation — fired anyway ~300ms after the last keystroke and pushed a **new** forward history entry with `action=Edge` onto the URL, built from `pendingParams` as resynced by the Back (i.e. everything else at its pre-"Edge" value). Net result: field shows `""`, URL shows `...&action=Edge` — the two are **not** consistent with each other, and the previously-committed `action=Before` entry the user could otherwise have gone `Forward` to is silently overwritten/discarded by this new push. No character-level corruption (unlike U-G6) and the on-screen results list stays consistent with what the field shows (both read from the same local `values` state), but a copied/bookmarked URL at that moment would not reproduce what is on screen, and Forward is no longer available to the edit the user actually committed via blur/Enter earlier. See FINDINGS for full detail and severity reasoning |
| U-X1 | Every editor: server `400` | Mapped to its field; `errorCode` not in the DOM | PASS | `tests/unit/errors.test.ts` ("never passes errorCode to setError — NFR-USA-4"), the shared helper every editor uses, read directly; re-proven in this area by `tests/components/admin/staff-account-detail.test.tsx` ("LastAdministratorCannotBeRemoved is shown as the server's own message") |
| U-X2 | Every editor: dirty + navigate | Unsaved-changes confirm | PASS (wiring verified directly, not just import-grep) | `grep -n "UnsavedChangesGuard" components/**` shows every named editor (`BranchDetailsForm`, `WeeklyHoursEditor`, `ClosureForm`, `RoomForm`, `ServiceForm`, `TaxClassForm`, `ContentEditor`, …) passes `when={isDirty && !isSubmitSuccessful}` (or the equivalent local `saved` flag) consistently; the guard's own navigation-interception behaviour is HO-ACC-002's own tested component, unchanged here |

## End-to-end cases (§16), against MSW

First serial run, `PORT=3102`, `--project=chromium --project=mobile-360 --workers=1`: **18/18 passed** (2.5 min), both projects, one run, no rerun needed (backlog row 1's cold-compile timeout did not occur serially).

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| E-B1 | Create a branch in `Europe/London`, set hours, add a holiday | Saved; calendar in the detail view skips the holiday | PASS (the "calendar in the detail view" half is a gap — see Gaps) | `tests/e2e/admin-functional.spec.ts`, watched pass on chromium and mobile-360 |
| E-B2 | Assign a staff member to two branches; sign in as them; switch branch | Picker shown; requests carry the new branch | PASS | `tests/e2e/admin-functional.spec.ts`, watched pass on chromium and mobile-360; also the subject of X-a below |
| E-C1 | Schedule a price for next month | Appears under Scheduled; current unchanged | PASS | `tests/e2e/admin-functional.spec.ts`, watched pass on chromium and mobile-360 |
| E-D1 | Nurse requests leave; admin approves; roster view | Leave days marked "On leave" | PASS | `tests/e2e/admin-functional.spec.ts`, watched pass on chromium and mobile-360 |
| E-E1 | Change the vaccination due-soon window; open a cat's vaccination status (HO-CAT-002 page) | Status reflects the new window | BLOCKED (partial) | The setting itself round-trips live (proven by U-E1…U-E3's real `PUT`/`GET` cycle); HO-CAT-002's own vaccination-status mock is outside this handover's §13 scope to read or edit and has no e2e case in this suite consuming `clinical.vaccinationDueSoonDays`. Not invented or worked around — same as the prior pass |
| E-F1 | Publish a page; open `/pages/{slug}` signed out | Rendered; unpublish → not-found page | PASS | `tests/e2e/admin-functional.spec.ts`, watched pass on chromium and mobile-360 |
| E-F2 | Publish a policy version requiring acceptance; sign in as a customer | Prompt shown; accept → portal usable | PASS | `tests/e2e/admin-functional.spec.ts`, watched pass on chromium and mobile-360 |
| E-G1 | Audit search for the E-C1 price change | Row with before/after; CSV downloads | PASS | `tests/e2e/admin-functional.spec.ts`, watched pass on chromium and mobile-360. Fourth pass: re-confirmed the deep-link half still works after `ec00965` — this pass's own temporary spec's "Back/Forward field freshness…" case deep-links to all seven fields (`from`, `to`, `action`, `entityId`, `actorUserId`, `category`, `entityType`) and watches every one pre-fill correctly, both chromium and mobile-360, fresh `next dev` |
| E-G3 (extra) | Export with `413` (real trigger, not just the component test) | The narrow-the-range message | PASS | `tests/e2e/admin-functional.spec.ts`, watched pass on chromium and mobile-360 |
| E-H1 | Maintenance on; staff sign-in and admin pages | Unaffected | PASS | `tests/e2e/admin-functional.spec.ts`, watched pass on chromium and mobile-360 |
| E-X1 | axe on every route in §8.1, clean and with errors, with the policy dialog open | Zero serious/critical | PASS | see "admin-a11y-responsive.spec.ts results" below — 132/132 on chromium, 132/132 on mobile-360 |
| E-X2 | Every route at 360 / 768 / 1024 / 1440 | No horizontal scroll, no clipped control | PASS | see "admin-a11y-responsive.spec.ts results" below — 132/132 on chromium, 132/132 on mobile-360 |

### `admin-a11y-responsive.spec.ts` results

First attempt: combined `--project=chromium --project=mobile-360 --workers=1` (`PORT=3102`), one
single `next dev` process for the whole 264-test combined run. This degraded badly partway through
the chromium project — a high proportion of `E-X2` cases (across essentially every route, staff and
public alike) failed with the page stuck showing `Loading…` past the 5-second heading-visibility
timeout, and the dev-server process's memory climbed from ~2.46 GB to ~2.52 GB over ~20 minutes.
This matches the environment limitation backlog row 1 already names for `admin-functional.spec.ts`
(cold/degraded `next dev` under sustained serial load), scaled up by this file's much larger test
count (132 tests/project vs. 18). Killed after ~20 minutes with no clean stopping point in sight.

Per this gate's own instructions ("run each project separately and report both"), re-ran each
project **separately, each against its own fresh `next dev` process** (`rm -rf test-results` before
each):

- **chromium alone**: `132 passed (7.4m)`, exit code 0 — watched directly, no failures.
- **mobile-360 alone**: `132 passed (6.9m)`, exit code 0 — watched directly, no failures.

Treating the first combined attempt's failures as environment-only per this gate's own instructions
("a first-hit timeout is environment only if a serial rerun passes") — the clean per-project reruns
(264/264 tests total, 0 failures, both watched directly) confirm this; no product-code finding
follows from the first attempt.

E-X1 and E-X2 are both **PASS** on both chromium and mobile-360, confirmed by these clean reruns.

**Fourth pass:** re-run per-project (fresh `next dev` each, `rm -rf test-results` before each), per
this gate's own standing instruction to report both a cold first-run timeout and its rerun. Both
projects passed cleanly on the **first** attempt this time — chromium 132/132 (7.1m), mobile-360
132/132 (6.3m) — no timeout occurred, so no rerun was needed. E-X1/E-X2 stay PASS.

## Extra checks asked for by earlier gates

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| X-a | ARCHITECTURE backlog row 11: `E-B2` run against the real `registerQueryClient` path (`next dev`, real browser, real `app/providers.tsx`), the only guard for the Strict-Mode regression fixed in `62178ab` | Passes against the real app, not a mocked `lib/branch` | PASS | `E-B2` in `tests/e2e/admin-functional.spec.ts` navigates via `page.goto`/real `next dev` (Strict Mode on) all the way through `app/providers.tsx` → `registerQueryClient()` → `apiFetch`; watched pass on chromium and mobile-360. Confirmed by reading `app/providers.tsx` that `registerQueryClient` is called from a `useEffect`, not the `useState` initialiser |
| X-b | Backlog row 12: after an idle lock and unlock (same staff member), the branch shown in `ActiveBranchPicker` and the `X-Branch-Id` actually sent agree | Both show/send the branch selected before the lock, not the primary branch | PASS | Wrote a temporary Playwright test, `tests/e2e/tmp-idle-lock-branch-consistency.spec.ts` (not committed, removed after this run): signs in, mocks `/api/staff/branches/mine` with two branches, switches to London (confirms `X-Branch-Id: london` on the next request), forces an idle-timeout lock (`__forceStaffIdleExpired` + `__invalidateQueries`, the same mechanism `staff-auth.spec.ts` uses), re-authenticates as the same staff member, then asserts the picker still shows London AND a fresh request (via `__invalidateQueries(['admin'])`, no full reload) still carries `X-Branch-Id: london`. Watched pass on chromium and mobile-360 (`2 passed, 15.6s`). Source reading confirms why: the active branch lives in the one registered `QueryClient`'s cache, and `handleReSignInSuccess` (the same-person unlock path in `IdleLock.tsx`) never calls `queryClient.clear()` — only the different-person "discard and reload" path does |
| X-c | `c7f29d7` reject-with-note fix: `U-D4` reject path sends the typed note (and omits a blank one) | `onReject` called with the typed note; blank → `undefined` | PASS | Same as U-D4 above — `npx vitest run tests/components/leave/leave-approvals-table.test.tsx --reporter=verbose`, both tests watched passing individually |

## Permission-denied and error-state cases (A3) — fifth pass

Added for `fa80c47` (ACCEPTANCE F-H1, §18 A3). IDs assigned by this gate (the handover's §15/§16
have no numbered rows for these). **All rows written `NOT RUN` before anything was executed.**
"Existing" = a committed test (`tests/components/admin/detail-pages-403.test.tsx`,
`form-403-message.test.tsx`); "temp" = a temporary, uncommitted spec this gate writes, runs, removes.

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| P1 | Tax-class detail, `GET tax-classes/:id` → 403 (existing) | `PermissionDeniedState`, no `ActionNotPermitted` text | PASS | `detail-pages-403.test.tsx` "tax-class detail: a 403 on the tax class", watched (`--reporter=verbose`) |
| P2 | Tax-class detail, class loaded, then `GET .../rates` → 403 (dependent query; temp) | Record what shows (code says `PermissionDeniedState` replaces the loaded page) | PASS | temp `tmp-a3-gate.test.tsx` (removed): rates 403 with the class GET 200 -> `PermissionDeniedState` shown, replacing the loaded detail page (backlog row 34 notes this as defensible, previously untested) |
| P3 | Tax-class detail, → 500 (existing) | ErrorState with retry, not a spinner | PASS | existing "tax-class detail: a non-403 error shows the error state with retry" watched (1.2s, incl. the hook's one 1s retry) |
| P4 | Tax-class detail, → 404 (temp) | "Tax class not found" ErrorState | PASS | temp: 404 -> "Tax class not found" ErrorState, no spinner |
| P5 | Service detail, `GET services/:id` → 403 (existing) | `PermissionDeniedState` | PASS | existing "service detail: a 403 on the service" watched |
| P6 | Service detail, service loaded, `GET .../prices` → 403 (temp) | Record what shows | PASS | temp: prices 403, service loaded -> `PermissionDeniedState` replaces the page |
| P7 | Service detail, → 500 (existing) | ErrorState with retry, not a spinner | PASS | existing "service detail: a non-403 error..." watched |
| P8 | Service detail, → 404 (temp) | "Service not found" ErrorState | PASS | temp: 404 -> "Service not found" ErrorState |
| P9 | Roster, `.../rosters` → 403 (existing) | `PermissionDeniedState` | PASS | existing "roster: a 403 on rosters" watched |
| P10 | Roster, `.../availability` → 403 (existing) | `PermissionDeniedState` | PASS | existing "roster: a 403 on availability" watched |
| P11 | Roster, `.../exceptions` → 403 (temp) | `PermissionDeniedState` | PASS | temp: roster-exceptions 403 -> `PermissionDeniedState` |
| P12 | Roster, `.../rosters` → 500 (temp) | Not a spinner; week-view error with retry | PASS | temp: rosters 500 -> week view shows "We couldn't load this just now." + Try again; rest of the page still renders; no `PermissionDeniedState`. (First attempt hit the 5s default test timeout because of the hook's single 1s retry plus my own wait, a flaw in my test fixed with a 15s timeout, not a product fault) |
| P13 | Admin system page: 403, and 500 (temp) | `PermissionDeniedState`; ErrorState with retry (was a spinner) | PASS | temp: system 403 -> `PermissionDeniedState`; 500 -> "couldn't load the system status" + Try again (was a spinner) |
| P14 | Admin settings page: 403, and 500 (temp) | `PermissionDeniedState`; ErrorState with retry | PASS | temp: settings 403 -> `PermissionDeniedState`; 500 -> "couldn't load the settings" + Try again (was a spinner) |
| P15 | Branch closures tab 403; rooms tab 403 (temp; branch loads fine) | `PermissionDeniedState` each | PASS | temp: closures 403 and rooms 403 (branch GET 200) -> `PermissionDeniedState` each |
| P16 | `/staff/leave` list 403 (temp) | `PermissionDeniedState` | PASS | temp: `/staff/leave` list 403 -> `PermissionDeniedState` |
| P17 | One form, 403 on submit (existing: `RoomForm`) | Permission message, not "Something went wrong", no error code | PASS | existing `RoomForm` 403 submit watched: permission message, no "Something went wrong", no error code |
| P18 | Another form through `submitWithServerErrors` with 403 (temp; a different form than P17) | Permission message | PASS | temp: `TaxClassForm` 403 submit -> permission message; cross-area `WeightEntryForm` (via `submitWithServerErrors`) 403 -> permission message, and a non-403 (`Error`) keeps "Something went wrong" |
| P19 | `describeApiError`: `BranchNotAssigned` 403 (existing) | Own wording naming the branch picker | PASS | existing `describeApiError(BranchNotAssigned)` -> branch-picker wording, watched |
| P20 | `describeApiError`: `BranchAssignmentRequired` 403 (existing) | Own wording (ask an administrator to assign a branch) | PASS | existing `describeApiError(BranchAssignmentRequired)` -> "assign you to a branch" wording, watched |
| P21 | A detail page whose GET returns 403 `BranchNotAssigned` / `BranchAssignmentRequired` (temp) | Record what shows (backlog row 34: `PermissionDeniedState`, not the banner) | PASS (recorded; MEDIUM open, backlog row 34) | temp: service detail GET 403 with `BranchNotAssigned` and, separately, `BranchAssignmentRequired` -> body text is exactly the generic `PermissionDeniedState` ("You don't have permission to do this. Ask an administrator if you need access."), not branch guidance. Matches row 34; not re-raised |
| P22 | Non-403 failure on a form submit (existing) | Generic "Something went wrong" retained | PASS | existing `describeUnexpectedError(new Error)` -> generic text; plus the temp `WeightEntryForm` non-403 case |
| P23 | Cross-area: a cat form / account form / photos page 403 or non-403 message via the shared helpers | Component suites in those areas green; no test expecting the old wording for a 403 | PASS | full `npx vitest run` 84 files / 311 tests green with the changed `describeApiError`/`submitWithServerErrors` (cat, account and photo suites included); `grep` found no test in `tests/` expecting the old generic text for a 403 (only `staff-admin.spec.ts` E-F1 and the admin 403 tests mention permission); temp `WeightEntryForm` (a cat form) 403 -> permission message, non-403 -> generic. Cross-area effect itself is backlog row 37 |
| B1 | Real browser: sign in as the seeded Nurse, open `/staff/admin/tax-classes/<id>` with the tax-class GET answered 403 | `PermissionDeniedState`, no spinner (chromium and mobile-360) | PASS (chromium and mobile-360) | temp `tmp-a3-browser.spec.ts` (removed): real MSW staff session as the seeded **Nurse** (`nurse@purrcare.test`), `page.goto(/staff/admin/tax-classes/<id>)`. **How the 403 was produced:** the admin MSW handlers do not model roles (`mocks/handlers/admin/fixtures.ts` says so; a control test B0 confirmed the Nurse gets the page with no injection), so an `addInitScript` wrapper on `window.fetch` answered every `GET /api/admin/tax-classes*` with a real 403 `Response` and the API error body `{errors:[{errorCode:"ActionNotPermitted"}]}`; the app's own `apiFetch` -> `ApiForbiddenError` path ran unmodified, and the test asserted the wrapper fired (`__injected > 0`). Result: `role=alert` "You don't have permission to do this" visible; after a further 2.5s no "Loading…" and no "ActionNotPermitted"/"Something went wrong". Also 500 -> "couldn't load this tax class" + Try again, and 404 -> "Tax class not found", none spinning. 5/5 on each project (control B0, B1, B2, 500, 404) |
| B2 | Real browser: sign in as the seeded Reception staff member, open `/staff/admin/branches/<id>/roster` with rosters answered 403 | `PermissionDeniedState`, no spinner (chromium and mobile-360) | PASS (chromium and mobile-360), with a role caveat | same technique on `/staff/admin/branches/<id>/roster`: `GET .../(rosters|availability|roster-exceptions)` answered 403 -> `role=alert` permission message, no "Loading…" after 2.5s. **Caveat:** signed in as the seeded **Nurse**, not Accountant/Reception — the mock has no signable Accountant or ShopManager (the seeded Reception has no password) and the Nurse legitimately holds `Rosters.View` (HO-ADM-001 §permissions), so the 403 is injected exactly as in B1, not produced by a role check. The rendering path under test is the same |
| B3 | axe on a rendered `PermissionDeniedState` page (B1's page) and on an `ErrorState` detail page | Zero serious/critical | PASS (chromium and mobile-360) | axe (`@axe-core/playwright`, serious/critical filter as `admin-a11y-responsive.spec.ts`) on the rendered `PermissionDeniedState` page (B1 tax-class and B2 roster) and on the 500 ErrorState and 404 not-found pages: zero serious/critical, both projects |
| X-d | Shared error helpers changed: cat and account e2e specs, one project at a time | Pass; nothing shows the wrong message | PASS (excluding `account-area.spec.ts`, see X-e) | shared error helpers changed, so the cat and account e2e specs were run, one project at a time, one dev server, `--workers=1` (account-area, accounts-auth, staff-auth, staff-admin, staff-customers, cat-deceased, cat-error-handling, cat-medical-history-and-cage-card, cat-pagination, cat-parasite-control, cat-profile-header, cat-register, cat-transfer, cat-vaccinations, cat-weights; a11y/responsive/shell/home/real-api specs not part of this run). chromium: 37 passed, 6 failed (5 `account-area`, 1 `staff-admin` E-F1); mobile-360: 37 passed, 6 failed (5 `account-area`, 1 `staff-customers` E-H1). The two singleton failures were not reproducible: `staff-admin` E-F1 (the nurse-gets-`PermissionDeniedState` test, so I chased it) passed 2/2 chromium in isolation and 10/10 with `--repeat-each=5` on both projects; `staff-customers` E-H1 passed in isolation on both projects (after ~5 min of one serial run) — treated as environment. No test showed a wrong or generic message; `cat-error-handling` E20 (409 wording) and E21 (500 wording) passed on both projects |
| X-e (new) | `account-area.spec.ts` (HO-ACC-002 e2e) on this branch, and at the pre-fix commit `bf7d3d0` (temporary detached worktree, removed) | Passes | FAIL (MEDIUM, pre-existing, not from fa80c47) | 5 of its 8 tests fail on both projects at `fa80c47`-era HEAD **and identically at `bf7d3d0`** (E-E1 add addresses, E-E2 SMS marketing, the three unsaved-changes Back tests). Cause seen in the failure output: the "Updated policies" `PolicyAcceptancePrompt` dialog is open over the portal and intercepts clicks (`<h2>Updated policies</h2> ... subtree intercepts pointer events`; the Back test finds that dialog instead of "leave this page"). HO-ADM-002's own mock seeds a pending policy acceptance for the seeded customer (`mocks/handlers/admin/content.ts`, from `14ea722`) and the customer layout shows the prompt. Not caused by the 403 fix; found by this pass's cross-area run. Reported for the backlog |

## Regression cases from earlier gates

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| R1 | ARCHITECTURE (1st) | `lib/branch.ts` built as a module-level store + `useSyncExternalStore`, outside §13, HO-ACC-002 §4.3 global-store rule | Active branch now lives in TanStack Query `['staff','active-branch']`; `registerQueryClient`/`getActiveBranchId` wired through an effect, not the `useState` initialiser | PASS | Read `lib/branch.ts`/`lib/hooks/useActiveBranch.ts`/`app/providers.tsx` directly — no module-level store, no `useSyncExternalStore`; confirmed by U-B5, X-a, X-b all passing against this shape |
| R2 | ARCHITECTURE (2nd) | Strict-Mode double-invoked `useState` initialiser could register the discarded `QueryClient`, silently dropping `X-Branch-Id` | E-B2 (real dev server, Strict Mode on) proves the header is sent | PASS | E-B2 passed against the real `next dev` (Strict Mode on by default, unmodified `next.config.ts`) on both chromium and mobile-360; `registerQueryClient()` confirmed called from `useEffect` in `app/providers.tsx`, not the lazy `useState` initialiser |
| R3 | SECURITY | `LimitedMarkup`/diff rendering must have no HTML path | U-F1, U-F2, U-G2 | PASS | All three watched passing in the green `vitest run`; `LimitedMarkup.tsx`/`DiffTable.tsx` read directly, no `dangerouslySetInnerHTML` in either |
| R4 | CODE_REVIEW (1st, HIGH) | Leave reject dialog never collected a note; every rejection sent an empty note | U-D4 / X-c | PASS | Watched directly — see U-D4/X-c above |
| R5 | TEST (1st, HIGH) | Audit-log and content list filters used `router.replace` for every change, so no history entry was ever created and Back left the page | U-G4, U-G5 | PASS | Watched directly, both pages, both chromium and mobile-360 — see U-G4/U-G5 above. `ce14242`'s `useFilterUrlUpdate` pushes one entry per filter edit (same-field keystrokes within 1s replace their own entry); re-confirmed still true against `ec00965` this pass, independently |
| R6 (new, fourth pass) | TEST (3rd, HIGH) | `expectedQuery`/single-last-write comparison in both hooks could mistake an intermediate, already-superseded write of its own for an external navigation, dropping one character mid-keystroke, non-deterministically, at ordinary typing speed (150ms/key) | U-G6 | PASS | `ec00965`'s `pendingWrites` list (both hooks) + debounced free-text URL writes; re-verified independently this pass — 0/30 `action`-typing attempts (10 at 150ms/key) and 0/12 `entityId` attempts failed, across both chromium and mobile-360 — see U-G6 above |

Note: this pass closes the second/third passes' own HIGH (U-G6 — silent keystroke/edit loss),
tracked here as R6. A new, distinct, MEDIUM anomaly (U-G7, debounce-edge Back timing) was found
while re-verifying this exact fix — see FINDINGS below; it has no regression row of its own since
no earlier gate raised it.

## Acceptance-criteria coverage

| Criterion | Covered by | Status |
|---|---|---|
| A1 — branch hours/holidays/rooms in the branch's zone | U-B1…U-B4, E-B1 | PASS (E-B1's calendar-visualisation half is a named gap, not a failure — see Gaps) |
| A2 — second branch + assignment, no code change | U-B5, E-B2, X-a, X-b | PASS |
| A3 — role that cannot use a page sees server `403` `PermissionDeniedState` | U-A1, P1–P22, B1–B3 | **PASS, fifth pass** — every fixed page (tax-class, service, roster, system, settings, closures, rooms, `/staff/leave`) shows `PermissionDeniedState` on a 403, other errors show a retry ErrorState and 404 a not-found state, none spins; forms show a permission message; real browser confirmed for tax-class and roster on both projects. Caveats: the 403 is injected in the browser (mock has no role enforcement); dependent-query 403s replace an already-loaded page and the two branch-assignment codes show `PermissionDeniedState` (backlog row 34) |
| A4 — rosters, exceptions, leave; approved leave visible on roster | U-D1…U-D4, E-D1 | PASS |
| A5 — price/tax changes scheduled, never edited; clear history | U-C1, U-C2, E-C1 | PASS |
| A6 — every setting editable from the server's registry, old→new confirm | U-E1…U-E3, E-E1 | PASS (setting round-trip proven; E-E1's cat-profile downstream half BLOCKED — out of §13 scope) |
| A7 — audit entries filterable and exportable | U-G1…U-G7, E-G1 | **PASS, fourth pass** — back-button safety (U-G4/U-G5/R5), the filter race (U-G6, closed this pass), and deep links (E-G1) are all PASS, re-confirmed against `ec00965`; filtering/export themselves still work (U-G1's block, U-G2, U-G3/E-G3, E-G1 all PASS). U-G7's new debounce-edge anomaly is MEDIUM, narrow-timing, and does not corrupt what the user sees on screen (field and result list stay consistent with each other) — not a criterion failure on its own terms, flagged for backlog |
| A8 — pages/FAQs/banners/policies edited, previewed, published; customer acceptance | U-F1…U-F3, E-F1, E-F2 | PASS |
| A9 — maintenance switch never blocks the back office | E-H1 | PASS |
| A10 — zero serious/critical axe; 360–1440 px | E-X1, E-X2 | PASS — 132/132 chromium, 132/132 mobile-360, both watched passing on a clean per-project rerun |
| A11 — no HTML rendering path exists | U-F1, U-F2 | PASS |

## Cases I could not run

| ID | Why | What would unblock it |
|---|---|---|
| Every §16 case's real-API re-run (§5.1) | No reachable PostgreSQL/`dotnet` API process this session | Start the real API + Postgres per §5.1 and re-run `test:e2e:real-api` |
| E-E1 (downstream half) | HO-CAT-002's own vaccination-status page/mock is outside this handover's §13 scope to read, edit or add an e2e case for | A HO-CAT-002-scoped follow-up test, or an explicit owner decision to extend scope |

## Findings — first pass (RESOLVED this pass)

- **[HIGH, RESOLVED] U-G4 — the back button did not restore a previous audit-log/content-list
  filter, contradicting §8.3's own explicit "back-button safe" decision.** Root cause was
  `handleFilterChange`/`setParam` calling `router.replace(...)` for every filter change, so no
  history entry was ever created. Fixed by `ce14242` (`web/lib/hooks/useFilterUrlUpdate.ts`):
  `router.push` per filter edit, `router.replace` only for same-field keystrokes inside a 1s
  typing burst so Back steps over whole edits, not characters. Re-verified live this pass on both
  audit-log and content list, both chromium and mobile-360 — see U-G4/U-G5/R5. **No longer
  blocks the gate**, but see the new Findings below, found while re-verifying this exact fix at
  real keystroke speed.
- **U-G1's "default last 7 days" half is correct but untested in the committed suite** — unchanged
  this pass, still a LOW coverage gap (`docs/backlog/ADM.md` row 18 already records it).

## Findings — second pass (new)

- **[HIGH] U-G6 — filter fields driven by `useFilterUrlUpdate` can silently lose a keystroke or an
  entire edit under realistic, ordinary use, well short of any artificial or superhuman speed.**
  Found while re-verifying `ce14242`'s back-button fix in a real browser at real keystroke timing
  (the task's own "several keystrokes" instruction) — not present in the fix's own unit tests,
  which call the hook directly with already-computed, cumulative string values and never render
  the actual `<input>`, so they cannot see this. Root cause: every field's value is a *pure*
  function of the URL (`value={filters[field] ?? ''}`, read via `useSearchParams()`), and every
  keystroke/selection triggers an async Next.js client-side navigation (`router.push`/`replace`)
  before the new value is reflected back into `filters`. If a second edit (another keystroke, or a
  second filter's change) fires before the previous navigation has committed, the component
  re-renders with the *stale* `filters` value, the controlled `<input>`/`<select>` snaps back to
  it, and the new edit is computed relative to that stale base — earlier characters/edits are
  silently overwritten, not merged. This is **not specific to `router.replace`** (the same-field
  burst path) — it reproduces identically off `router.push` too (a different field, or two fast
  `<select>` changes), so it predates `ce14242` and is not something that commit introduced; it is
  a property of the `value`-from-URL pattern §8.3 itself specifies, combined with the async
  round-trip on every single edit. Reproduced directly, real browser, `PORT=3102`, chromium
  (temporary, uncommitted debug specs, removed after this run — not the committed suite):
  - Typing `action` on `/staff/admin/audit-log` with `pressSequentially(..., {delay})`: at
    `delay: 50`/`80`/`100` ms/keystroke the field settled on `"e"` / `"irstalue"` / `"FistValue"`
    respectively (target was `"FirstValue"`) — most or all characters silently lost. At
    `delay: 150` ms/keystroke it *usually* completed correctly but dropped a character
    (`"FistValue"`) on one run — confirming this is a genuine, non-deterministic **race**, not a
    fixed "too fast" cutoff a slower typist is safe from. Typing `entityId` at 80ms/keystroke lost
    3 of 8 characters (`"abc12345"` → `"abc15"`).
  - `/staff/admin/content`: two back-to-back `<select>` changes with no pause (`kind` then
    `status`, Playwright's default zero inter-action delay — i.e., a user who sets two filters in
    quick succession without waiting for the page to settle between them) landed on
    `status=Draft` only; the `kind=Faq` edit was silently lost entirely.
  Impact: an administrator searching the **audit log** — a compliance/investigation tool — who
  types a known entity ID or action code at a normal-to-fast pace, or sets two filters quickly,
  can get a filter silently different from what they typed, with no error shown; the most likely
  visible symptom is "no results" or a wrong result set, easily misread as "there's nothing here"
  rather than "the search box didn't record what I typed." Classified HIGH, not MEDIUM: it is
  confirmed and reproducible in a real browser (not a synthetic/theoretical edge case), affects
  both pages under this hook, and silently corrupts input in a compliance-sensitive feature with
  no error surfaced to the user. Not a regression of `ce14242` and not fixed by it — reported here
  because this pass's closer, real-keystroke-timed re-verification of the back-button fix is what
  surfaced it; not something the first pass's `.fill()`-style single-shot check could have seen.
  Fix direction (for the implementer, not attempted here — TEST gate reports, does not fix):
  give the input/select local `useState` as the source of truth for display (synced *from* the URL
  on external navigation, e.g. Back/Forward), and treat `useFilterUrlUpdate`'s `router.push`/
  `replace` as a write-behind side effect rather than the field's only source of truth — the same
  shape `WeeklyHoursEditor`/other form components in this codebase already use for locally-typed
  state.

## Findings — third pass (new)

- **[HIGH, NARROWED — NOT RESOLVED] U-G6 — `8498378` (`useUrlFilterState.ts` local-state fix)
  removes the second pass's whole-string-loss symptom but a rarer, single-character-loss variant
  of the same underlying race survives, reproduced on both chromium and mobile-360.** The fix does
  what it says: display is no longer a pure function of `useSearchParams()`, so a controlled
  field/select no longer snaps back to a stale URL value on every intervening render. But the
  `expectedQuery`/`pendingParams` resync (both `useUrlFilterState.ts` and
  `useFilterUrlUpdate.ts`) still compares the *live* query string against what this hook itself
  most recently asked the router for, and that comparison still races against however long the
  actual `router.push`/`replace` transition takes to land — which this gate measured directly
  (see the gate report) as sometimes several hundred milliseconds, not the near-instant the fix's
  own doc comments seem to assume. When a resync effect fires believing the URL changed "for a
  reason other than this hook's own last write" at exactly the wrong moment mid-keystroke, it
  overwrites local state with an intermediate — not final — URL value, one character short of
  what was actually typed. Reproduced directly, real browser, `PORT=3102` (temporary, uncommitted
  probe, removed after this run — not the implementer's own committed `admin-filters.spec.ts`,
  which uses only 50ms/30ms and did not surface this in this gate's testing):
  - `action` typed at 150ms/key, `pressSequentially`, target `"FirstValue"`: **4/4** failed in one
    `--repeat-each=5` chromium session (`"FistValue"` ×2, `"FirtValue"`, and one run in a separate
    5-repeat session was clean 4/5, 1/5 failed differently — `"FirstVale"`) and **2/3** failed in a
    mobile-360 `--repeat-each=3` session (`"FirsValue"`, `"FirstVale"`) — a different character
    lost each time, confirming a genuine non-deterministic race, not a fixed cutoff.
  - The same `action` field at 50/80/100ms/key and `entityId` at 80ms/key passed cleanly across
    several 3-attempt and 5-delay sweep sessions (50/80/100/150/200/250ms all clean in one sweep),
    confirming the race is real but now infrequent and not tied to any one specific speed — one
    isolated flake was also seen at exactly the committed suite's own 50ms/key parameter
    (`"ServicePrieSet"` for `"ServicePriceSet"`) in an otherwise-clean 3-run session.
  - `kind`/`status` (content list, no pause) and the deep-link cases were clean across every run
    this gate made, both projects.
  Severity kept at **HIGH**, not downgraded to MEDIUM: the underlying defect this gate's second
  pass classified as HIGH (silent, unannounced corruption of what a compliance-tool search field
  shows) is unambiguously still present and reproducible without any contrived timing — 150ms/key
  is an ordinary typing speed, not an edge case — only its frequency has dropped. A bug that
  corrupts audit-log search input a fraction of the time is not qualitatively safer than one that
  does it most of the time; a user cannot tell, from the UI, which runs are affected. Not
  attempted here — this is a report, not a fix. The most likely next-step direction: the
  `expectedQuery` comparison needs to account for the possibility that the *live* query string can
  reflect an intermediate state of this hook's own in-flight writes (not just "my last write" vs.
  "someone else's write"), e.g. by tracking a monotonically increasing write sequence number rather
  than a single last-expected string, or by not resyncing from the URL at all except on a signal
  that is unambiguously external (a real `popstate`/navigation event was tried and rejected per
  `useFilterUrlUpdate.ts`'s own doc comment, for a different but related ordering reason — a
  sequence-number or "ignore resync entirely for N ms after any local write" approach may avoid
  both problems).

- **[MEDIUM, new, not blocking — for backlog] `router.push`'s actual commit to the browser's real
  History API can lag well behind both (a) the field's own local-state display and (b)
  `page.url()`/the visible address bar, by several hundred milliseconds — long enough that a
  native browser Back press issued in that window can produce a Back result inconsistent with
  what was on screen.** Found while investigating an apparent redirect-to-sign-in this gate's own
  exploratory Back/Forward test hit; isolated with a debug script instrumenting
  `history.length`/`history.state` directly: after typing `action` fully (confirmed via the DOM
  value) and waiting the committed spec's own 300ms "settle" gap, `history.length` had **not yet
  incremented** — the real history entry for that edit had not yet been created, even though the
  field displayed the typed value throughout (local-state fix working as intended) and even though
  `page.url()` can already match the edited value by that point in some runs. Pressing the native
  Back button in that window does not "undo the last visible edit" the way a user would expect —
  it goes back relative to wherever the *actual*, not yet caught-up, browser history position is,
  which can be one or more edits further back than what is currently on screen. **Not the same
  finding as U-G6** (no character/edit is lost — the field's own display is correct throughout;
  this is purely about what happens if Back is pressed before the URL/history genuinely settles).
  **Does not affect the committed `admin-filters.spec.ts`'s own correctness**: every Back-pressing
  test in that file confirms the preceding edit's URL via `toHaveURL` (an auto-retrying assertion)
  before calling `goBack()`, which in practice waits long enough for the real commit too — verified
  by this gate with 5 repeated runs of that exact test, 5/5 clean. Classified MEDIUM, not HIGH: it
  needs an unusually fast native-Back click within a sub-second window right after an edit — a real
  but narrow scenario — and the worst outcome (Back travels further than the user expected,
  potentially off the page) is inconvenient but recoverable, not silent data corruption. Flagged
  for `docs/backlog/ADM.md`, not blocking this gate. This is also this gate's answer to the task's
  question about the committed spec's own 300ms wait (see the gate report): the wait itself is
  largely a comfort margin, not the thing making the test reliable — the `toHaveURL` assertions
  either side of it are — but the suite has no case at all for "native Back pressed before the URL
  visibly settles," which is exactly the gap this finding describes.

## Findings — fourth pass (new)

- **[MEDIUM, new, not blocking — for backlog] U-G7 — a debounced free-text filter field
  (`AuditLogSearch`'s five text inputs) that is mid-debounce (typed, not yet blurred, less than
  ~300ms since the last keystroke) when the user presses the browser's native Back button ends up
  with its displayed value and the address-bar URL disagreeing, and can silently overwrite a
  Forward-navigable history entry the user had already explicitly committed.** Found while
  executing this pass's own probe for exactly the scenario the task asked to try ("the debounce
  edge: type, then press Back within 300ms"). Root cause: `useUrlFilterState.ts`'s debounce timer
  is a plain `setTimeout` held in a ref — nothing about a Back/Forward navigation cancels or is even
  aware of it. Sequence reproduced identically on both chromium and mobile-360 (deterministic given
  this exact timing, not a race):
  1. Type "Before" into `action`, blur (flushes immediately via `onFilterCommit`) — URL becomes
     `action=Before`, a real, settled history entry.
  2. Type "Edge" into `action` (no blur), and call `goBack()` immediately — well inside the 300ms
     debounce window, before any URL write for "Edge" has even been requested.
  3. Back correctly resyncs the **displayed** field to `""` (nothing in the URL for `action` at the
     entry Back landed on) — this part is correct and is not the same defect as U-G6, since no
     character is corrupted and the field/result-list stay internally consistent with each other.
  4. ~300ms after step 2's last keystroke, the debounce timer fires anyway (it was never told about
     the Back) and pushes `action=Edge` onto the URL as a **new** history entry, built from
     `pendingParams` as it stood after the Back's own resync (i.e., everything else at its
     pre-"Edge" value).
  5. End state, watched directly: the field displays `""`, but `page.url()` shows `...&action=Edge`
     — the two disagree — and the step-1 `action=Before` entry the user could previously have gone
     **Forward** to has been silently discarded (a `pushState` after a Back always truncates the
     forward stack).
  Impact: narrow but real — a user who types into the audit-log search, changes their mind and
  clicks Back within roughly a third of a second (no blur first) sees the field clear as expected,
  but the address bar quietly drifts to a value not shown anywhere on screen, and their own
  previous, already-committed search (reachable via Forward before this happened) becomes
  unreachable that way. Not silent data corruption in the sense U-G6 was (nothing the user is
  looking at is wrong — the list and the field agree with each other throughout), and a page reload
  at that point would simply show the stale-but-real "Edge" filter re-applied, which is confusing
  but not a corrupted value. Classified **MEDIUM, not HIGH**, on the same reasoning the third pass
  used for its own related (but distinct) native-Back-timing finding: it needs an unusually fast,
  specific sequence (type, no blur, Back, all within ~300ms) and the worst outcome (a stale URL, a
  lost Forward step) is confusing and requires an explicit user action to trigger, not automatic
  silent corruption of what is displayed. Not attempted here — this is a report, not a fix. Likely
  next-step direction (for the implementer, not this gate): cancel every pending debounce timer
  (and, ideally, flush or discard it — flush is probably wrong here, since the user is navigating
  away from the edit, so discarding is likely closer to what a user expects) on the same "genuinely
  external navigation" signal `useUrlFilterState.ts` already computes for its own resync — the
  `pendingWrites`-miss branch already distinguishes "external nav happened" from "my own write
  landed"; the debounce-timer cleanup that already exists for unmount (see the existing `useEffect`
  cleanup a few lines above `setValue`) would need to run there too, not just on unmount.
  For `docs/backlog/ADM.md` — not blocking this gate.

## Findings — fifth pass (new)

No BLOCKER or HIGH. `fa80c47` meets A3 for every case in P1–P22 and B1–B3.

- **[MEDIUM, new] X-e — `tests/e2e/account-area.spec.ts` (HO-ACC-002) fails 5 of 8 tests on both projects on this branch, identically at `bf7d3d0` (before the fix).** The blocking "Updated policies" dialog covers the customer portal because HO-ADM-002's own mock seeds a pending policy acceptance for the seeded customer and the portal layout shows `PolicyAcceptancePrompt`. Not from `fa80c47`; found while running the cross-area specs the fix's shared-helper change called for. Merge risk for the account area's e2e suite: either the spec must accept the prompt or the mock must not seed one for the default customer.
- **[LOW, new]** The two dependent-query 403s (tax-class rates, service prices) and the branch-assignment-code 403s render `PermissionDeniedState` — recorded as P2, P6, P21; already covered by backlog row 34, not re-raised.
- **[LOW, new, environment]** In one serial run of 43 cat/account tests, one unrelated test per project failed once and passed on isolated rerun (E-F1 chromium, E-H1 mobile-360) — same "long serial run against one `next dev`" pattern as backlog row 1; no product cause found.

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| E-B1's "calendar in the detail view skips the holiday" | §11.2's own component list names no calendar-visualisation component (`WeeklyHoursEditor`, `ClosureList`/`ClosureForm`, `RoomList`/`RoomForm` only); the mock's `GET .../calendar` endpoint exists for contract completeness with no FE consumer. Flagged by the handover's own Amended (5), not silently dropped here | Human reviewer / product decision — build the component, or drop the calendar line from §16 |
| U-G1 default-range coverage | See Findings above — behaviour correct, coverage missing | Implementer follow-up: an e2e or page-level test for the no-querystring default |
| U-G4/U-G5 back-button REGRESSION coverage — **RESOLVED, third pass** | `8498378` committed `tests/e2e/admin-filters.spec.ts`, which presses a real Back on both audit-log and content list (and Back-twice on content list) — this gap is closed; no longer only this gate's own temporary spec | — |
| U-G6 (second pass; narrowed third pass; **CLOSED, fourth pass**) — filter-field edit-loss race | See Findings above — `ec00965`'s `pendingWrites` list + debounced free-text writes closed the race this gate's second and third passes tracked; 0 failures in 30 `action` attempts (10 at 150ms/key) and 12 `entityId` attempts per project, both chromium and mobile-360, this pass's own independent probe | — |
| New, third pass — native Back pressed before a `router.push` has actually committed to the real browser History API can travel further than the currently-displayed state implies | See third-pass Findings above — MEDIUM, not blocking; needs an unusually fast native-Back click in a sub-second window right after an edit; no committed case covers this scenario (every committed Back-pressing test waits on `toHaveURL` first, which in practice avoids it). Not re-verified this pass (fourth pass's own diff, `ec00965`, does not touch the `router.push`-commit-timing mechanism this finding is about) | Implementer/owner follow-up: decide whether to close the window (e.g. block Back handling until commit, unlikely to be practical) or accept it as a narrow, recoverable rough edge and document it |
| New, fourth pass — U-G7, debounce-edge Back timing can leave the field and URL disagreeing and silently discard a committed Forward-navigable entry | See fourth-pass Findings above — MEDIUM, not blocking; needs Back pressed within ~300ms of an unblurred keystroke in a debounced field, before this pass's own probe surfaced it | Implementer follow-up: cancel (not flush) a debounced field's pending write on the same external-navigation signal `useUrlFilterState.ts`'s own resync effect already computes |
| `admin-a11y-responsive.spec.ts` cannot run combined (`--project=chromium --project=mobile-360`) in one `--workers=1` invocation | Backlog row 1 already names this environment limitation for `admin-functional.spec.ts` (18 tests); this file is far larger (132 tests/project, 264 combined) and a combined run degraded badly partway through chromium — high failure rate, `Loading…` stuck past timeout, dev-server memory climbing — never actually finished in ~20 minutes before being killed. Each project run **separately** (fresh `next dev` each time) passed 132/132 cleanly, second, third and fourth passes alike (fourth pass: both projects clean on the *first* attempt, no rerun even needed). Not a product bug. Third pass additionally found: running two `next dev` processes from the *same* `web/` directory concurrently (even on different ports) can itself cause spurious failures via shared `.next` build-cache contention — an artifact of that pass's own parallel investigation, not a product issue, but worth naming so a future gate doesn't rediscover it the hard way; this pass avoided it by construction (one `next dev` process at a time throughout) | Implementer/owner follow-up: extend backlog row 1's fix (or a CI-only production-build run) to cover this file too, or split it into two smaller spec files |
| Fifth pass — a real role-driven 403 in the browser | The admin MSW handlers do not model roles and the mock has no signable Accountant/ShopManager, so B1/B2 inject the 403 at `window.fetch` for a real Nurse session. Whether the real API returns the 403 for those roles is the API gate's (HO-ADM-001) claim, only checkable in the real-API run | Integrator real-API run (backlog row 24) |
