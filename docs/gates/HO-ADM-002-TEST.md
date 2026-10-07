GATE: TEST
HANDOVER: HO-ADM-002
COMMITS: f93b790 6f9da0a 5598fa5 14ea722 b7f18d9 338834f 994305c f2a8970 b7b244d ceb0019 62178ab 2299141 2106914 09b6277 c7f29d7 96889d2 b7065fc ffb1918 ce14242 d0a3758 f26cf1a 8498378 12bbe1a ec00965 bf7d3d0 313b89e fa80c47 9ce0541 e535110 (range 08e873e..e535110)
DATE: 2026-09-29
STATUS: PASS

**This is the fifth pass.** The fourth pass (PASS, `bf7d3d0`) closed the filter race. ACCEPTANCE then
failed §18 A3 (`313b89e`): a server 403 was not shown as `PermissionDeniedState` (tax-class and service
detail spun forever, the roster showed a generic error, forms said "Something went wrong"). `fa80c47`
fixes the pages (tax-class, service, roster, system, settings, closures, rooms, `/staff/leave`) and
adds `PERMISSION_DENIED_MESSAGE` / `describeUnexpectedError` to `lib/api/errors.ts`, used by ten admin
forms; `describeApiError` and `submitWithServerErrors` are shared, so about 20 forms in other areas
now show the permission message on a 403. ARCHITECTURE and CODE_REVIEW delta passes are PASS.

Sheet: `docs/test-cases/TC-HO-ADM-002.md`, new section "Permission-denied and error-state cases (A3)".
All 28 new rows were written `NOT RUN` before anything ran, then executed. Counts: **77 total, 74
PASS, 2 FAIL (U-G7 carried from the fourth pass; X-e new; both MEDIUM), 1 BLOCKED (E-E1, carried), 0
NOT RUN.**

**Verdict on A3: met.** Every fixed page shows `PermissionDeniedState` on a 403, a retry ErrorState on
a non-403 error, a not-found state on a 404, and none spins; forms show the permission message on a
403 and keep the generic text otherwise; the two branch-assignment codes are recorded (below).

Exact counts per run (all in `web/`, `PORT=3102`, `--workers=1`, one `next dev` at a time):
- `npx tsc --noEmit`: clean. `npm run lint`: clean.
- `npx vitest run`: **84 files / 311 tests passed** (41.0s). The 9 new tests (`detail-pages-403.test.tsx` 6,
  `form-403-message.test.tsx` 3) also watched individually with `--reporter=verbose`.
- Temporary component test (18 cases, removed): 17 passed first run; the 18th (roster 500) hit my own 5s
  default timeout (the hook retries once after 1s), rerun with a 15s timeout passed. Not a product fault.
- Temporary browser spec (removed): chromium 5/5, mobile-360 5/5.
- `admin-functional.spec.ts` + `admin-filters.spec.ts`, both projects: **30/30 passed** (18 + 12).
- Cat and account e2e (15 spec files, 43 tests per project, one project at a time): **chromium 37 passed,
  6 failed; mobile-360 37 passed, 6 failed.** Failures: 5 `account-area.spec.ts` per project (pre-existing,
  see FINDINGS) plus one flake per project (chromium `staff-admin` E-F1, mobile-360 `staff-customers`
  E-H1) that passed on isolated rerun (E-F1: 2/2, then 10/10 with `--repeat-each=5` across both
  projects; E-H1: 2/2 in isolation).
- `admin-a11y-responsive.spec.ts`, fresh dev server per project: **chromium 132/132 (6.7m), mobile-360
  132/132 (6.6m)**, both clean on the first attempt, no cold timeout, nothing to rerun.

