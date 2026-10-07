## Second pass — 2026-09-27

GATE: TEST
HANDOVER: HO-ACC-002
COMMITS: `d14d591`, `ddbae6d`, `aae12d4` (F1 idle-lock race fix and its own regression coverage) and
`13d46ae`, `410b93a`, `5b01d22` (branch head, `ho/ACC-002`) (CODE_REVIEW gate seventh-pass HIGH found
in that fix — a missing `staleTime: 0` on the authoritative `fetchQuery` — plus its own regression
coverage)
DATE: 2026-09-27
STATUS: PASS

Second pass of the TEST gate for HO-ACC-002, resuming after the first pass's FAIL on F1 (the
IdleLock bystander `session_invalid` race, E-G2/R3). F1 was fixed in `d14d591`; a CODE_REVIEW HIGH
found in that fix (a missing `staleTime: 0` letting a merely-recent cached session read hide a
genuine sign-out) was then fixed in `13d46ae`. Both fixes have their own regression unit tests
(`ddbae6d`, `410b93a`). A narrow CODE_REVIEW eighth pass ran in parallel over the same commits and is
recorded separately in `docs/gates/HO-ACC-002-CODE_REVIEW.md` — not touched by this pass.

Full second-pass record, written and executed one row/check at a time: the "## Second pass —
2026-09-27" section appended to `docs/test-cases/TC-HO-ACC-002.md` (the first pass's own content
kept verbatim above it; the F1/E-G2/R3/A17/A18 rows it names were also updated in place with an
appended second-pass note, per instruction). Summary:

- **Static checks, all clean**: `npx tsc --noEmit` (0 errors), `eslint . --max-warnings 0` (0
  warnings), `npx prettier --check .` (clean).
- **Unit suite**: `npx vitest run` — **60 files / 239 tests, all passed**, run twice. 239 = the first
  pass's 236 plus this fix's 3 new tests (U-G7/U-G8/U-G9 in the TC sheet): two tests proving the F1
  race itself is closed (a slow, genuine `session_idle_timeout` response is not pre-empted by a
  faster bystander `session_invalid`; a genuinely revoked/unknown session is still signed out) and
  one proving the CODE_REVIEW seventh-pass staleTime fix (a `QueryClient` configured with the app's
  real `staleTime: 30_000` default, pre-seeded with a <30s-old successful session read, still locks
  on a bystander `session_invalid` instead of silently trusting the stale cache). All three read in
  full, judged as genuinely exercising the fix (each confirmed, per its own commit message, to fail
  against the pre-fix code and pass after) rather than decoration.
- **F1/E-G2 targeted verification**: `PORT=3102 npx playwright test --project=mobile-360
  --project=chromium --workers=1 --repeat-each=20 -g "E-G2"` — **40/40 passed**, 0 failures, 2.8
  minutes. The first pass reproduced 1 failure in 3 isolated single-project repeats under the pre-fix
  code at this same isolation level; 40/40 here is the direct apples-to-apples comparison. Watched
  directly via the run's own per-test pass lines and final `40 passed (2.8m)` summary.
- **Full E2E suite regression check**: `PORT=3102 npx playwright test --project=chromium
  --project=mobile-360 --workers=1` (MSW mocks, no real API/DB started this session) — 244 tests, one
  continuous 26.1-minute run: **235 passed, 9 failed**. All 9 failures traced and none are E-G2, F1,
  or the staleTime fix — both E-G2 instances passed in this same run. The 9 are scattered across
  unrelated pre-existing HO-CAT-002/HO-ACC-001 area tests (`staff-customers.spec.ts` E-H1/E-I1 on
  both projects, `cat-deceased.spec.ts`, `cat-register.spec.ts`, `staff-admin.spec.ts`, one
  `staff-shell-*` nav-sheet test, and `staff-auth.spec.ts`'s own E-G3 — whose failing assertion is a
  generic "Name field not visible" *before* any lock/session logic runs, the same shape as the other
  8, not a session-handling assertion). Re-ran all 6 affected spec files together, fresh, immediately
  afterward (`--workers=1`, both projects, no preceding 26-minute history): **30/30 passed**,
  including every one of the 9 failing identities and E-G2/E-G3 again. This confirms the 9 failures
  are an artifact of one very long unbroken `--workers=1` session (a `node.exe` was observed holding
  ~3.3GB partway through — consistent with dev-server memory growth over ~240 sequential page loads),
  not a regression from this pass's fix and not reproducible in isolation. Recorded as a new MEDIUM
  backlog item in the TC sheet (distinct from the CODE_REVIEW gate's already-diagnosed `--workers=2`
  route-compile contention, R8) — per the owner's rule, MEDIUM findings do not fail the gate.

