GATE: CODE_REVIEW
HANDOVER: HO-CAT-002
COMMITS: c2d941c 7f5301c 03226e9 b546fb1
DATE: 2026-09-25
STATUS: PASS

This is the fourth pass of this gate, a short delta over `git diff 0efab8c..b546fb1 -- web/` (4
commits). It does not re-review anything already covered by earlier passes — see History below.

- **`c2d941c` (stale next-due banner fix, verified)** — the third pass's HIGH is genuinely fixed
  for the case that matters: `catQuery.data` for a cat not already in the React Query cache is
  `undefined` on the render that follows a `catId` change, so `if (!catQuery.data) return null`
  fires before the JSX with the stale banner is ever reached; `useEffect(() => …, [catId])` is
  declared unconditionally above that early return, so it still commits on the null-returning
  render and clears `lastRecordedNextDueOn` before the cat's data (and the banner) can render at
  all. The regression test proves exactly this: `renderResult.rerender(renderFor(catIdB))` reuses
  the RTL root without unmounting (confirmed by reading `@testing-library/react`'s `rerender`
  semantics — it does not call `unmount`), matching an App Router same-segment navigation. Traced
  through by hand: without the `useEffect`, `lastRecordedNextDueOn` stays at cat A's computed
  `'2026-12-19'` after the rerender, `screen.findByLabelText(/^Product/)` still resolves once cat
  B's data loads, and `expect(screen.queryByRole('status')).not.toBeInTheDocument()` fails because
  the stale banner is still there — the test is not vacuous. Ran it, plus the whole affected unit
  suite, to confirm: 104/104 unit tests pass with the fix in place.
  - **One residual gap, not blocking**: `useCat`'s default React Query `staleTime` is `0`, but
    React Query still returns a cache hit *synchronously* on the render that changes `queryKey` if
    that key was already populated by an earlier visit (e.g. staff browsing cat A, then cat B,
    then back to cat A within the same session). In that specific case `catQuery.data` is
    non-`undefined` on the very first render after the `catId` change, the early-return guard does
    *not* fire, and the JSX renders once with cat A's stale `lastRecordedNextDueOn` before
    `useEffect` clears it on the next commit (`useEffect`, not `useLayoutEffect`, runs after
    paint) — a genuine single-frame flash of the wrong cat's date, not covered by the new test
    because it uses a fresh `QueryClient` per render pair so `catIdB` is never pre-cached. This is
    materially narrower than the third-pass HIGH (self-corrects within one frame instead of
    persisting indefinitely) and does not block this pass, but keying the page itself by `catId`
    (e.g. `key={catId}` somewhere in `layout.tsx`/`CatProfileDataShell`, forcing a real remount)
    would derive the state instead of reconciling it after the fact and remove the flash window
    entirely, at the cost of remounting the tab's data-fetching too. Left as a suggestion.
  - `catId` correctly dropped from `RecordCatParasiteTreatmentResponse` and the MSW handler;
    `nextDueOn` correctly made non-nullable in both the type and the page (the dead "No next-due
    date" branch is gone). No remaining `nextDueOn: string | null` reference for this endpoint —
    the one hit at `cats.ts:123` belongs to the unrelated vaccination-status shape.
- **`7f5301c` (`FormFieldRow`)** — `required={required}` now reaches the `<input>` itself; before
  this, `ParasiteTreatmentForm.tsx` already passed `required` to four fields for the visual
  asterisk, but nothing enforced it natively, so this is a real fix, not decoration. Focus and
  error styling (`focus-visible:outline-2 focus-visible:outline-offset-2
  focus-visible:outline-accent`, `error && 'border-danger-ink'`) is byte-for-byte identical to
  `FieldWithUnit`'s existing classes. Correct.
- **`03226e9` (back-navigation spec rewrite)** — replaces the second `page.goto()` with a real
  in-sheet `<Link>` click, so `goBack()` exercises Next.js's own popstate handling rather than a
  hard reload that would reset `sheetOpen` regardless of the fix. Confirmed the referenced
  selectors exist (`aria-label="Expand menu"`, `id="main-content"` in `staff-shell.tsx`; a
  `getByRole('link', { name: 'Vaccine catalogue' })` reachable from the sheet). The commit message
  records the required proof directly (checked out pre-fix `staff-shell.tsx`, ran the spec — 2/2
  failed; restored, re-ran — 2/2 passed; tree clean before and after) — didn't repeat that
  destructive revert-and-restore experiment on this worktree since architecture is writing to it
  in parallel and this pass is read-only apart from its own verdict file, but the recorded method
  is sound and the assertions (`dialog` count 0, `aria-hidden` cleared on both `#main-content` and
  the trigger, live focus after `Tab`) match what the fix actually does.
- **`b546fb1` (`useAllVaccines` ceiling test)** — `vi.mock('@/lib/api/vaccines', …)` is declared at
  the top of `use-vaccines.test.tsx` only; Vitest's default `isolate: true` (unchanged in
  `vitest.config.mts`, no `pool`/`isolate` override) gives each test *file* its own module
  registry, so this mock cannot leak into `vaccine-list-page.test.tsx` or any other file. Ran
  `vitest run` on both files together plus `parasite-control-page.test.tsx`: 4/4 pass. Ran the full
  suite: 37 files / 104 tests, all pass — no leakage, no regressions.