Real-browser check (task item 2). Signed in through the MSW staff sign-in as the seeded **Nurse**
(`nurse@purrcare.test`). **How the 403 was produced:** the admin MSW handlers do not model roles
(`mocks/handlers/admin/fixtures.ts` says so), and a control test confirmed the Nurse gets the tax-class
page with nothing injected. A `page.addInitScript` wrapper on `window.fetch` answered `GET
/api/admin/tax-classes*` (and, for the roster, `.../(rosters|availability|roster-exceptions)`) with a
real 403 `Response` carrying `{errors:[{errorCode:"ActionNotPermitted"}]}`; the app's own `apiFetch` and
`ApiForbiddenError` path ran unmodified, and the test asserted the wrapper fired. Results, both
projects: tax-class detail shows `role=alert` "You don't have permission to do this", and 2.5s later
still no "Loading…" and no error code or "Something went wrong"; the roster page the same; a 500 shows
"couldn't load this tax class" with Try again; a 404 shows "Tax class not found". axe on the
`PermissionDeniedState` pages (tax-class and roster) and on the 500 and 404 pages: zero serious or
critical. Caveat: the roster was checked as the Nurse, not Accountant or Reception, because the mock has
no signable Accountant/ShopManager (the seeded Reception has no password) and the Nurse legitimately has
`Rosters.View`; the 403 is injected exactly as for the tax class.

Recorded behaviours (not failures): a 403 on a dependent query (tax-class rates, service prices) replaces
the already-loaded detail page with `PermissionDeniedState`; a detail-page GET 403 carrying
`BranchNotAssigned` or `BranchAssignmentRequired` shows the generic `PermissionDeniedState`, not branch
guidance. Both are already backlog row 34, not re-raised. The form-level wording for those two codes
(`describeApiError`) is correct and tested.

FINDINGS

- [MEDIUM, new, not blocking] `web/tests/e2e/account-area.spec.ts` — 5 of its 8 tests fail on both projects
  on this branch, and fail identically at `bf7d3d0` (checked in a temporary detached worktree, since
  removed), so this is not from `fa80c47`. The "Updated policies" `PolicyAcceptancePrompt` dialog is open
  over the customer portal and intercepts clicks; HO-ADM-002's own mock seeds a pending policy
  acceptance for the seeded customer (`mocks/handlers/admin/content.ts`, from `14ea722`). Found because
  the shared-helper change called for a cross-area e2e run. Merge risk for the account area's suite.

MEDIUM/LOW (for `docs/backlog/ADM.md`, not editing it — owner rule 2026-09-27):
- [MEDIUM, new] `account-area.spec.ts` 5 failures per project, pre-existing at `bf7d3d0`: seeded pending policy acceptance blocks the portal (above).
- [LOW, new, environment] one unrelated cat/account test per project failed once in a 43-test serial run and passed on isolated rerun (chromium `staff-admin` E-F1, mobile-360 `staff-customers` E-H1): the row 1 pattern, no product cause found.
- [LOW, new] the real API's 403 for Accountant/ShopManager on tax classes and rosters, as the mock cannot model roles, is only checkable in the real-API run (backlog row 24).
- Rows 1-38 not re-raised; U-G7 (fourth pass, backlog row 22) unchanged and not re-verified (filter hooks not in this delta).

Gaps carried into the sheet, not hidden: every §16 real-API re-run is BLOCKED (no Postgres/dotnet);
E-E1's downstream half BLOCKED (outside §13); E-B1's calendar has no component; the injected browser 403 above.

SCOPE CHECK
- files in allowed scope: 2/2 (`docs/test-cases/TC-HO-ADM-002.md`, `docs/gates/HO-ADM-002-TEST.md`)
- files outside allowed scope: none. No product code edited. Temporary files, all removed before
  finishing: `web/tests/components/admin/tmp-a3-gate.test.tsx`, `web/tests/e2e/tmp-a3-browser.spec.ts`,
  the detached worktree `cat_ecommerce-wt/tmp-bf7d3d0` (with its `node_modules` junction, removed
  without touching the real `node_modules`), `web/test-results`. `git status` shows only the two files above, uncommitted.
