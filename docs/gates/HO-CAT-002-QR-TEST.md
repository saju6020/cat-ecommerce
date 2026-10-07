GATE: TEST
HANDOVER: HO-CAT-002 (follow-up: the `/cats/{id}` smart-redirect page, owner rule change of
2026-09-27, branch `ho/CAT-002-qr`)
COMMITS: e8a6d25, 01309bc, 9f94bee, b543474 (feature diff, `git diff 08e873e HEAD` at the point this
gate started), plus this gate's own `806f760` (two new unit tests) and `1a8c4fd` (case sheet)
DATE: 2026-09-28
STATUS: PASS

Case sheet: `docs/test-cases/TC-HO-CAT-002-QR.md`. 38 total: **36 PASS, 0 FAIL, 2 BLOCKED, 0 NOT RUN.**
Written before any check was run, executed one row at a time, updated as each result was observed —
no row was filled in from memory or inferred from a passing neighbour.

## Build gate

Confirmed green before testing (this is a review gate, not the place a red build gets discovered).
`web/`, all clean against the branch head:
- `npx tsc --noEmit` — 0 errors.
- `npx eslint . --max-warnings 0` — 0 errors, 0 warnings.
- `npx prettier --check .` — only flags `next-env.d.ts`, untracked and gitignored (confirmed absent
  from `git ls-files`), not part of this diff. Same finding CODE_REVIEW recorded; not new.
- `npx vitest run`, **twice** as instructed — 259/259 both times, both runs identical.

## Tests added by this gate

Two unit cases the spec names but no existing case exercised, added to
`web/tests/unit/cat/cat-smart-redirect-page.test.tsx` (commit `806f760`):

- **U19** — staff idle-timed-out *and* a customer session both present. The owner's rule ("staff win
  when a browser holds both sessions") is tested for a plain staff session (`staff wins` case) and
  for idle-timeout alone (`staff idle-timeout` case), but never for the two combined — the literal
  "shared clinic device, staff has gone idle" scenario the rule is written for.
- **U20** — the ownership check returning `403` (`ApiForbiddenError`). The handover's own doc comment
  names three equivalent outcomes — "a `404`, a `403`, or any other response that can't tell
  doesn't-exist apart from not-yours" — but only `404` and `500` had cases.

Both were **proven non-decorative**, not just written and trusted: each was watched to fail against a
targeted, realistic mutation of the exact production branch it covers (dropping the idle-timeout
disjunct from `isStaffSignedIn`; treating a `403` as ownership), then watched to pass again after the
mutation was reverted (`git status --short` clean before and after both checks — nothing was left
uncommitted from the mutation testing itself). Full detail and the mutated diffs are in the case
sheet, U19/U20 rows.

