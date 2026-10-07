GATE: TEST
HANDOVER: HO-CAT-002
COMMITS: 2fcd392 0f6bc0e b1f380c c6cb154 a408ab1 40ea4a8 70ae99a 94a626b 824b1fb bee5be8 542477e c3a076a 1a4cac4 fdef977 ad776b0 09a01fd 820f5dc 0c5a59b 454f6f9 bb9fef1 47d8f10 06121d9 4187ef8 241d310 e4f368c a76aca5 07e0733 6e71fae ee59d2a 24828c5 dbba453 b19928e f478f41 7304f85 7f758b3 8379121 bb24673 92a33e5 1bf95d8 b209ef7 c813696 b83bd2a f392132 8b38cc3 ccf29b1 631904f
DATE: 2026-09-25
STATUS: PASS

**Eighth pass, a targeted delta over `git diff c813696..631904f` (the 7th pass's own head to the
current branch head), per this pass's own brief**: build gates, one full `npm test`, one full serial
`npx playwright test --workers=1` run, and decoration checks limited to the three genuinely new
tests this delta introduces — not a full re-audit of everything the 7th pass already closed (that
pass's own complete accessibility audit, its 17/17 + 7/7 center-dialog regression check, and its
clean prod build are not repeated here, since this delta touches none of the mechanisms they
covered). Five owner-decided commits: `b83bd2a` (A11's next-due display, unblocking ACCEPTANCE's
BLOCKER 1 via an amendment to HO-CAT-001 C10), `f392132` (`StaffShell`'s nav sheet now closes on any
route change, fixing CODE_REVIEW's 2nd-pass MEDIUM on browser Back/Forward, plus a unified
`NavSections` renderer), `8b38cc3` (`FormFieldRow` extracted from `MedicalProfileForm`, ARCHITECTURE
MEDIUM), `ccf29b1` (dead `CatWeightPoint.id` branch dropped, O(1) row-id lookup, ARCHITECTURE LOW +
CODE_REVIEW perf note), `631904f` (`useAllVaccines` capped at 100 pages, ARCHITECTURE LOW). Full
test-case sheet, re-executed one row at a time: `docs/test-cases/TC-HO-CAT-002.md`. 86 rows: 82
PASS, 0 FAIL, 4 BLOCKED, 0 NOT RUN.

**Build gates, re-run clean.** `npm run typecheck` (0 errors), `npm run lint` (0 warnings),
`npm run format:check` (Prettier clean). `npm test` — **102/102 passed** (up from the 7th pass's
101 — the one new test is `tests/unit/cat/parasite-control-page.test.tsx`, `U34` in the TC sheet).
Directly re-ran the specific files this delta's non-test commits touch, rather than trusting the
aggregate count alone: `medical-profile-form.test.tsx` + `high-risk-flag-dialog.test.tsx`
(`8b38cc3`) — 4/4; `weights-page.test.tsx` (`ccf29b1`) — 1/1; `staff-shell.test.tsx` (`f392132`) —
8/8, the same 8 tests as the 7th pass (the delta only added a `vi.mock('next/navigation')` for the
new `usePathname()` import, no new test body). `npx playwright test --workers=1` (one full serial
run, per this pass's reduced brief) — **110 total, 110 passed, 0 failed**, 7.7 minutes (up from the
7th pass's 108 — the two new executions are `staff-shell-sheet-closes-on-back-navigation.spec.ts` ×
2 projects). **360px (E18/E19) passed clean on both projects inside this one run** — per this pass's
own brief, since the full run surfaced no 360px problem, the triple isolated E18 re-runs and the
clean `rm -rf .next` prod build (both used at the 4th/7th passes to settle an actual anomaly) are
skipped this pass: there is nothing here for them to settle.

