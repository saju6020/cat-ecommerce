# TC-HO-CAT-002-QR — `/cats/{id}` smart-redirect page

Handover: `docs/handover/HO-CAT-002.md` §1 Amended, both 2026-09-27 rows on `/cats/{id}` — "The
`/cats/{id}` smart-redirect page is now built" and the owner's later same-day revision ("owner
decision, changing two of `/cats/{id}`'s rules…").
Commit(s) under test: `08e873e` (base, `master`) .. `b543474` (branch `ho/CAT-002-qr`, HEAD) —
`e8a6d25`, `01309bc`, `9f94bee`, `b543474`.
Written: 2026-09-28 · Executed: 2026-09-28

> Statuses record what was **observed**, not inferred. The Evidence column names what was actually
> seen. `BLOCKED` marks a case that could not be executed or whose result was reasoned about rather
> than watched — it is not a failure, and it is not hidden.

The governing rule set, taken from the task brief (the owner's final 2026-09-27 rule change):

| # | Rule |
|---|---|
| R1 | Staff → `/staff/cats/{id}` |
| R2 | Staff wins when a browser holds both a staff and a customer session |
| R3 | The owner (a signed-in customer who owns the cat) → `/portal/cats/{id}` |
| R4 | A signed-in customer who does **not** own the cat → `/staff/sign-in?next=/staff/cats/{id}` |
| R5 | An anonymous visitor → the same staff sign-in URL as R4 |
| R6 | A `5xx` from the ownership check → the same staff sign-in URL as R4/R5 (fail closed) |
| R7 | An invalid id → the inline error state, no redirect |
| R8 | Anonymous visitors and staff trigger **no** cat call (`GET /api/cats/{id}`) |
| R9 | A session-check failure (network/`5xx`) is treated as "not signed in" |
| R10 | A staff idle-timeout still counts as "signed in staff" |
| R11 | Every redirect uses `router.replace`, never `push` |
| R12 | The `next` value is built and re-validated with `sanitizeNextPath()`, not a hand-rolled check |

## Summary

| | Count |
|---|---|
| Total | 38 |
| PASS | 36 |
| FAIL | 0 |
| BLOCKED | 2 |
| NOT RUN | 0 |

## Environment

- **Node**: v24.21.0, npm 11.19.0, `web/node_modules` already installed (confirmed present before
  any command below).
- **Build**: no `.NET` backend involved in this diff (pure `web/**` change). No backend/Postgres
  process was started or needed — every check below runs against MSW mocks only, per the task's
  own steps. The real-API cross-check HO-CAT-002 §5.1 asks every §16 case to eventually get is
  **out of scope for this pass** (not one of the task's steps) — recorded under "Gaps left
  deliberately", not silently skipped.
- **Unit suite**: Vitest, run from `web/` (`npm test` / `npx vitest run`), twice as instructed.
- **E2E suite**: Playwright, `web/`, `PORT=3108`, `--workers=1`, projects `chromium` and
  `mobile-360`, MSW-mocked backend (the dev server's own `NEXT_PUBLIC_USE_MOCKS=true`). Dev server
  was started once per run set and stopped afterwards — none left running (confirmed at the end).
- **Observed once, not reproduced as a code defect**: `cat-a11y.spec.ts` and `cat-responsive.spec.ts`
  run together in one `npx playwright test` invocation crashed partway through
  (`FATAL ERROR: Zone Allocation failed - process out of memory` / `worker process exited
  unexpectedly (code=134)`, `mobile-360` project, mid-`cat-responsive.spec.ts`). Re-run as two
  separate invocations (still `--workers=1`, `PORT=3108`, both projects each) — 42/42 and 16/16,
  clean, no crash. `Available Physical Memory` on this host was ~8.9 GB out of 32 GB total at the
  time (checked via `wmic OS get FreePhysicalMemory`), and the task brief names three other gate
  passes running concurrently on this same machine — read as host memory contention from concurrent
  work, not a defect in either spec or the page under test. Recorded as a note, not a FAIL, since
  every case in both files was still watched to pass once isolated.
- Delta ARCHITECTURE/SECURITY/CODE_REVIEW passes ran concurrently on
  `docs/gates/HO-CAT-002-QR-{ARCHITECTURE,SECURITY,CODE_REVIEW}.md` (their own second-pass sections,
  commits `e66fb3d`, `a209b50`, `8267265`) and finished, all `STATUS: PASS`, before this sheet's
  final update. Those three files were read for context only and were not edited by this gate.
- This gate's own e2e execution was interrupted partway through by a rate limit (see the task's own
  mid-run resume message). Before resuming, an orphaned dev server / duplicate `playwright test`
  invocation left over from the interruption was found (two concurrent `playwright test` processes
  sharing one dev server on port 3108, from a stray, improperly-backgrounded shell command) and
  killed; the full e2e suite below was then re-run cleanly, once, as a single tracked process. Port
  3108 and all `node`/`next`/`playwright` processes were confirmed absent both immediately after that
  cleanup and again at the very end of this gate (`netstat`, `Get-CimInstance Win32_Process`).

## Unit cases

Source: `web/tests/unit/cat/cat-smart-redirect-page.test.tsx` and `web/tests/unit/lib/cat-id.test.ts`
(pre-existing), plus two new cases added by this gate (U19, U20) closing gaps the spec names.

| ID | Case | Rule(s) | Expected | Status | Evidence |
|---|---|---|---|---|---|
| U1 | Invalid id | R7, R8 | Shows "link isn't valid", never calls `router.replace`; both session calls still fire (hooks are unconditional) but the ownership call never fires | PASS | `npx vitest run` — watched pass, both full runs |
| U2 | Loading (both sessions pending) | — | Accessible `role="status"` loading text, no redirect yet | PASS | `npx vitest run` — watched pass, both full runs |
| U3 | Staff signed in | R1, R8 | `router.replace('/staff/cats/{id}')`; `getCat` never called | PASS | `npx vitest run` — watched pass, both full runs |
| U4 | Staff idle-timeout | R1, R10 | Still treated as signed-in staff; same redirect as U3 | PASS | `npx vitest run` — watched pass, both full runs |
| U5 | Owner (customer, ownership check succeeds) | R3 | `router.replace('/portal/cats/{id}')` | PASS | `npx vitest run` — watched pass, both full runs |
| U6 | Signed-in non-owner customer (`404` from ownership check) | R4 | `router.replace(staffSignInWithNext)`, never `/portal/cats/{id}` | PASS | `npx vitest run` — watched pass, both full runs |
| U7 | Ownership check `5xx` | R6 | Fails closed to the same staff sign-in URL as U6 | PASS | `npx vitest run` — watched pass, both full runs |
| U8 | Both sessions present (staff + customer) | R2, R8 | Staff wins: `/staff/cats/{id}`, never `/portal/cats/{id}`; no ownership call | PASS | `npx vitest run` — watched pass, both full runs |
| U9 | Anonymous (no session at all) | R5, R8 | `router.replace(staffSignInWithNext)`; no ownership call | PASS | `npx vitest run` — watched pass, both full runs |
| U10 | Session-check `5xx` on both endpoints | R9 | Fails closed to the same staff sign-in URL as U9, never stuck loading | PASS | `npx vitest run` — watched pass, both full runs |
| U11 | `isValidCatId`, well-formed GUID | R7 | `true` | PASS | `npx vitest run` — watched pass, both full runs |
| U12 | `isValidCatId`, GUID, mixed case | R7 | `true` | PASS | `npx vitest run` — watched pass, both full runs |
| U13 | `isValidCatId`, empty string | R7 | `false` | PASS | `npx vitest run` — watched pass, both full runs |
| U14 | `isValidCatId`, non-GUID word | R7 | `false` | PASS | `npx vitest run` — watched pass, both full runs |
| U15 | `isValidCatId`, missing segment | R7 | `false` | PASS | `npx vitest run` — watched pass, both full runs |
| U16 | `isValidCatId`, extra character | R7 | `false` | PASS | `npx vitest run` — watched pass, both full runs |
| U17 | `isValidCatId`, path-traversal payload | R7 | `false` | PASS | `npx vitest run` — watched pass, both full runs |
| U18 | `isValidCatId`, surrounding whitespace | R7 | `false` | PASS | `npx vitest run` — watched pass, both full runs |
| U19 | **New, added this gate.** Staff idle-timeout *and* a customer session both present | R2, R8, R10 | Staff still wins (idle-timeout counts as signed-in staff per R10) over a co-present customer session — the literal "shared clinic device, staff has gone idle" case R2 is meant to cover; no ownership call | PASS | Watched pass. **Proven non-decorative**: temporarily changed `isStaffSignedIn` (`page.tsx`) to drop the idle-timeout branch (`Boolean(staffSession.data)` only) — this test (and the pre-existing plain idle-timeout test) failed, `routerReplace` never called with `/staff/cats/{id}`. Reverted (`git status --short` clean after), re-ran — 20/20 pass again |
| U20 | **New, added this gate.** Ownership check returns `403` (`ApiForbiddenError`) | R4 | Treated as non-owner, same as `404` — the spec text names `403` explicitly as one of "a `404`, a `403`, or any other response that can't tell doesn't-exist apart from not-yours" | PASS | Watched pass. **Proven non-decorative**: temporarily changed `isOwner` (`page.tsx`) to also treat a `403` response as ownership (a realistic "assumed benign" bug) — this test failed (timed out waiting for the staff sign-in redirect), all 11 other cases in the file still passed. Reverted (`git status --short` clean after), re-ran — 20/20 pass again |

## Integration (e2e) cases

Source: `web/tests/e2e/cat-smart-redirect.spec.ts` (pre-existing, 7 cases), plus the dedicated
`/cats/{id}` cases added to `cat-a11y.spec.ts` (4 cases) and `cat-responsive.spec.ts` (2 cases).
Each runs on both Playwright projects (`chromium`, `mobile-360`) unless noted.

| ID | Case | Rule(s) | Expected | Status | Evidence |
|---|---|---|---|---|---|
| I1 | Staff → staff cat profile | R1 | Final URL `/staff/cats/{id}`, a real `<h1>` renders | PASS | `cat-smart-redirect.spec.ts --repeat-each=5`, both projects — 70/70 passed (3.4m), `--workers=1`, `PORT=3108` |
| I2 | Signed-in owner → portal cat profile | R3 | Final URL `/portal/cats/{id}`, a real `<h1>` renders | PASS | Same run — 70/70 passed |
| I3 | Signed-in non-owner → staff sign-in | R4 | Final URL `/staff/sign-in?next=...`, "Staff sign in" heading renders | PASS | Same run — 70/70 passed |
| I4 | Ownership check `5xx` (`catServerError` fixture) → staff sign-in | R6 | Same destination and heading as I3 | PASS | Same run — 70/70 passed |
| I5 | Anonymous → staff sign-in, then a full staff sign-in round trip returns to `/staff/cats/{id}` | R5, R1 (round trip) | Lands on staff sign-in first; after submitting real staff credentials, final URL is `/staff/cats/{id}` with its own `<h1>` — this is the task's own named example ("a staff member scanning while signed out completes the full round trip through staff sign-in back to `/staff/cats/{id}`") | PASS | Same run — 70/70 passed. This case was **already present** in the pre-existing spec (`anyone not signed in lands on staff sign-in, and signing in as staff returns them to the cat they scanned`); the task's named example was not a gap |
| I6 | Invalid id (`/cats/not-a-real-id`), even while signed in as staff | R7 | URL stays on `/cats/not-a-real-id`, inline "link isn't valid" message, no redirect | PASS | Same run — 70/70 passed |
| I7 | Both sessions present → staff wins | R2 | Final URL `/staff/cats/{id}` | PASS | Same run — 70/70 passed |
| I8 | a11y: staff destination | R1 | No serious/critical axe violations after the final URL is reached | PASS | `cat-a11y.spec.ts`, both projects, `--workers=1` — 42/42 passed (2.5m), includes all pre-existing per-screen E17 cases plus the 4 `/cats/{id}` cases × 2 projects |
| I9 | a11y: owner destination | R3 | No serious/critical axe violations after the final URL is reached | PASS | Same run — 42/42 passed |
| I10 | a11y: non-owner destination (staff sign-in) | R4 | No serious/critical axe violations after the final URL is reached | PASS | Same run — 42/42 passed |
| I11 | a11y: anonymous destination (staff sign-in) | R5 | No serious/critical axe violations after the final URL is reached | PASS | Same run — 42/42 passed |
| I12 | Responsive: `/cats/{id}` in the `STAFF_SCREENS` 360px loop | R1 | Final URL `/staff/cats/{id}` reached before measuring; no horizontal overflow at 360px | PASS | `cat-responsive.spec.ts`, both projects, `--workers=1` — 16/16 passed (2.3m), all four viewports (360/768/1024/1440) |
| I13 | Responsive: `/cats/{id}` in the `PORTAL_SCREENS` 360px loop | R3 | Final URL `/portal/cats/{id}` reached before measuring; no horizontal overflow at 360px | PASS | Same run — 16/16 passed |
| I14 | Full e2e suite (regression check) | all | Every pre-existing e2e case across the whole `web/tests/e2e/` tree still passes with this diff applied — no regression introduced by `/cats/{id}` or its supporting changes (`useCat`'s new `enabled` param, the mock hook) | PASS | `npx playwright test --project=chromium --project=mobile-360 --workers=1` (no file filter), `PORT=3108` — **390/390 passed, 0 failed, 15.1 minutes**, single clean run after the interruption-recovery cleanup above |

## Regression cases from earlier gates

The delta ARCHITECTURE/SECURITY/CODE_REVIEW passes named in the task brief finished and were
committed (`e66fb3d`, `a209b50`, `8267265`) before this table's final update; the rows below cite
their final, second-pass content. All three are `STATUS: PASS`; every finding they raised is LOW, so
under the owner's gate-blocking rule none of them fail this gate. Only findings that changed shipped
behaviour, or that name a verifiable runtime claim, get a regression row; two CODE_REVIEW 1st-pass
LOWs explicitly marked "optional, nice-to-have, not required" (hook-call ordering; a no-op
`encodeURIComponent`) produced no code change and have nothing to regress against — not repeated here.

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| R1 | ARCHITECTURE 1st pass | LOW — the handover's prose claimed an invalid id "never reaches a session round trip"; in fact both session `GET`s still fire, only their results go unused. Corrected in the 2nd-pass handover row, confirmed fixed-in-record by ARCHITECTURE 2nd pass | U1 | PASS | `npx vitest run` — watched pass; the test explicitly asserts both `getStaffSessionStatusMock`/`getCustomerSessionMock` **were** called and `getCatMock` was **not**, for an invalid id |
| R2 | ARCHITECTURE 1st pass, LOW 3 → superseded by the owner's later rule | A signed-in non-owner customer used to redirect to `/portal/cats/{id}`; the owner's later rule instead sends them to staff sign-in, confirmed unchanged (still the shipped behaviour) by ARCHITECTURE/SECURITY/CODE_REVIEW 2nd pass | U6, I3 | PASS | `npx vitest run` + `cat-smart-redirect.spec.ts` — both watched pass, asserting `/staff/sign-in?next=...` and explicitly **not** `/portal/cats/{id}` |
| R3 | SECURITY 2nd pass / CODE_REVIEW 2nd pass | LOW — `isOwner = Boolean(ownershipQuery.data)` inherits the global 30s `staleTime`, so a cached success from an earlier check could resolve ownership without a fresh network round trip on a client-side navigation; both gates independently judge this unreachable from an actual QR scan (a full page load starts with an empty `QueryClient`) and non-blocking even in its narrow reachable window | — | BLOCKED | No case added — see "Gaps left deliberately" below. Reproducing it would need a fake system clock or a `QueryClient` pre-seeded with cross-identity cached data; disproportionate for a LOW that both source gates judge as changing only the destination screen, never an authorization outcome |
| R4 | ARCHITECTURE 2nd pass | LOW — the mock hook's own comment says "one-shot" / "the NEXT document", but `page.addInitScript` actually re-arms on **every** subsequent navigation in that browser context, not only the first | — | BLOCKED | Wording-only finding on a code comment; ARCHITECTURE's own text confirms "the current tests are not affected" (every existing case's flow ends at `/staff/sign-in`, which triggers no further cat call) — no case in this gate's scope exercises a second navigation after the forced flag, so there is nothing here to watch fail or pass |

## Acceptance-criteria coverage

| Criterion | Covered by | Status |
|---|---|---|
| Staff → `/staff/cats/{id}` | U3, U4, I1 | PASS |
| Staff wins when a browser holds both sessions | U8, U19, I7 | PASS |
| Owner → `/portal/cats/{id}` | U5, I2 | PASS |
| Signed-in non-owner customer → staff sign-in | U6, U20, I3 | PASS |
| Anonymous visitor → staff sign-in (same URL) | U9, I5 | PASS |
| Ownership check `5xx` → staff sign-in | U7, I4 | PASS |
| Invalid id → error state | U1, U11–U18, I6 | PASS |
| Anonymous visitors and staff trigger no cat call | U1, U3, U4, U8, U9, I1, I5, I7 | PASS |

## Cases I could not run

| ID | Why | What would unblock it |
|---|---|---|
| REAL-API | HO-CAT-002 §5.1's own rule ("every §16 case is re-run against the real API once it lands") is not one of this task's steps — no `.NET` backend/Postgres process was started for this pass | A separate pass with the real API and `E2E_REAL_API=1 npm run test:e2e:real-api`, scoped explicitly to include `/cats/{id}` |
| PROD-BUILD | The task's steps list `tsc --noEmit`, `eslint`, `prettier --check`, `vitest`, and `playwright` against the dev server — a production `next build` was not one of the requested checks, so a build-only defect (e.g. an env-dependent code path) would not surface here | Run `npm run build` as part of a future pass if that becomes a stated step |

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| No test asserts `router.push` was **never** called (only that `router.replace` was called with the right value) | `page.tsx` never imports/calls `push` at all (confirmed by reading the file) — ARCHITECTURE and CODE_REVIEW both verified this by direct code reading; adding a `expect(routerPush).not.toHaveBeenCalled()` assertion to every existing case would be pure decoration against code that structurally cannot call `push` | Whoever next touches this file's history mechanism — worth adding only if the file is refactored to hold a reference to the router object generally |
| No case for a staff session that is a network error (`status === 0`) specifically, distinct from a real `5xx` | `useSession.ts`'s own `retryOnlyNetworkError` already retries a network error twice before settling as an error either way — session resolution's ultimate outcome (fail closed) is identical to the `5xx` case (U10/I4), so a dedicated network-error variant would exercise the same downstream branch this page already covers, only through `useSession.ts`'s own retry path (already someone else's tested surface) | Low priority; HO-ACC-002's own `useSession.ts` test suite, not this page's |
| No case reproduces the ownership check's `staleTime`-inherited cache reuse (SECURITY/CODE_REVIEW 2nd-pass LOW, regression row R3) — a client-side navigation, same customer, same `catId`, inside the shared query's 30s window, could resolve `isOwner` from a cached earlier answer rather than a fresh fetch | Both source gates independently judge this unreachable from an actual QR scan (a full page load always starts with an empty `QueryClient`) and, even in its narrow reachable window, a change of destination screen only — never an authorization bypass, since `/portal/cats/{id}` and the API still enforce ownership independently. Reproducing it deterministically would need either a fake system clock advanced within the 30s window or a `QueryClient` pre-seeded with another identity's cached `['cat', catId]` data before mounting the page — a disproportionate amount of test scaffolding for a LOW that does not block and that neither source gate asked TEST to specifically reproduce | Whoever picks up the LOW from `docs/backlog/CAT.md` (note: that file does not exist yet in this worktree as of this gate — see this gate's report) — fix direction already given by both gates: `ownershipQuery.isSuccess && ownershipQuery.isFetchedAfterMount && !ownershipQuery.isFetching`, or a dedicated `staleTime: 0` on the ownership check |
| No case reproduces the mock hook's "re-arms on every subsequent navigation" behaviour (ARCHITECTURE 2nd-pass LOW, regression row R4) | Purely a comment-wording inaccuracy on test-only mock code, not a behaviour any current spec depends on; every existing case's flow terminates at `/staff/sign-in`, which never makes a further cat call, so there is no reachable second-navigation-in-the-same-test scenario to exercise today | Whoever next adds an e2e case that navigates to a second cat page after using this hook — fix the comment, or clear the flag explicitly after use, at that point |