FINDINGS
- None at Critical/High/Medium.

SUGGESTIONS
- `web/app/(staff)/staff/cats/[catId]/parasite-control/page.tsx` — the `useEffect([catId])` reset
  still allows a one-frame flash of the previous cat's next-due banner when React Query already
  has the new `catId` cached from an earlier visit in the same session (see above). Consider
  deriving the reset instead of reconciling it after paint, e.g. remounting the page by `catId`
  (a `key` somewhere between `layout.tsx` and this component) rather than relying on an effect
  that necessarily runs one commit after the stale render.

SCOPE CHECK
- files in allowed scope: 6/6 — `git diff 0efab8c..b546fb1 --stat -- web/` (parasite-control
  page.tsx, FormFieldRow.tsx, lib/api/cats.ts, the MSW handler, the back-navigation e2e spec, the
  parasite-control unit test) plus 1 new file (`tests/unit/lib/use-vaccines.test.tsx`), all under
  `web/**`.
- files outside allowed scope: none

---

## History

### Third pass — FAIL (2026-09-25)
Reviewed `git diff d0ed408..631904f -- web/`. One HIGH: `lastRecordedNextDueOn` local `useState`
in `parasite-control/page.tsx` was reset only by a new `handleSubmit` call, never by `catId`
changing, so a client-side navigation from one cat's `/parasite-control` tab to another's could
show cat A's next-due date attributed to cat B indefinitely. Fixed by this fourth pass's
`c2d941c`. Full third-pass report (recovered here for the record):

> - **`f392132` (StaffShell nav-sheet close-on-route-change)** — fixes the MEDIUM carried from the
>   second pass. `useEffect(() => setSheetOpen(false), [pathname])` fires once on initial mount
>   too, but `sheetOpen` is already `false` there, so React bails out on the no-op `setState` (no
>   extra render, no observable effect). It does not interfere with in-sheet link navigation:
>   `SheetNav`'s own `onClick={onNavigate}` already closes the sheet synchronously on click; the
>   `pathname` effect only re-confirms the same `false` value one tick later, once the route has
>   actually changed. `NavSections`'s compact/full unification is correct.
> - **`8b38cc3` (`FormFieldRow` extraction)** — aria wiring identical for all 11 replaced fields.
> - **`ccf29b1` (weight-row-id `Map`)** — perf fix only, no behaviour change; the `?? point.
>   recordedOn` fallback is unreachable dead code, not a bug.
> - **`631904f` (vaccine pagination ceiling)** — correct bounded loop, surfaces as `isError`.
>
> FINDINGS: [HIGH] parasite-control/page.tsx — `lastRecordedNextDueOn` not reset on `catId`
> change; untested. Fix: `useEffect(() => setLastRecordedNextDueOn(undefined), [catId]);` plus a
> regression test rendering cat A then re-rendering with cat B's `catId`.
>
> SUGGESTIONS: None beyond the `FormFieldRow` `aria-invalid="false"` note, not a defect.

### Second pass — PASS (2026-09-25)
Reviewed `git diff 241d310..c813696 -- web/`. One MEDIUM (StaffShell nav sheet not closing on
back/forward navigation) and one suggestion (weight-row-id `indexOf` O(n²)) — both addressed by
the third pass's `f392132`/`ccf29b1`. Full second-pass report (recovered here for the record):

> Both of the first pass's MEDIUMs hold (`a76aca5` vaccine pagination, `07e0733` MedicalProfileForm
> row errors — both correctly fixed with regression coverage). The two carried LOW/suggestion items
> are also fixed (`6e71fae` empty-state title, `ee59d2a`/`b19928e` same-date row-key collision).
> `92a33e5`'s `sheet-left` dialog variant is byte-for-byte equivalent for every existing `'center'`
> caller. `dbba453`…`c813696`'s StaffShell rebuild: resize-listener cleanup correct, no SSR/hydration
> mismatch, the duplicate `<nav>` is real but inert (never simultaneously exposed to assistive
> tech), no duplicate ids. New MEDIUM: the sheet doesn't close on a route change other than its own
> links (most notably browser Back/Forward), untested. Superseded-drawer dead code: none found.
>
> FINDINGS: [MEDIUM] staff-shell.tsx — sheet stays open across Back/Forward navigation, still
> trapping focus. Fix: `usePathname()` + `useEffect(() => setSheetOpen(false), [pathname])`.
>
> SUGGESTIONS: weights/page.tsx `weightRowId`'s `indexOf` is O(n) per row, O(n²) over the table.

### First pass
Stopped at `241d310`; superseded by the second pass per ACCEPTANCE's BLOCKER 2
(`HO-CAT-002-ACCEPTANCE.md`).