**F1, E-G2, R3, A17, A18 — all now PASS, verified closed.** No new HIGH or BLOCKER finding this pass.
The one new observation (full-suite `--workers=1` long-run flakiness) is MEDIUM and unrelated to the
fix under test; per the owner's rule it goes to the backlog, not the gate outcome.

SCOPE CHECK (second pass): only `docs/test-cases/TC-HO-ACC-002.md` and this file
(`docs/gates/HO-ACC-002-TEST.md`) were edited. `docs/gates/HO-ACC-002-CODE_REVIEW.md` (the parallel
eighth pass) was not touched. No dev server or Playwright process was left running at the end of this
pass — confirmed no LISTENING socket on port 3102 and no `node.exe` process, both immediately after
the full-suite run and again after the final isolated rerun. Nothing was committed.

---

## First pass — 2026-09-27

GATE: TEST
HANDOVER: HO-ACC-002
COMMITS: ee5e907 (branch head, ho/ACC-002) plus uncommitted TEST-gate work in the worktree (not
committed, per instruction — see below for the exact file list)
DATE: 2026-09-27
STATUS: FAIL

Session note: this pass resumed after a session restart. Six modified test files and two new test
files were already present, uncommitted, in the worktree at resume — found, reviewed, and folded
into this pass rather than discarded (`git diff`/`git status` at resume: `web/tests/components/
account/address-list.test.tsx`, `web/tests/components/auth/{register-form,reset-password-form,
session-guards}.test.tsx`, `web/tests/unit/lib/next-path.test.ts`, `web/tests/e2e/staff-auth.spec.ts`
modified; `web/tests/components/auth/registration-success.test.tsx`,
`web/tests/unit/lib/register-schema.test.ts` new). No servers were running at resume (coordinator:
ports 3102/5005 free); `next dev` on 3102 was the only server this pass started, and it was stopped
after every run (confirmed: no listener on 3102 or 5005, no stray `node.exe`, after the final run).

Full test-case sheet, written from §15/§16 first, then executed one row at a time:
`docs/test-cases/TC-HO-ACC-002.md`. 87 rows: 83 PASS, 2 FAIL (the same underlying failure, in the
E2E table and its Regression row), 2 BLOCKED (collapsed, blanket rows — every real-API variant; and
E-G1's reset-link half), 0 NOT RUN. 7 further §16 cases have no test written at all (E-B2, E-B4,
E-B5, E-B6, E-B7, E-B8, E-J1) — recorded as gaps, not silently dropped; none of them leaves an
acceptance criterion with **zero** coverage (each has at least a passing unit case), so per the
owner's rule these are MEDIUM/LOW, not the reason this gate fails.

THIS GATE FAILS ON ONE HIGH FINDING, NOT ON VOLUME

**F1 (HIGH) — a real, reproducible production race can drop the idle-lock and sign a staff member
out (losing their draft) instead of locking in place.** Found while executing E-G2
(`staff-auth.spec.ts:211`), a named, required case for A17/A18 and the ARCHITECTURE gate's own
fourth-pass HIGH. Full mechanism, reproduction evidence and frequency are in the TC sheet's
"Findings" section (F1) and its regression row (R3); summary: a real window-focus event refetches
both the session-status query and a page's own data query at once; if the session-status query's
`session_idle_timeout` response is processed by `IdleLock.tsx`'s effect *after* the page query's own
`session_invalid` response reaches the shared `setSessionErrorHandler` callback (a real, observed
network-response-ordering race, not a test artifact), the "not already locked" branch runs first:
`queryClient.clear()` and a hard redirect to `/staff/sign-in`, discarding the draft entirely. Watched
directly: one failing run's DOM snapshot shows the page already on "Staff sign in" where the dialog
was expected. Reproduced 1 failure in 3 isolated (`--workers=1`, single spec, single test)
`--repeat-each` runs on `mobile-360`, and 0 failures in a separate 10-repeat run across both
projects — a narrow, genuine race, not the load-dependent flakiness this pass separately confirmed
for nine other files (R8/R9, all environmental, all pass at `--workers=1`). This is squarely "a
failing test of a required behaviour" under the owner's rule, not a backlog item. **Not fixed here**
— this is the TEST gate; reported for ARCHITECTURE/the owner. Every other required behaviour this
mechanism touches (E-G3, the third-pass staff-account-detail case, U-G4/U-G5, the whole
`idle-lock.test.tsx`/`idle-lock-context.test.tsx` suites) passed in every run this pass, including
isolated repeats — this is not a wider regression of the lock mechanism, only this one concurrent-
error-ordering path.

Two further items, both closed (not reasons for the STATUS above, recorded for completeness):