The task's own named example — "a staff member scanning while signed out completes the full round
trip through staff sign-in back to `/staff/cats/{id}`" — was checked against the existing suite
first, per this gate's own discipline of enumerating before running: it was **already covered**
(`tests/e2e/cat-smart-redirect.spec.ts`, "anyone not signed in lands on staff sign-in, and signing in
as staff returns them to the cat they scanned"), so nothing was added for it.

## Runs

- `cat-smart-redirect.spec.ts --repeat-each=5`, `--project=chromium --project=mobile-360
  --workers=1`, `PORT=3108`, MSW mocks — **70/70 passed (3.4m)**.
- `cat-a11y.spec.ts` and `cat-responsive.spec.ts`, once each, both projects, `--workers=1` —
  **42/42** and **16/16 passed**. First attempted together in one invocation; that run crashed
  partway through mid-`cat-responsive.spec.ts` on the `mobile-360` project (Node
  `FATAL ERROR: ... process out of memory`, `worker process exited unexpectedly, code=134`). Re-run
  as two separate invocations — clean, no crash, every case watched to pass. Host had ~8.9 GB
  available physical memory out of 32 GB at the time, and three other gate passes (the delta
  ARCHITECTURE/SECURITY/CODE_REVIEW reviews named in the task brief) were running concurrently on the
  same machine; read as host memory contention, not a defect in either spec or the page under test.
  Recorded in the case sheet's Environment section, not hidden.
- Full e2e suite, no file filter, both projects, `--workers=1`, `PORT=3108` — **390/390 passed, 0
  failed, 15.1 minutes**, one clean run. No regression anywhere in the suite from this diff (the new
  page, `useCat`'s additive `enabled` option, or the new mock hook).

### Interruption during this gate

This gate's e2e work was interrupted partway through by a rate limit. On resuming, a stray,
improperly-backgrounded shell command from before the interruption had left **two concurrent
`playwright test` invocations** running against one shared dev server on port 3108 (confirmed via
`Get-CimInstance Win32_Process` command-line inspection) — both processes were killed, the port was
confirmed free, and the full-suite run above was then executed once, cleanly, as a single properly
tracked background process. Port 3108 and all `node`/`next`/`playwright` processes were confirmed
absent again at the very end of this gate; nothing was left running.

## Existing tests judged

`cat-smart-redirect-page.test.tsx`'s pre-existing 10 cases and `cat-id.test.ts`'s 8 cases were read
against "would this still pass if the production code were broken": each asserts an exact
`router.replace` argument (a real final URL string) via `waitFor`, or its explicit absence, never a
loose "was called" check; the e2e cases in `cat-smart-redirect.spec.ts` chain a final-URL assertion
with a `getByRole('heading', ...)` check for the destination screen, not the URL alone. No decoration
found in the pre-existing suite. The two cases this gate added (U19, U20) were independently confirmed
non-decorative by mutation, above — not merely by resembling the existing style.

## Findings

None raised by this gate directly. The delta ARCHITECTURE/SECURITY/CODE_REVIEW passes named in the
task brief finished and were committed (`e66fb3d`, `a209b50`, `8267265`) during this gate's own run,
all `STATUS: PASS`; every finding across all three is LOW, so none blocks under the owner's
gate-blocking rule. Two of those LOWs are carried into this gate's case sheet as `BLOCKED` regression
rows (R3, R4) rather than silently dropped:

- **R3** (SECURITY 2nd pass / CODE_REVIEW 2nd pass, LOW): the ownership check's `isOwner =
  Boolean(ownershipQuery.data)` inherits the global 30s `staleTime`, so a client-side navigation
  (same customer, same cat, inside the 30s window) could resolve ownership from an already-cached
  answer instead of a fresh fetch. Both source gates judge this unreachable from an actual QR scan
  (a full page load starts with an empty `QueryClient`) and, even in its narrow reachable case, a
  wrong-destination-only outcome — `/portal/cats/{id}` and the API still enforce ownership
  independently. Not reproduced by this gate: doing so deterministically would need a fake system
  clock or a `QueryClient` pre-seeded with cross-identity cached data, disproportionate for a LOW
  both source gates already judged non-blocking. Left as a gap, with the fix direction both gates
  already gave, in the case sheet.
- **R4** (ARCHITECTURE 2nd pass, LOW): the mock test-hook's own comment describes itself as
  "one-shot"/"the NEXT document" when `page.addInitScript` actually re-arms on every subsequent
  navigation in that browser context. A wording defect on test-only mock code; no current spec
  reaches a second navigation after using the hook, so there is nothing to watch fail or pass.

**Process observation, not a finding against this diff**: `docs/backlog/CAT.md`, the file all three
delta gates say their LOW findings "go to", does not exist yet in this worktree. Reported here as
observed; not created by this gate, which does not own that filing.

## Scope check

Files touched by this gate:
- `web/tests/unit/cat/cat-smart-redirect-page.test.tsx` — two new test cases (U19, U20), committed
  `806f760`. Within `web/**`, HO-CAT-002 §13.
- `docs/test-cases/TC-HO-CAT-002-QR.md` — new, committed `1a8c4fd`. Within HO-CAT-002 §13's
  `docs/test-cases/TC-HO-CAT-002.md` allowance in spirit (a dedicated sheet for this follow-up, named
  per the handover-ID convention this gate's own instructions specify).
- `docs/gates/HO-CAT-002-QR-TEST.md` — this file, left uncommitted per instructions.
- `docs/gates/HO-CAT-002-QR-{ARCHITECTURE,SECURITY,CODE_REVIEW}.md` — read for context only, per the
  task's explicit instruction not to touch them. Not edited.
- No file outside `web/**`/`docs/test-cases/`/`docs/gates/` was touched. No `src/**`, `.ai/**`,
  `.claude/**`, or .NET build file in scope.

## Housekeeping

No server left running. Port 3108 confirmed free and no stray `node`/`next`/`playwright` process
confirmed via `netstat` and `Get-CimInstance Win32_Process`, both immediately after this gate's own
interruption-recovery cleanup and again at the end of this gate.

## Verdict

**PASS.** 36/38 case-sheet rows PASS, 2 BLOCKED (both carried LOW findings from the delta gates,
judged unreachable-in-practice by their own source gates, with a clear non-blocking rationale each),
0 FAIL, 0 NOT RUN. Build gate clean. Two genuine coverage gaps the spec named were found and closed
with tests proven non-decorative by mutation. The task's own named example case was checked and found
already covered, not assumed. Full e2e suite (390 tests) shows no regression from this diff.