**`U34` and the new A11 e2e assertion are both proven non-decorative, watched failing on the real
mechanism before being relied on.** `U34`: checked out `app/(staff)/staff/cats/[catId]/
parasite-control/page.tsx` to its exact pre-change (`c813696`) content and re-ran
`parasite-control-page.test.tsx` — **1/1 failed** (`screen.findByRole('status')` timed out —
`element(s) not found` — the pre-change page renders only a toast and a static PDF link, no status
region at all); restored and re-ran — **1/1 passed**. The new e2e assertion in
`cat-parasite-control.spec.ts` (`getByRole('status').getByText('Next due 19 Dec 2026')`): checked
out both `parasite-control/page.tsx` and `mocks/handlers/areas/cat-profiles.ts` (the mock's own
`nextDueOn` computation, standing in for HO-CAT-001 §8.2's aggregate rule, is part of the same
amendment) to their exact pre-change (`c813696`) content and re-ran — **1/1 failed**, identical
`getByRole('status')` element-not-found evidence; restored both files and re-ran — **1/1 passed**.
Working tree was clean before and after both checks (`git status --short`); nothing was committed.

**The back-navigation e2e is a genuine production fix with a decorative committed test — found,
investigated, and reported, not silently accepted.** Checking out `staff-shell.tsx` to its exact
pre-fix (`c813696`) content and re-running `staff-shell-sheet-closes-on-back-navigation.spec.ts`
**passed 1/1**, not the required fail — the same outcome this pass's brief exists to catch, not to
wave through. Investigated with two temporary, uncommitted diagnostic specs (removed before this
commit; nothing from either committed):
- The first traced `page.on('request')` for `resourceType() === 'document'` during `goBack()`,
  reproducing the committed spec's own exact setup (`page.goto('/staff/cats')`, then
  `page.goto('/staff/vaccines')` — both hard, full-page loads in Playwright). It found a genuine
  document request fired during `goBack()`: a **hard reload**, not the soft, client-side route
  change `f392132`'s fix and CODE_REVIEW's 2nd-pass MEDIUM are actually about. A hard reload
  remounts the whole app fresh, trivially resetting `sheetOpen` to its initial `false` regardless of
  whether the fix exists at all — exactly why the committed spec passes identically either way.
- The second rebuilt the same scenario using a real in-sheet `<Link>` click (`onNavigate`) to reach
  `/staff/vaccines` instead of a second `page.goto()`, then re-opened the sheet there before calling
  `goBack()`. Against the checked-out pre-fix `staff-shell.tsx`: **no** document request fired
  during `goBack()` (a genuine soft nav) and `getByRole('dialog')` count was **1** — the sheet
  stayed open, the real bug, directly reproduced, not inferred. Restoring the fix and repeating the
  identical steps: no document request, dialog count **0** — the sheet closes. **This independently
  confirms `f392132`'s production fix is genuinely correct on the real mechanism.**
- `staff-shell.tsx` was restored to `HEAD` (confirmed `git status --short` clean) before and after
  both diagnostics; both temporary spec files were deleted before this commit.

**Reported as a finding below, not fixed** — this gate verifies and reports; rewriting an
already-committed spec is not this pass's call to make unilaterally, and `StaffShell` is shared
infrastructure no single area's gate should patch unilaterally, consistent with every other finding
this shell has raised across this handover's TEST-gate history. This does not change the e2e row's
own status in the TC sheet (the spec was watched pass, both against the fix and, undesirably,
against its absence) but is disclosed in full, not reported as a clean, proven-non-decorative
regression case.

**Rows updated this pass, per the brief**: A11's acceptance-criteria and end-to-end rows both flip
to full PASS (the "shows its next-due date" evidence gap ACCEPTANCE's BLOCKER 1 named is closed);
§16 E6 gets a note recording the owner's 2026-09-25 acceptance of the TC sheet's own wording,
resolving ACCEPTANCE's "undeclared deviation" item; two new rows (`U34`; a new "not a §16 ID" row
for the back-navigation case); `MedicalProfileForm`'s regression row (R2) and the unit rows its own
`FormFieldRow` extraction touches (`medical-profile-form.test.tsx`, `high-risk-flag-dialog.test.tsx`)
are re-confirmed directly against the refactor, not assumed unchanged from the diff alone. No row
from the 1st–7th passes' own findings needed re-litigating — this delta doesn't touch any of the
mechanisms they were about.