**F2 (test-authoring bug, found and fixed this pass) — the pre-existing "locking while genuinely
offline" E2E test (`staff-auth.spec.ts`, added uncommitted before this pass resumed, per the
ARCHITECTURE gate fifth pass's own explicit ask: "TEST should also run the real-browser variant...
`context.setOffline(true)` before the lock, `false` after") could never have passed, for a reason
unrelated to the `networkMode: 'always'` fix it was meant to prove.** It drove the lock via
`__forceStaffIdleExpired` + `__invalidateQueries(['session','staff'])` — the same mechanism every
*other* lock test in the file uses — but that refetches `useStaffSession()` through `useQuery`'s own
observer, which defaults to `networkMode: 'online'`. The real `offline` event fired by
`context.setOffline(true)` (deliberately set *before* this step, matching the scenario) flips
`onlineManager` to `false` first, so TanStack Query correctly, matching production, never even
attempts that refetch — the lock could never engage, regardless of whether the fix under test was
present. Confirmed empirically: ran unmodified, failed identically (no dialog, draft correct, page
otherwise interactive) on both projects, at `--workers=1` and `--workers=2`. Fixed by driving the
lock the way `idle-lock.test.tsx`'s own jsdom counterpart does — a raw `apiFetch()` call, bypassing
`useQuery`'s `networkMode` gate entirely, matching the exact "any staff-audience call" mechanism
`client.ts`'s session-error handler is documented to react to. This needed one small, precedented,
test-only addition to production code: `window.__apiFetch` in `web/app/providers.tsx`, gated
`NODE_ENV !== 'production'` exactly like the existing `__apiBaseUrl`/`__invalidateQueries` hooks
immediately above it (not a new pattern). Verified passing on both projects at `--workers=1`, and
inside two full-suite `--workers=2` runs.

**Confirmed, not found: CODE_REVIEW gate's "Playwright flakiness at default worker count" note.**
That note judged the flakiness "most likely environmental" (dev-server route-compile contention
under `fullyParallel`) and asked TEST to confirm both that diagnosis and that
`real-api-verification.spec.ts` is structurally excluded from the default run. Both confirmed: (1)
`npx playwright test --project=chromium --project=mobile-360 --list` lists zero of that file's
tests; (2) two full `--workers=2` runs produced 14 failing test identities between them, no two runs
failing the same set, and every one of them passed cleanly at `--workers=1` — except E-G2 (F1,
above), which is not part of this pattern (it fails even alone).

Unit/component suite: `npx tsc --noEmit` (0 errors), `npm run lint` (`eslint . --max-warnings 0`, 0
warnings after fixing one this pass introduced), `npx prettier --check .` (clean), `npx vitest run`
— **60 files / 236 tests, all passed**, run twice (once at resume, once after this pass's own
additions: U-C4 via a new `sign-out-button.test.tsx` — `SignOutButton` had no test file at all
before — and U-G2 via a new case in `idle-lock.test.tsx` — the idle-warning banner had no test at
all before, at any point in this handover's history).

FINDINGS

- [HIGH] `web/components/staff/IdleLock.tsx` (the `setSessionErrorHandler` callback vs. the
  `sessionError`-driven effect): unsynchronized handling of two concurrent 401s from the same
  idle-timeout event can clear the cache and redirect instead of locking. See F1 above and the TC
  sheet's Findings/R3 for full evidence. **ARCHITECTURE/owner.**
- No other HIGH or BLOCKER findings. See the TC sheet's "Gaps left deliberately" for the 7 MEDIUM/LOW
  items (missing §16 E2E cases, all with partial coverage already) and its "Existing tests judged
  weak" section (only F2, fixed in place).

SCOPE CHECK

Files touched this pass, all within `web/tests/` and `docs/{test-cases,gates}/`, plus one
precedented test-hook addition to `web/app/providers.tsx`:
- New: `docs/test-cases/TC-HO-ACC-002.md`, this file, `web/tests/components/auth/
  sign-out-button.test.tsx`.
- Modified (this pass, on top of the pre-existing uncommitted diff described above):
  `web/app/providers.tsx` (`__apiFetch` hook, ~15 lines, gated identically to the two existing hooks
  beside it), `web/tests/e2e/staff-auth.spec.ts` (F2's fix), `web/tests/components/staff/
  idle-lock.test.tsx` (+U-G2).
- Nothing outside `web/app/providers.tsx`, `web/tests/**` and `docs/{test-cases,gates}/**` was
  touched.
- **Nothing in this pass was committed**, per instruction. `git status --short` at the end of this
  pass shows exactly: the pre-existing 6 modified + 2 new files from resume, plus
  `web/app/providers.tsx`, `web/tests/e2e/staff-auth.spec.ts`, `web/tests/components/staff/
  idle-lock.test.tsx` modified further, and `web/tests/components/auth/sign-out-button.test.tsx` +
  `docs/test-cases/TC-HO-ACC-002.md` + this file new.