FINDINGS

- [MEDIUM — TEST-suite quality, not a production defect] `tests/e2e/
  staff-shell-sheet-closes-on-back-navigation.spec.ts` provides no actual regression protection for
  the CODE_REVIEW gate's 2nd-pass MEDIUM (`StaffShell`'s nav sheet surviving the browser's own
  Back/Forward) it was written to guard.
  - The production fix (`f392132`) is real and confirmed correct on the real mechanism — see above.
    This finding is about the **test**, not the shipped behaviour.
  - The spec builds its "before" navigation history with two `page.goto()` calls. Confirmed via
    request tracing that `page.goBack()` after a `page.goto()`-built history performs a genuine hard
    document reload, not the soft, client-side transition the underlying bug requires — a hard
    reload trivially resets `sheetOpen` to `false` by remounting the app, independent of whether
    `f392132`'s effect exists.
  - Consequence: the spec currently passes identically whether `f392132`'s fix is present or
    reverted (both checked directly, see above) — it cannot fail if this exact code regresses.
  - Fix direction: replace the second `page.goto('/staff/vaccines')` with a real in-sheet `<Link>`
    click (open the sheet, click "Vaccine catalogue" inside it, exactly as a user closing the sheet
    via `onNavigate` would), so the resulting history entry is a genuine Next.js client-side
    transition and `goBack()` exercises the router's own popstate handling instead of the browser's
    native reload. The ad hoc diagnostic that found this (see above, not committed) demonstrates the
    corrected shape works and does distinguish fixed from unfixed code.
  - Not fixed by this gate — `StaffShell`'s tests are shared infrastructure, and rewriting an
    already-committed spec is not this pass's call to make unilaterally. Recorded for whoever next
    touches `tests/e2e/staff-shell-*`, or an ACCEPTANCE-gate decision on whether to require the fix
    before sign-off.
- [LOW, carried, resolved in code this pass] ARCHITECTURE gate LOW — `useAllVaccines` had no
  page-count ceiling.
  - `631904f` fixes this in production code: the loop is capped at 100 pages and throws a clear
    error past that, rather than looping forever on a broken `hasNextPage`.
  - No dedicated test exercises the ceiling itself (a 101st-page scenario, or the thrown error) —
    MSW's fixture catalogue is far short of 100 pages, and constructing one would be disproportionate
    for what remains a LOW-severity guard-rail. Recorded in "Gaps left deliberately", not escalated.
- [Resolved, no longer tracked as a finding] ARCHITECTURE gate LOW — the parasite-control page's doc
  comment cited the wrong aggregate (`CatWeightRecordAggregate`). `b83bd2a` rewrote the comment
  wholesale for the A11 amendment; the new text names only `CatAggregate.RecordParasiteTreatment`,
  confirmed by reading the file directly this pass.

EXISTING TESTS JUDGED WEAK (re-reviewed this pass; unchanged from the 1st–7th passes' own
disclosures unless noted)
- `tests/unit/cat/weight-trend-chart.test.tsx` (U22), `tests/unit/errors.test.ts`'s U25 case — as
  every prior pass recorded, unchanged; this delta doesn't touch either file.
- `tests/e2e/staff-shell-sheet-closes-on-back-navigation.spec.ts` (new this pass, landed with
  `f392132`) — **judged decorative for its stated purpose**, not merely weak: it does not fail
  against the pre-fix code, for the reason detailed in FINDINGS above. This is a stronger judgement
  than U22/U25's caveats (which verify *something* real, just not everything their case's literal
  wording claims) — this spec currently verifies nothing that distinguishes the fix from its absence.
  Reported in full rather than accepted on the strength of its own "1/1 passed" result.
- `tests/unit/cat/parasite-control-page.test.tsx` (`U34`, new this pass, landed with `b83bd2a`) —
  read for whether it would still pass if the production code were broken: it asserts a specific,
  computed date string (`'Next due 19 Dec 2026'`) via `toHaveTextContent` on a `role="status"`
  element found with `findByRole`, both a real DOM query and a real, non-trivial expected value (not
  a substring match loose enough to pass on an empty or placeholder render) — proven non-decorative
  directly this pass (fails against the pre-change page, passes against the fix). No decoration
  found.
- `tests/unit/cat/medical-profile-form.test.tsx`, `tests/unit/cat/high-risk-flag-dialog.test.tsx` —
  re-read this pass against `8b38cc3`'s `FormFieldRow` extraction: both still assert on real,
  observable outcomes (`aria-invalid`, `aria-describedby` pointing at a rendered `role="alert"`
  element, field-level error text) rather than on `FormFieldRow`'s own internal structure, so the
  extraction is transparent to them — re-run directly (4/4 passed) rather than assumed unaffected by
  the refactor's own diff.

SCOPE CHECK
- Files touched by this gate this pass, all within HO-CAT-002 §13's allowed scope (`web/**`,
  `docs/test-cases/TC-HO-CAT-002.md`, `docs/gates/HO-CAT-002-*.md`):
  - modified: `docs/test-cases/TC-HO-CAT-002.md`, this file.
  - temporary, uncommitted, and removed before this commit: `web/tests/e2e/_tmp_diagnose.spec.ts`
    (rewritten twice in place for the two back-navigation diagnostics described above), plus
    Playwright's own `test-results/`/`playwright-report/` output from the ad hoc runs — all removed,
    none of their contents committed.
  - working-tree round-trips used for the three decoration checks, each confirmed clean
    (`git status --short`) both before and after:
    `git checkout c813696 -- "web/app/(staff)/staff/cats/[catId]/parasite-control/page.tsx"` (`U34`);
    the same file plus `web/mocks/handlers/areas/cat-profiles.ts` (the A11 e2e assertion);
    `web/components/common/staff-shell.tsx` (the back-navigation spec, checked twice — once for the
    committed spec's own decoration check, once more while running the two ad hoc diagnostics).
  - a manually-started `npm run dev` (curl-warmed at `/staff/cats`, the parasite-control route, and
    `/staff/vaccines` before use, avoiding the documented dev-mode cold-start artefact) was used for
    the three decoration checks and the two ad hoc diagnostics; stopped before this commit.
- Files this pass read but did not modify: `web/lib/api/cats.ts`, `web/components/cat/
  ParasiteTreatmentForm.tsx`, `web/components/cat/FormFieldRow.tsx`, `web/lib/hooks/useVaccines.ts`,
  `web/lib/dateOnly.ts`, `docs/handover/HO-CAT-002.md` (the owner's own Amended rows),
  `docs/gates/HO-CAT-002-ACCEPTANCE.md` (BLOCKER 1's exact wording, for R18's citation). This pass
  also read the in-progress, uncommitted `docs/gates/HO-CAT-002-ARCHITECTURE.md` and
  `-CODE_REVIEW.md` (being written in parallel by those gates on this same commit) for context only
  — neither was modified by this gate, and nothing in either file's own findings changed this pass's
  own verdict, which is scoped to runtime/test behaviour, not the contract-shape question
  ARCHITECTURE's own in-progress HIGH finding on that pair of files is about.
- The production code under test this pass (`b83bd2a`, `f392132`, `8b38cc3`, `ccf29b1`, `631904f`)
  is the integrator's own commits, not this gate's — this gate verified and reported on them, per
  its mandate.
- Files outside allowed scope: none.
