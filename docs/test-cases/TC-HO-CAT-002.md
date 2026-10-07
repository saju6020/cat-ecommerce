# TC-HO-CAT-002 — Cat Profiles (frontend)

Handover: docs/handover/HO-CAT-002.md
Commit(s) under test: 2fcd392 0f6bc0e b1f380c c6cb154 a408ab1 40ea4a8 70ae99a 94a626b 824b1fb
bee5be8 542477e c3a076a 1a4cac4 fdef977 ad776b0 09a01fd 820f5dc 0c5a59b 454f6f9 bb9fef1 47d8f10
06121d9 4187ef8 241d310 e4f368c a76aca5 07e0733 6e71fae ee59d2a 24828c5 dbba453 b19928e f478f41
7304f85 7f758b3 8379121 bb24673 92a33e5 1bf95d8 b209ef7 c813696 b83bd2a f392132 8b38cc3 ccf29b1
**631904f** (branch head, `ho/CAT-002`)
Written: 2026-09-25 · Executed: 2026-09-25 (1st pass) · Re-executed: 2026-09-25 (2nd pass) ·
Re-executed: 2026-09-25 (3rd pass) · Re-executed: 2026-09-25 (4th pass) ·
Re-executed: 2026-09-25 (5th pass) · Re-executed: 2026-09-25 (6th pass) ·
Re-executed: 2026-09-25 (7th pass) · **Re-executed: 2026-09-25 (8th pass, targeted delta)**

> **Eighth pass (2026-09-25), commits `b83bd2a` + `f392132` + `8b38cc3` + `ccf29b1` + `631904f`,
> a targeted delta over `git diff c813696..631904f`, per this pass's own brief (faster passes:
> build gates + one full `npm test` + one full serial Playwright run + decoration checks limited
> to the three genuinely new tests, not a full re-audit of everything the 7th pass already closed).**
> Five owner-decided changes: (1) `b83bd2a` — A11 ("records and shows its next-due date"), blocked
> since the 1st pass and the subject of ACCEPTANCE's BLOCKER 1, is unblocked by an owner amendment
> to HO-CAT-001 C10's `201` body (now carries `nextDueOn`); the parasite-control page renders it as
> visible `role="status"` text, never recomputed client-side, with a new unit case (`U34` in this
> sheet — **the handover's own §15 table also calls this case "U33"; that ID was already taken in
> *this* sheet by the pre-existing `StaffShell` toggle case, assigned at the 3rd pass, well before
> the handover itself had a `U33` — using `U34` here to avoid a collision, not to disagree with the
> handover, and flagging the mismatch rather than silently absorbing it**) and an extended
> `cat-parasite-control.spec.ts`. (2) `f392132` — `StaffShell`'s below-`sm` sheet now closes on any
> route change (`usePathname()`-keyed effect), fixing the CODE_REVIEW gate's 2nd-pass MEDIUM (the
> sheet survived the browser's own Back/Forward, which bypass the sheet's in-DOM `onNavigate`
> entirely), plus a unified `NavSections` renderer replacing the old, type-duplicating `SheetNav`.
> (3) `8b38cc3` extracts `FormFieldRow` from `MedicalProfileForm`'s seven hand-copied error blocks
> (ARCHITECTURE MEDIUM). (4) `ccf29b1` drops a dead, speculative `CatWeightPoint.id` branch and
> makes the row-id lookup O(1) via a `Map` (ARCHITECTURE LOW + CODE_REVIEW perf note). (5) `631904f`
> caps `useAllVaccines`'s pagination loop at 100 pages with a thrown error past that, closing the
> ARCHITECTURE LOW (no page-count ceiling) in code, though still with no dedicated test — see "Gaps
> left deliberately".
>
> **Build gates, re-run clean**: `npm run typecheck` (0 errors), `npm run lint` (0 warnings),
> `npm run format:check` (Prettier clean). `npm test` — **102/102 passed** (up from the 7th pass's
> 101 — the one new case is `U34`/`parasite-control-page.test.tsx`). `npx playwright test
> --workers=1` (one full serial run, per this pass's own reduced brief) — **110 total, 110 passed,
> 0 failed**, 7.7 minutes (up from the 7th pass's 108 — the two new executions are
> `staff-shell-sheet-closes-on-back-navigation.spec.ts` × 2 projects). **360px (E18/E19) passed
> clean on both projects in this one run, with nothing to re-isolate** — per this pass's own brief,
> the triple E18 isolation runs and the clean prod build are skipped this pass, since the full run
> surfaced no 360px problem to settle.
>
> **U34 and the new A11 e2e assertion are both proven non-decorative, watched failing on the real
> mechanism before being relied on.** `U34`: checked out `parasite-control/page.tsx` at its exact
> pre-change (`c813696`) content and re-ran `parasite-control-page.test.tsx` — **1/1 failed**
> (`findByRole('status')` timed out — the pre-change page never rendered a status region at all,
> only a toast and a PDF link); restored and re-ran — **1/1 passed**. The new e2e assertion
> (`cat-parasite-control.spec.ts`'s `getByRole('status').getByText('Next due 19 Dec 2026')`):
> checked out both `parasite-control/page.tsx` and `mocks/handlers/areas/cat-profiles.ts` to their
> exact pre-change (`c813696`) content (the mock's own `nextDueOn` computation, standing in for
> HO-CAT-001 §8.2's aggregate rule, is part of the same amendment) and re-ran — **1/1 failed**
> (`getByRole('status')` — element(s) not found, identical mechanism to `U34`'s failure); restored
> and re-ran — **1/1 passed**. Working tree was clean before and after both checks; nothing was
> committed.
>
> **The back-navigation e2e (`staff-shell-sheet-closes-on-back-navigation.spec.ts`) is a genuine
> production fix with a decorative committed test — reported, not silently accepted.** Checking out
> `staff-shell.tsx` to its exact pre-fix (`c813696`) content and re-running the committed spec
> **passed 1/1**, not the expected fail. Investigated rather than waved through: the spec builds its
> "before" navigation history with two `page.goto()` calls, and a temporary, uncommitted diagnostic
> (network-request tracing) confirmed `page.goBack()` after a `page.goto()`-built history performs a
> genuine hard document reload (`DOCUMENT REQUESTS DURING GOBACK` was non-empty, naming the target
> URL) — which trivially resets `sheetOpen` to its initial `false` by remounting the whole app, the
> same outcome the fix produces for an entirely different reason. This is not the scenario
> CODE_REVIEW's 2nd-pass MEDIUM or `f392132`'s fix are about (a **soft**, client-side route change
> that leaves `StaffShell`'s own instance mounted with stale state). A second temporary diagnostic,
> using a real in-sheet `<Link>` click to build the forward-navigation history instead of
> `page.goto()`, confirmed both halves directly: against the pre-fix `staff-shell.tsx`, `goBack()`
> produced **no** document request (a genuine soft nav) and the sheet **stayed open**
> (`getByRole('dialog')` count `1`) — the real, reproduced bug; against the fix, the identical soft
> nav closed it (count `0`). **The production fix (`f392132`) is confirmed genuinely correct on the
> real mechanism.** The committed regression spec is not: it passes identically whether the fix is
> present or not, because its own setup never produces the soft navigation the bug requires — it
> currently provides no regression protection for the MEDIUM it was written to guard. Reported as a
> TEST-suite finding below, not fixed (this gate verifies and reports; it does not rewrite an
> already-committed spec unilaterally) — see `docs/gates/HO-CAT-002-TEST.md`'s FINDINGS and EXISTING
> TESTS JUDGED WEAK sections. Both
> temporary diagnostic spec files were removed before this commit; nothing from either check was
> committed. This does not change the pass's own e2e-row status (the spec was **watched pass**, both
> against the fix and, undesirably, against its absence) but is disclosed in full rather than
> reported as a clean, proven-non-decorative regression case.
>
> **Rows updated this pass**: A11's acceptance-criteria and end-to-end rows (both flip to full PASS
> — the "shows its next-due date" evidence gap ACCEPTANCE's BLOCKER 1 named is closed); §16 E6 gets
> a note recording the owner's 2026-09-25 acceptance of this sheet's own wording, resolving
> ACCEPTANCE's "undeclared deviation" item; two new rows (`U34`, and a new "not a §16 ID" row for
> the back-navigation case); `MedicalProfileForm`'s regression row (R2) and the unit rows its own
> `FormFieldRow` extraction touches are re-confirmed directly against the refactor, not assumed
> unchanged. No row from the 1st–7th passes' own findings needed re-litigating — this delta didn't
> touch any of the mechanisms they were about.

> **Seventh pass (2026-09-25), commits `92a33e5` + `1bf95d8` + `b209ef7` + `c813696`.** The 6th pass
> FAILed on two MEDIUMs, both on the same HO-CAT-002 §8.5 text: `bb24673`'s `inert`-on-siblings fix
> closed the 5th pass's keyboard-focus-trap gap, but (1) neither `Escape` nor a scrim click ever
> returned focus to the toggle once Tab had moved it onto a drawer nav link first, and (2) a live
> toast (`<Toaster />`, rendered at the root layout, outside `StaffShell`'s own tree) was never
> covered by the `inert` fix at all and stayed fully Tab-reachable. Rather than patch either gap
> individually, the integrator stopped hand-rolling the below-`sm` drawer entirely and rebuilt it on
> `@radix-ui/react-dialog` — exactly what §8.5 itself names ("shadcn/ui's Radix primitives give
> this... hand-rolled dialogs do not"). `92a33e5` adds a `variant: 'center' | 'sheet-left'` prop to
> the shared `DialogContent` (`center` unchanged — `ConfirmDialog` and everything built on it are
> untouched); `1bf95d8` makes `StaffShell`'s below-`sm` `<aside>` a pure-CSS, always-`w-16` rail with
> no widening state left at all, and its "Expand menu" button a `DialogTrigger` that opens the full
> nav as a `sheet-left` Radix `Dialog` — focus-trapping, `Escape`, overlay-click, focus-return, and
> hiding the rest of the document from assistive tech (via Radix's own `FocusScope`/`hideOthers`, not
> `inert`-on-hand-picked-siblings) all come from Radix itself now, so the toast gap is closed
> structurally rather than by chasing every sibling that needs marking; `b209ef7` rewrites the
> staff-shell e2e specs (two of the 4th/3rd-pass specs, whose premise — an in-flow aside that widens
> below `sm` — is now structurally impossible, are rewritten to the equivalent contract against the
> sheet; each file's own top comment explains the change) and adds `staff-shell-sheet-helpers.ts`;
> `c813696` rewrites the unit tests the same way.
>
> **This pass's brief, again, was not "confirm the two open findings are fixed and stop."** It
> required judging the rewritten specs honestly (do the new attribute-based locators paper over a
> real a11y regression — specifically, does the trigger still have an accessible name when the sheet
> is closed?), watching the two 6th-pass regression specs fail against the pre-rebuild
> (`9464acd`) `staff-shell.tsx`/`dialog.tsx` and pass against the rebuild, one full audit of the new
> sheet against UI-1/UI-6/§8.5/NFR-USA-* (focus on open, trap both directions, `Escape`/overlay/close
> button each returning focus, a nav-link click closing and navigating, screen-reader exposure,
> resizing across 640 both ways, 768/1024/1440 unchanged, the skip link, toasts, and the existing
> center dialogs not regressed), a clean `rm -rf .next` prod build checked at 360/1440 to settle the
> 6th pass's own unresolved prod-build anomaly, and a full re-execution of every row.
>
> **Both 6th-pass MEDIUMs are confirmed fixed, and confirmed on the real mechanism, not the
> rewritten spec's own say-so.** Restoring `staff-shell.tsx`/`dialog.tsx` to their exact pre-rebuild
> (`9464acd`) content and re-running `staff-shell-drawer-focus-not-returned-to-trigger.spec.ts` and
> `staff-shell-drawer-toast-not-inert.spec.ts` against it — **3 of 3 failed**, all on
> `getByRole('dialog')` timing out (`element(s) not found` — the old hand-rolled drawer never
> rendered anything with `role="dialog"`), not a loosely-worded assertion; restoring the rebuild and
> re-running returned **3/3 passed**. The rewritten specs' own contract is judged genuine, not
> decorative, the same way: every file's assertions are unchanged in *what* they check (focus
> returns to the trigger; a toast stays untouched; Tab doesn't escape; no overflow; no
> pointer-clickable content underneath), only *how* elements are located changed to match the new
> DOM.
>
> **The attribute-based locators in `staff-shell-sheet-helpers.ts` do not hide a real regression —
> checked directly, not assumed.** The concern this pass's brief named directly: resolving the
> trigger by `[aria-label="Expand menu"]` instead of `getByRole` could paper over the trigger losing
> its accessible name once Radix's `modal` behaviour marks it `aria-hidden` (a `getByRole` query
> would silently refuse to find it either way, for two different reasons — no accessible name, or
> correctly `aria-hidden` — and the helper's comment is explicit that it switched *because of* the
> second reason, which makes the first worth checking rather than assuming). Confirmed with the
> sheet **closed**: `page.getByRole('button', { name: 'Expand menu' })` — Radix's own `role` query,
> not the attribute selector — finds it directly (`staff-shell-drawer-toast-not-inert.spec.ts`'s own
> setup already does this, unprompted, and passes). The trigger has a real, stable accessible name
> whenever it is actually interactive; the attribute selector is only ever reached for while the
> sheet is *open*, which is exactly when a `getByRole` query is *supposed* to fail (the trigger is
> legitimately `aria-hidden` then, along with the rest of the document) — not a workaround for a
> broken accessible name.
>
> **The full audit found no further §8.5/UI-1/UI-6/NFR-USA-* violations** — see the FINDINGS section
> of `docs/gates/HO-CAT-002-TEST.md` for the complete, itemised list, including the two carried-forward
> NOTEs (no `aria-modal="true"` on the Radix `Dialog`'s own content node; the `<main>` landmark tag
> itself is not marked `aria-hidden`, though every one of its rendered children individually is, via
> Radix's `hideOthers`, so nothing inside it is reachable or exposed — a screen reader's landmark list
> would show an anomalous empty "main" region while the sheet is open, cosmetic, not a violation).
> **The center-docked dialogs (`ConfirmDialog` and everything built on it — `MarkDeceasedDialog`,
> `TransferOwnershipDialog`, `HighRiskFlagDialog`) are confirmed unregressed** by `92a33e5`'s
> `variant` prop: their own unit tests (17/17 across 5 files) and e2e coverage (E10, E11/E12, E13,
> the parasite-control extra case) all re-run clean.
>
> **The 6th pass's own unresolved prod-build anomaly (Tailwind width classes not applying, the
> toggle's `onClick` not updating state, on one ad hoc local build) does not reproduce on this pass's
> clean prod build.** `rm -rf .next && npm run build` compiled clean (0 errors), and `npm run start`
> against it, checked directly rather than assumed: at 360px the `<aside>` is genuinely the 64px rail
> (`boundingBox().width` = 64, not the class list read statically), no horizontal overflow
> (`scrollWidth`/`clientWidth` both 360), and "Expand menu" opens a real `role="dialog"` sheet that
> closes on `Escape` and returns focus to the trigger; at 1440px, clicking "Collapse menu" moves the
> `<aside>` from 240px to 64px (a real, observed width change, not a static read) and its `position`
> stays `sticky`. The only console output was the two already-documented, expected MSW-race
> `ERR_CONNECTION_REFUSED` messages (§5.1) — no hydration warnings, no React errors. This is judged a
> settled non-issue for the current code: the 6th pass's own build was correctly not relied on and not
> reported as a finding at the time, and this pass's independent, clean rebuild finds nothing wrong to
> carry forward.
>
> **Both of the 6th pass's FAIL rows below flip to PASS**, given permanent regression coverage (R15,
> R16) rather than being merely re-confirmed, and STATUS moves to **PASS** for the first time in this
> handover's TEST-gate history — this pass's own full audit found zero new requirement violations,
> only the two carried-forward NOTEs above.

> **Sixth pass (2026-09-25), commit `bb24673`.** The 5th pass FAILed on one MEDIUM: the 360px
> drawer's scrim protected the mouse but not the keyboard, so Tab reached and Enter activated
> `/staff/cats`'s "Register a cat" button behind the scrim. `bb24673` fixes this with a
> `matchMedia('(max-width: 639.98px)')` check, taken only while the sidebar is manually expanded,
> that decides whether the expanded sidebar is currently the below-`sm` drawer; while it is, the
> skip link and the header/`<main>` column both get a real `inert` attribute. Layout stays pure CSS
> (E18's mount-timing race can't return). This pass's brief required doing **one complete
> accessibility and behaviour audit** of the whole drawer against HO-CAT-002's stated requirements —
> not chasing one finding at a time — covering focus-on-open, focus cycling, `Escape`, scrim click,
> focus return after close, screen-reader exposure of the inert content, resizing across 640px both
> ways, 768/1024/1440px unchanged behaviour, the `mobile-360` project, and the skip link.
>
> **The 5th pass's MEDIUM is confirmed fixed.** The rewritten
> `staff-shell-drawer-not-focus-trapped.spec.ts` (its middle assertion now checks focus never lands
> in `main`/`header`, not that it does) passes 2/2 in isolation, in the full serial run, and in the
> full default-parallel run. Beyond trusting the spec, this pass verified the fix at the level that
> actually matters for a screen-reader user: a **real Chrome DevTools Protocol accessibility-tree
> query** (`Accessibility.getFullAXTree`, not Playwright's own `ariaSnapshot()` convenience API)
> confirms `<main>` and "Register a cat" are genuinely absent from the browser's own accessibility
> tree while the drawer is open — not merely excluded from the tab order. (Playwright's
> `ariaSnapshot()` was tried first and misleadingly still showed `<main>`'s content; cross-checked
> against the real CDP accessibility tree before trusting either result — see Environment.)
>
> **But the full audit this pass's brief required — not stopping at the one mechanism already fixed
> — found two further, real gaps the fix didn't cover, both on the same §8.5 text the 5th pass's
> finding cited.** First: neither the `Escape` handler nor the scrim's `onClick` ever moves focus
> back to the toggle, so "closes on `Escape`... and returns focus to its trigger" only ever held by
> accident, in the one case (`Escape` pressed immediately after opening) where focus had never left
> the toggle to begin with — once Tab has moved focus onto a drawer nav link, `Escape` leaves it
> there, and a scrim click drops it to `document.body` entirely (the same "focus lost to `<body>`"
> failure mode this component's own 2nd-pass MEDIUM was about, reached here by a different path).
> Second: `<Toaster />` (`app/providers.tsx`) is rendered as a sibling of `StaffShell`'s entire tree
> at the root layout, not inside it, so the fix's `inert` attributes never reach it — a live toast's
> "Dismiss" button stays fully Tab-reachable and activatable while the drawer is nominally open.
> Both are reported as findings, not fixed, per this gate's mandate; both got permanent regression
> tests. This is why STATUS is FAIL again, for a sixth, different reason — not a repeat of the 5th
> pass's finding, which is genuinely closed.

> **Fifth pass (2026-09-25), commit `8379121`.** The 4th pass FAILed on one MEDIUM: the 360px
> overlay had no backdrop, so it visually and interactively covered nearby main-content controls
> (e.g. `/staff/cats`'s "Register a cat" button). `8379121` resolves this by turning the overlay into
> a standard off-canvas drawer: a `data-testid="staff-shell-scrim"` `div` (`fixed inset-0 z-30
> sm:hidden`) now sits between the aside (`z-40`) and the page, closes the drawer on click, and
> `Escape` closes it too — both returning `override` to `null` (the viewport default). The
> integrator rewrote the 4th pass's own regression spec
> (`staff-shell-manual-expand-occludes-content.spec.ts`) to assert this drawer contract instead of
> its original "stays uncovered while open" assertion, which the drawer pattern itself contradicts,
> and added a unit test for `Escape`. This pass's brief required judging that rewrite rather than
> taking it on trust, a real-browser keyboard check of whether Tab can reach anything behind the
> scrim, and a full re-execution of every row below.
>
> **The rewrite is judged genuine, not decorative, for what it actually asserts**: restoring
> `staff-shell.tsx` to its exact pre-fix (`ee54879`) content and re-running the rewritten spec against
> it failed 2 of its 3 cases (`toBeVisible()`/`toBeFocused()` timeouts — the scrim and the `Escape`
> handler simply don't exist yet), on the real mechanism, not a loosely-worded assertion; restoring
> the fix returned 3/3. The third case (no scrim at 1440px) passes both before and after, since
> neither version ever had a scrim there — a legitimate invariant, just not one that distinguishes
> fixed from unfixed code by itself. **And for the specific defect the 4th pass raised — a
> half-exposed, still-mouse-clickable control with nothing signalling it was inert — the drawer
> contract is a real, complete fix**: `document.elementFromPoint` sampled across the *entire* width
> of "Register a cat" (left edge, centre, right edge) resolves to the `<aside>` or the scrim at every
> point while the drawer is open — no mouse click can reach it. That part of UI-1's "no clipped
> controls" wording is satisfied, for pointer input, and is not a silent pass: it is confirmed
> directly, watched, below.
>
> **But this pass's own required keyboard check — "while the drawer is open, can Tab reach anything
> behind the scrim?" — found that it can, and that it can be activated.** The scrim is a plain
> `aria-hidden` `div` with an `onClick`; nothing removes `<main>` from the tab order or traps focus
> inside the `<aside>` while the drawer is open. Three Tabs from the toggle (past the drawer's own two
> nav links) land on "Register a cat" itself — confirmed directly, not inferred — and pressing `Enter`
> there fires the button's native click handler (which does not consult mouse hit-testing) and
> **navigates to `/staff/cats/new` while the scrim is still visible and the toggle still reads
> `aria-expanded="true"`**. This is not a stray edge case: HO-CAT-002 §8.5 states, verbatim, "Every
> dialog traps focus, closes on `Escape`, and returns focus to its trigger... **Hand-rolled dialogs do
> not**" — and this drawer, introduced by `8379121` itself as a hand-rolled scrim overlay, reproduces
> exactly the gap that sentence warns about: it closes on `Escape` and returns focus correctly, but it
> does not trap focus. **This is reported as a finding, not fixed**, with a permanent regression test,
> and **recorded as a decision for ACCEPTANCE and the owner** — not a silent pass — on whether the
> drawer pattern, as shipped, satisfies UI-1/§8.5 without a focus trap. This is why STATUS is FAIL
> again, for a fifth, different reason.

> **Fourth pass (2026-09-25), commit `7f758b3`.** The 3rd pass FAILed on one MEDIUM: manually
> expanding `StaffShell`'s sidebar at the 360px floor pushed `document.documentElement` 24–28px
> past its own viewport, because the manual override's `w-60`/`w-16` width classes applied
> unconditionally, with no `sm:` guard. `7f758b3` fixes this by making the expanded aside
> `max-sm:fixed max-sm:inset-y-0 max-sm:left-0 max-sm:z-40 max-sm:shadow-lg` below `sm` — an
> overlay laid on top of the page rather than pushed alongside it — plus a `w-16 sm:hidden` spacer
> so the content's existing 64px offset doesn't move. Only `staff-shell.tsx` and the 3rd pass's own
> regression spec's doc comment changed; the spec's assertions are untouched. This pass re-executed
> **every** row below, re-ran the full command list (`typecheck`/`lint`/`test`/`test:e2e`), ran
> E18's spec in isolation three times as required, proved the 3rd pass's regression spec
> (`staff-shell-manual-expand-overflow.spec.ts`) is not decorative by restoring `staff-shell.tsx` to
> its exact pre-fix (`6baf0bb`) content and watching all 4 of its cases fail against it, then
> restoring the fix, and did a real-browser, keyboard-only walkthrough of the 360px overlay (toggle
> visibility/focus/reachability after expanding, the overlay's own nav links tabbable and unclipped,
> collapsing back to the 64px rail cleanly) plus a check that 1440px behaviour is unchanged
> (`position: sticky`, `w-60`/`w-16`, never `fixed`). All of that confirms the fix and finds nothing
> wrong with it. **But the same keyboard walkthrough's own "not clipped" check, extended from the
> overlay's nav links to nearby main-content controls, surfaced a new, real defect**, reported
> below: the fixed overlay has no backdrop, so on `/staff/cats` its 240px-wide expanded band
> visually and interactively covers part of the page's own "Register a cat" button and a filter
> checkbox — `document.elementFromPoint` on the button's own left edge resolves to the sidebar's
> "Cats" link, not the button, confirmed with a screenshot. This is a different manifestation of the
> exact UI-1 wording ("no clipped controls") the 3rd pass's finding sat on, not a repeat of it (no
> horizontal overflow is present here at all) — reported as a finding, not fixed, per this gate's
> mandate. This is why STATUS is FAIL again, for a fourth, different reason.

> **Third pass (2026-09-25), commit `7304f85`.** The 2nd pass FAILed on a new MEDIUM:
> `StaffShell`'s sidebar toggle lost keyboard focus to `<body>` on its first activation, because
> `dbba453` had swapped a two-button fragment for a single button at the same JSX position, and
> React remounts (rather than updates) an element whose type changes at the same slot. `7304f85`
> fixes this by mounting **both** per-viewport buttons always, with stable `key`s, each deriving its
> state from `override ?? <its own viewport default>` — a click only ever updates props on an
> already-mounted element now. It also adds `tests/unit/staff-shell.test.tsx` (`StaffShell` had no
> unit test of any kind before this) and renames the e2e regression spec. This pass re-executed
> **every** row below, re-ran the full command list (`typecheck`/`lint`/`test`/`test:e2e`), ran
> E18's spec in isolation three times as required, audited the new unit test for decoration by
> reverting `staff-shell.tsx` to its pre-fix (`f478f41`/`dbba453`) content and watching the test fail
> against it, and checked with a real browser at 360px and 1440px that a manual toggle both follows
> the choice at each width and leaves the button visible/focused. The first three checks confirm the
> fix. **The last one surfaced a new, real defect**, reported below — not present because of
> `7304f85` (it is a `dbba453`-era design property, `override` deliberately ignoring the viewport),
> but never previously observed because no existing case interacts with the toggle at 360px and then
> measures layout. This is why STATUS is FAIL again, for a third, different reason.

> **Second pass (2026-09-25), commits `dbba453` + `b19928e`.** The 1st pass FAILed on E18 (360px
> horizontal overflow, §18 A20) and separately recorded a LOW (`WeightTrendChart`'s hidden a11y
> table repeating the same-date key collision `ee59d2a` had just fixed elsewhere). `dbba453` fixed
> E18 by making `StaffShell`'s sub-640px sidebar collapse a CSS default instead of a mount-time
> `matchMedia` effect. `b19928e` fixed the `WeightTrendChart` key collision. This pass re-executed
> **every** row below, not only the ones that changed, and additionally checked (per this pass's
> brief) that `StaffShell`'s own rendering, `nav-registry.test.ts`, and the sidebar toggle's
> keyboard/aria behaviour at both narrow and wide widths were not broken by `dbba453` — since
> `StaffShell` is a shared component every one of the 18 staff areas depends on. That last check
> found a **new** regression, reported below (not present before `dbba453`, and not fixed by this
> gate — this gate reports, it does not repair). Rows whose status didn't change carry a short
> "confirmed again" note rather than a full rewrite, since the same test files were re-run against
> the same fixtures and produced the same observations.

> Statuses record what was **observed**, not inferred. `BLOCKED` marks a case that could not be
> executed, or whose result was reasoned about rather than watched — it is not a failure, and it is
> not hidden.

## Summary

**1st pass** (2026-09-25): Total 67 — 62 PASS, 1 FAIL (E18), 4 BLOCKED, 0 NOT RUN.

**2nd pass** (2026-09-25, commits `dbba453` + `b19928e`): Total 70 — 65 PASS, 1 FAIL (the `StaffShell`
toggle focus-loss MEDIUM), 4 BLOCKED, 0 NOT RUN.

**4th pass** (2026-09-25, commit `7f758b3`): Total 75 — 70 PASS, 1 FAIL, 4 BLOCKED, 0 NOT RUN.

**5th pass** (2026-09-25, commit `8379121`): Total 77 — 72 PASS, 1 FAIL, 4 BLOCKED, 0 NOT RUN.

**6th pass** (2026-09-25, commit `bb24673`):

| | Count |
|---|---|
| Total | 80 |
| PASS | 74 |
| FAIL | 2 |
| BLOCKED | 4 |
| NOT RUN | 0 |

Breakdown: 33 unit cases (all PASS, unchanged — `bb24673` didn't touch anything U1–U33 exercise) +
14 regression cases (all PASS — R14 new, confirming `bb24673`'s fix of the 5th pass's MEDIUM) + 29
end-to-end rows (27 PASS — including the 5th pass's focus-trap finding, now fixed — 2 FAIL, two
**new** findings this pass's own full audit found) + 4 BLOCKED items in "Cases I could not run"
(unchanged, still real-API dependencies).

**7th pass** (2026-09-25, commits `92a33e5` + `1bf95d8` + `b209ef7` + `c813696`):

| | Count |
|---|---|
| Total | 82 |
| PASS | 78 |
| FAIL | 0 |
| BLOCKED | 4 |
| NOT RUN | 0 |

Breakdown: 33 unit cases (all PASS — U33's underlying file was rewritten for the Radix rebuild, now
8 tests instead of 7, still all pass) + 16 regression cases (all PASS — R15/R16 new, closing
regression coverage for the 6th pass's two MEDIUMs, both now fixed by the Radix rebuild) + 29
end-to-end rows (all 29 PASS — the 6th pass's two FAIL rows both flip to PASS, confirmed on the real
mechanism by a decoration check against the pre-rebuild `9464acd` code, not merely the rewritten
spec's own say-so) + 4 BLOCKED items in "Cases I could not run" (unchanged, still real-API
dependencies).

**8th pass** (2026-09-25, commits `b83bd2a` + `f392132` + `8b38cc3` + `ccf29b1` + `631904f`,
targeted delta), current:

| | Count |
|---|---|
| Total | 86 |
| PASS | 82 |
| FAIL | 0 |
| BLOCKED | 4 |
| NOT RUN | 0 |

Breakdown: 34 unit cases (all PASS — `U34` is new, for `parasite-control-page.test.tsx`; proven
non-decorative, see the header note) + 18 regression cases (all PASS — `R17` new, closing
regression coverage for CODE_REVIEW's 2nd-pass MEDIUM on `StaffShell`'s Back/Forward handling
(fixed `f392132`, though see the caveat on its own committed test in the header note and the gate
file's EXISTING TESTS JUDGED WEAK section); `R18` new, closing regression coverage for ACCEPTANCE's
BLOCKER 1 on A11)
+ 30 end-to-end rows (all 30 PASS — 29 carried unchanged from the 7th pass plus one new row for the
back-navigation case) + 4 BLOCKED items in "Cases I could not run" (unchanged, still real-API
dependencies).

**Verdict input for the gate file: STATUS = PASS.** All build gates and the full unit/e2e suites
are clean, including a fresh 360px pass on both projects in the one full serial run this pass's
own (reduced) brief called for. The two owner-decided contract changes this delta makes possible —
A11's next-due display, and `StaffShell` closing on Back/Forward — are both genuinely implemented,
confirmed on the real mechanism, not each test's own say-so: `U34` and the new e2e assertion for
A11 both watched failing against the pre-change code and passing against the fix; `f392132`'s fix
for the Back/Forward MEDIUM is independently confirmed correct via an ad hoc, non-committed,
link-based reproduction (real bug against the pre-fix code; genuinely fixed against the current
code) — **but its own committed regression spec is not a valid proof of this on its own**, since it
passes unchanged whether the fix is present or not (its `page.goto()`-built navigation history never
produces the soft client-side transition the bug requires). This is reported as a TEST-suite finding,
not a production defect — the shipped behaviour is correct — and does not, on its own, move STATUS
to FAIL, consistent with how this sheet has treated test-quality caveats (U22, U25) that don't
themselves represent a live requirement violation. See `docs/gates/HO-CAT-002-TEST.md`'s FINDINGS
section for the fix direction.

Breakdown: 33 unit cases (all PASS — U33's underlying file was rewritten for the Radix rebuild, now
8 tests instead of 7, still all pass) + 16 regression cases (all PASS — R15/R16 new, closing
regression coverage for the 6th pass's two MEDIUMs, both now fixed by the Radix rebuild) + 29
end-to-end rows (all 29 PASS — the 6th pass's two FAIL rows both flip to PASS, confirmed on the real
mechanism by a decoration check against the pre-rebuild `9464acd` code, not merely the rewritten
spec's own say-so) + 4 BLOCKED items in "Cases I could not run" (unchanged, still real-API
dependencies).

**Verdict input for the gate file: STATUS = PASS — the first passing verdict in this handover's
TEST-gate history.** The 6th pass's two MEDIUMs (closing the drawer not returning focus to the
trigger unless focus never left it; a live toast sitting outside the `inert` fix's reach) are both
confirmed fixed by the Radix `Dialog` rebuild, on the real mechanism: restoring `staff-shell.tsx`/
`dialog.tsx` to their exact pre-rebuild (`9464acd`) content and re-running the two 6th-pass regression
specs against it reproduces 3/3 failures (`getByRole('dialog')` timing out — the old hand-rolled
drawer never had a `role="dialog"` node), and restoring the rebuild returns 3/3 passes. This pass's
own required **full** audit of the new sheet against UI-1/UI-6/§8.5/NFR-USA-* — not stopping at the
two already-fixed mechanisms — found **no new requirement violations**: focus moves into the sheet on
open; Tab/Shift+Tab both stay trapped inside it (12 consecutive presses each direction, watched, never
reaching `main`/`header`); `Escape`, an overlay click, and the sheet's own close button each return
focus to the trigger; a nav-link click closes the sheet and navigates; nothing inside `main`/`header`/
the skip link/a live toast is Tab-reachable or exposed to a real Chrome DevTools Protocol
accessibility-tree query while the sheet is open; resizing across 640px in both directions is handled
correctly (narrowing closes nothing extra, widening past 640 while open auto-closes the sheet rather
than stranding the user in it); 768/1024/1440px are unchanged (`position: sticky`, no dialog ever);
the skip link is unaffected outside the drawer-open state; and the trigger has a real, `getByRole`-
findable accessible name whenever the sheet is closed (the helper's attribute-based locator is
required only while the sheet is legitimately `aria-hidden`, not compensating for a lost name). Two
NOTEs are carried forward (no `aria-modal="true"` on Radix's own `Dialog.Content`; the `<main>` tag
itself isn't marked `aria-hidden`, though every one of its rendered children individually is) — see
the gate file's FINDINGS section. A clean `rm -rf .next` prod build, checked directly at 360/1440,
does not reproduce the 6th pass's own unresolved build anomaly.

**Verdict input for the gate file: STATUS = FAIL, a sixth time, for a sixth reason.** The 5th pass's
MEDIUM (Tab reaching, and Enter activating, content behind the scrim) is confirmed fixed:
`staff-shell-drawer-not-focus-trapped.spec.ts` (rewritten by the fix to assert the corrected
contract) passes 2/2 in isolation, the full serial run and the full default-parallel run, and a real
Chrome DevTools Protocol accessibility-tree query independently confirms `<main>`/"Register a cat"
are genuinely absent from the browser's own accessibility tree while the drawer is open, not merely
excluded from Playwright's `ariaSnapshot()` (which gave a misleading "still exposed" reading —
cross-checked and not trusted on its own, see Environment). But this pass's own required **full**
audit — not stopping at the one already-fixed mechanism — found two further, real §8.5 violations the
fix didn't cover: (1) neither `Escape` nor a scrim click ever moves focus back to the toggle, so
"returns focus to its trigger" only ever held by accident in the one case where focus had never left
it; (2) the root-level `<Toaster />` sits outside `StaffShell`'s own tree, so its `inert` fix never
reaches a live toast's Dismiss button, which stays fully Tab-reachable while the drawer is open. Both
are new findings below, both fixed neither by the 5th pass's fix nor by this gate (reported, not
repaired, per this gate's mandate).

Breakdown: 33 unit cases (all PASS — U33's underlying file gained a 5th, `Escape`, test this pass,
still all pass) + 13 regression cases (all PASS, R1–R13 — R13 new, confirming `8379121`'s fix of the
4th pass's MEDIUM for pointer input) + 27 end-to-end rows (26 PASS — including the 4th pass's
occlusion finding, now fixed for pointer input — 1 FAIL, a **new** finding this pass's own
real-browser keyboard check found) + 4 BLOCKED items in "Cases I could not run" (unchanged, still
real-API dependencies).

**Verdict input for the gate file: STATUS = FAIL, a fifth time, for a fifth reason.** The 4th pass's
MEDIUM (the 360px overlay had no backdrop, so it visually and interactively covered part of `<main>`)
is confirmed fixed **for pointer input**: the rewritten `staff-shell-manual-expand-occludes-content.spec.ts`
watched pass 3/3 (both projects, 6/6) in the full serial run, proven non-decorative by restoring
`staff-shell.tsx` to its exact pre-fix (`ee54879`) content and watching 2 of its 3 cases fail against
it (the scrim/`Escape` cases; the third, "no scrim at 1440px", passes either way — see the header
note above), then restoring the fix and re-confirming 3/3. A real-browser mouse-hit-test check
(`document.elementFromPoint` across the full width of "Register a cat") confirmed no point on the
button is mouse-clickable while the drawer is open — either the `<aside>` or the scrim intercepts it.
But this pass's own required keyboard check — whether Tab can reach anything behind the scrim — found
that it can: three Tabs from the toggle land on "Register a cat" itself, and `Enter` navigates to
`/staff/cats/new` while the drawer is still nominally open (scrim visible, `aria-expanded="true"`).
This sits directly on HO-CAT-002 §8.5's own non-negotiable text about hand-rolled dialogs not trapping
focus. See the new finding below, and the header note's judgement on the rewritten spec.

**Third pass** (2026-09-25, commit `7304f85`): Total 73 — 68 PASS, 1 FAIL, 4 BLOCKED, 0 NOT RUN.

**Verdict input for the gate file: STATUS = FAIL.** The 1st pass's E18 finding (§18 A20, real
360px overflow) is confirmed fixed — reproduced clean on six separate 2nd-pass executions, three of
them exactly the isolated/serial repeats this pass's brief required. The 1st pass's LOW
(`WeightTrendChart` row-key collision) is also confirmed fixed. But this pass found a **new** real
defect while specifically checking that the fix didn't regress `StaffShell`'s keyboard operability:
the sidebar collapse/expand toggle loses keyboard focus to `<body>` on its first activation per
navigation (not present before `dbba453`), reproduced directly and given a permanent, watched-failing
regression test. That is why STATUS is FAIL again, for a different reason than the 1st pass.

## Environment

- **Node** `v24.21.0`, **npm** `11.19.0` (HO-CAT-002 §20.1 is resolved in this worktree).
- **Build tools**: `npm run typecheck` (tsc 5.9.3, `--noEmit`) — clean, 0 errors.
  `npm run lint` (ESLint 9.39.5 flat config) — clean, 0 warnings, both before and after the tests
  added at this gate.
- **Unit/component suite**: Vitest 5.0.1 + Testing Library + jsdom. `npm test` — **93/93 passed**
  (87 pre-existing + 6 added at this gate; see §"Cases added at this gate" below).
- **E2E suite**: Playwright 1.63.0, Chromium `chromium-1243` already installed locally
  (`ms-playwright` cache) — no download needed. Two projects: `chromium` (default viewport) and
  `mobile-360` (360×780, the UI-1 floor). `webServer` auto-starts `npm run dev`.
- **Mocks**: `NEXT_PUBLIC_USE_MOCKS=true`, `NEXT_PUBLIC_API_BASE_URL=https://localhost:7099`
  (`.env.local`, unreachable and never actually dialled — MSW intercepts every request). **The
  HO-CAT-001 backend does not exist.** No database, no real API host, no FusionAuth involvement.
  Every case below ran against MSW fixtures only, per HO-CAT-002 §5.1.
- **E2E run methodology, recorded because it matters for how to read the results below**: the
  first `npm run test:e2e` run used Playwright's default `fullyParallel: true` and showed **23
  failing tests**, spread across many unrelated specs (E3, E7, E8, E9, E10, E13, E20, E22, two
  E17 screens, all four E18/E19 breakpoints, …). Given that scale and spread, this was investigated
  before being taken at face value, per this gate's mandate not to fabricate results.
  `tests/e2e/fixtures.ts`'s own doc comment explains a race in this exact app between
  `page.goto` and MSW's service-worker registration; that race is far more likely to be
  exacerbated by many parallel Chromium instances competing for CPU/IO on one machine than to be
  22 independent real defects appearing simultaneously. A second full run with `--workers=1`
  (serialised, no cross-test contention) reproduced **only one** of those 23 failures — see E18
  below — and a third, further-isolated single-spec run (`--project=chromium --workers=1` against
  only `cat-responsive.spec.ts`) reproduced that same one again, on its own, with nothing else
  running. **The 22 other parallel-run failures are recorded as resource-contention flakiness, not
  as findings** — every one of them passed cleanly, twice, once contention was removed. The one
  that survived serialisation and isolation, three times running, is treated as real and is `FAIL`
  below, not `BLOCKED` and not waved through as flake.
- No `npm run build` was run (not in the gate's requested command list); `typecheck`/`lint`/`test`/
  `test:e2e` were.

### Second pass (2026-09-25) — re-execution methodology

- `npm run typecheck` and `npm run lint` — re-run clean (0 errors / 0 warnings), including against
  the one new test file added this pass (`tests/e2e/staff-shell-toggle.spec.ts`, see below).
- `npm test` — **93/93 passed**, unchanged count (`dbba453` only touched `staff-shell.tsx` and a
  spec comment; `b19928e` extended an existing test rather than adding a new file). No console
  warnings printed, confirming `b19928e`'s fix directly (the 1st pass's LOW finding reproduced by
  an *unprompted* "same key" warning from Vitest; that warning no longer fires).
- **E18 in isolation, three separate runs**, as the gate brief required: `--project=chromium
  --workers=1 tests/e2e/cat-responsive.spec.ts` alone (4/4 passed), then two full
  `--workers=1 tests/e2e/cat-responsive.spec.ts` runs across both projects (8/8 passed, each run).
  **All three runs passed at every breakpoint, including 360px, on both projects** — a reversal of
  the 1st pass's three-for-three 360px failure.
- **Full serial suite**, `npx playwright test --workers=1` (no new spec yet): **82/82 passed**,
  7.6 minutes — the same 82 tests the 1st pass ran, all still green, including E18/E19.
- **Full default-parallel suite**, `npx playwright test` (`fullyParallel: true`, the project
  default): **65 passed, 17 failed**, 2.3 minutes. Contention flakiness reappeared, as it did on the
  1st pass, but with a materially different (and smaller) footprint: spread across `cat-a11y`
  (one screen), `cat-parasite-control`, `cat-profile-header` (E3/E4), `cat-transfer` (E13),
  `cat-vaccinations` (E6–E10), `cat-weights` (E5) — all `element(s) not found` / `Test timeout of
  30000ms exceeded` failures consistent with dev-mode route-compilation and hydration losing a race
  under many-Chromium-instance CPU contention, exactly the mechanism `tests/e2e/fixtures.ts` and the
  1st pass's own note describe. **Critically, `cat-responsive.spec.ts` (E18/E19) is not among the 17
  failures** — it passed cleanly on every one of its 4 sub-tests × 2 projects, even under
  contention, which independent of the three isolated/serial runs above supports that the 360px fix
  is real and not merely an artefact of low contention. Per this pass's own brief: this
  parallel-run flakiness is recorded here, not as a finding, and does not fail the gate, since every
  serial/isolated run is clean.
- **`StaffShell`'s own tests**: none exist as a dedicated file — this was true before `dbba453` and
  remains true after it (see "Gaps left deliberately", second pass, below). Its only existing
  coverage is incidental: `nav-registry.test.ts` (registry shape only, doesn't render the
  component), `cat-a11y.spec.ts`'s per-page axe scans (static DOM per load, 34/34 passed in the
  clean serial run, confirmed again this pass), and `cat-responsive.spec.ts` (`scrollWidth`/
  `clientWidth` only). `npm test`'s clean 93/93 run confirms `nav-registry.test.ts` specifically
  passed, watched directly in that run's output.
- **Sidebar toggle keyboard/aria behaviour, both widths** — checked manually first via a temporary,
  uncommitted Playwright script (360px and 1440px), which surfaced a real regression; then
  committed as a permanent regression case, `tests/e2e/staff-shell-toggle.spec.ts` (2 tests × 2
  projects = 4). **This changes the full serial suite's final total from the 82 tests above to
  86**: re-run in full with the new spec present, `npx playwright test --workers=1` — **82 passed,
  4 failed, 7.5 minutes**; the 4 failures are exactly and only the 4 new
  `staff-shell-toggle.spec.ts` executions (both widths × both projects), watched failing on every
  one, with identical `document.activeElement === document.body` evidence each time. See the
  "Finding" after the end-to-end table.

### Third pass (2026-09-25, commit `7304f85`) — re-execution methodology

- `npm run typecheck` and `npm run lint` — re-run clean (0 errors / 0 warnings).
- `npm test` — **97/97 passed** (93 + the new `tests/unit/staff-shell.test.tsx`, 4 tests). Run in
  isolation too (`npx vitest run tests/unit/staff-shell.test.tsx`) — 4/4 passed on its own.
- **Decoration check on `staff-shell.test.tsx`**, per this pass's specific brief: confirmed the
  2nd-pass content of `staff-shell.tsx` is byte-identical between `dbba453` and `f478f41`
  (`git diff --quiet f478f41 dbba453 -- web/components/common/staff-shell.tsx` — no output), so
  checking out either commit's copy exercises the exact pre-fix component. Did so
  (`git checkout f478f41 -- web/components/common/staff-shell.tsx`, working tree otherwise clean),
  re-ran the same command — **3 of 4 tests failed**: `keeps the same focused element … (below sm)`
  and `… (sm and up)` both threw inside the shared `toggles()` helper (`expected […] to have a
  length of 2 but got 1` — the pre-fix component collapses back to a single `<Button>` the moment
  `override` is non-null, so only one matching role remains after the first click), and
  `applies a manual choice to both controls` failed on `expect(wide).toHaveAttribute('aria-expanded',
  'false')` (`Received: "true"` — clicking the wide control never touches the narrow one once they've
  collapsed into one shared element). Only the 4th test (initial render, before any click) passed
  against the old code. This is watched, not inferred: the test fails for the *mechanism* of the bug
  (element identity/count, not merely a focus assertion jsdom might not reproduce faithfully), so it
  is not decoration. Restored `staff-shell.tsx` via `git checkout HEAD --`, confirmed clean
  (`git status --short`), re-ran once more — 4/4 passed again. Working tree was clean before and
  after; nothing from this check was committed.
- **E18 in isolation, three runs**, as this pass's brief required. The **first two** attempts
  (`--project=chromium --workers=1 tests/e2e/cat-responsive.spec.ts` against a freshly-started
  `webServer`, then the same command again against a separately, manually-started `npm run dev`
  with only one of its seven `SCREENS` routes pre-warmed) both failed — **but not on a `scrollWidth`
  overflow**: `page.waitForLoadState('networkidle'): Test timeout of 30000ms exceeded` at 360px only
  (768/1024/1440 passed both times, 3/4 and 3/4). 360px runs first in `BREAKPOINTS`, and its single
  30s test timeout has to cover cold-compiling all seven distinct `SCREENS` routes in Next dev mode
  the first time anything visits them on a given server process — confirmed directly: curling all
  seven routes once (0.19–0.23s each, already-fast, since the two failed attempts had already forced
  compilation of whichever routes they reached before timing out) removed the effect entirely. A
  **third** run, `--project=chromium --workers=1` against the now-fully-warm server: **4/4 passed**,
  54.2s. Two further `--workers=1` runs across both projects on the same warm server: **8/8 passed**
  (57.0s), then **8/8 passed** again (1.2m) — the three required isolated/serial executions, all
  clean, 360px never overflowing once it did. **This is recorded as a methodology note, not a
  finding**: it is a dev-tooling cold-start artefact (a `waitForLoadState` timeout on first
  compilation), categorically different from the `scrollWidth`-overflow defect this row exists to
  catch, and it did not recur once the server was warm — consistent with, though a different
  mechanism from, the parallel-contention flakiness the 1st/2nd passes already documented.
- **Full serial suite**, `npx playwright test --workers=1`, run twice this pass: once *before* the
  new finding's regression spec existed (**86/86 passed**, 9.0 minutes — confirms the 2nd pass's
  `staff-shell-toggle.spec.ts` finding is fixed, watched clean on all 4 of its executions, alongside
  everything else, including `cat-responsive.spec.ts`), and once *after* adding
  `tests/e2e/staff-shell-manual-expand-overflow.spec.ts` (below): **86 passed, 8 failed**, 5.6
  minutes — the 8 failures are exactly and only the new spec's 4 screens × 2 projects, each showing
  identical `384 > 361` (`clientWidth + 1`) evidence. Nothing else regressed between the two runs.
- **Full default-parallel suite**, `npx playwright test` (optional per this pass's brief, run for
  consistency with the prior two passes): **81 passed, 13 failed**, 1.4 minutes. 8 of the 13 are the
  same new finding, present under contention exactly as under isolation (the same `384`/`361`
  numbers) — this is what distinguishes it as a real, deterministic defect rather than flake. The
  other 5 (`cat-vaccinations` E7–E10 on `mobile-360`, `cat-weights` E5 on `mobile-360`) are
  `Test timeout of 30000ms exceeded` waiting on form controls, consistent in kind with the 1st/2nd
  passes' documented parallel-contention flakiness — a different, smaller-footprint set of screens
  than either prior pass hit, which is itself consistent with contention flakiness (its exact
  footprint isn't reproducible run to run) rather than a new finding. Recorded here, not as a
  finding, since the full serial run above is clean apart from the one real, reproduced-everywhere
  defect.
- **Real-browser check of the manual toggle's post-click behaviour**, per this pass's specific
  brief: a temporary, uncommitted Playwright script at 360px and 1440px confirmed both halves of the
  brief directly — the toggle button itself stays visible and keeps focus after the click that
  produced the manual choice (`toBeVisible()`/`toBeFocused()` both watched pass), and the sidebar's
  width does follow that choice at *each* width it's clicked at (confirmed via `expect.poll` on
  `boundingBox().width`, once the check was corrected to wait out the `transition-[width]` CSS
  animation rather than read `boundingBox()` immediately after the click — an artefact of the ad hoc
  script, not of production code: the `class` attribute already held the correct new width class the
  instant after the click in every observation, confirmed by reading it directly). **Extending that
  same script to also measure `document.documentElement.scrollWidth` after the settled click is what
  surfaced the new finding below**: at 360px specifically, the settled, correctly-applied `w-60`
  class produces 24–28px of real horizontal overflow, on every one of four representative
  `/staff/**` screens tried. Not present at 1440px. Script removed before committing; the
  observation is now the permanent `staff-shell-manual-expand-overflow.spec.ts`, watched failing
  8/8 in both full-suite runs above.

### Fourth pass (2026-09-25, commit `7f758b3`) — re-execution methodology

- `npm run typecheck` and `npm run lint` — re-run clean (0 errors / 0 warnings), including against
  the one new spec added this pass. `npm run format:check` (Prettier) — also clean.
- `npm test` — **97/97 passed**, unchanged count (`7f758b3` only touched `staff-shell.tsx` and an
  e2e spec's doc comment, no unit-test-relevant file).
- **Decoration check on `staff-shell-manual-expand-overflow.spec.ts`**, per this pass's specific
  brief (step 1): confirmed the working tree was clean, then restored `staff-shell.tsx` to its exact
  pre-fix content (`git checkout 6baf0bb -- web/components/common/staff-shell.tsx` — `6baf0bb` is
  the 3rd pass's own gate-recording commit, immediately before `7304f85`/`7f758b3`; diffed against
  the current file first to confirm the only difference *is* the fix under test, nothing else drifted
  in between). Re-ran the regression spec against that pre-fix content
  (`--project=chromium --workers=1 tests/e2e/staff-shell-manual-expand-overflow.spec.ts`) — **4/4
  failed**, identical `384 > 361` evidence to the 3rd pass's own recorded numbers, on every one of the
  four representative screens. This is watched, not inferred, and confirms the spec exercises the
  real mechanism, not a decoration. Restored the fix (`git checkout HEAD --
  web/components/common/staff-shell.tsx`), confirmed `git diff --quiet HEAD --
  web/components/common/staff-shell.tsx` (clean), and re-ran once more — **4/4 passed**. Nothing from
  this check was committed.
- **E18 in isolation, three runs**, as this pass's brief required, against an already-warm server
  (no repeat of the 3rd pass's cold-start artefact): `--project=chromium --workers=1
  tests/e2e/cat-responsive.spec.ts` alone — **4/4 passed** (59.0s); `--workers=1` across both
  projects — **8/8 passed** (1.6m); the same again — **8/8 passed** (1.6m). All three clean, 360px
  never overflowing.
- **Real-browser, keyboard-only check of the 360px overlay**, per this pass's specific brief (step
  2): a temporary, uncommitted Playwright script on `/staff/cats` at 360px tabbed to the visible
  "Expand menu" button (`reachedToggle` watched `true` within 15 tabs), pressed **Enter** (keyboard,
  not `.click()`), and polled the aside's `boundingBox().width` past 200px to confirm the expand
  settled. Observed directly: `aside`'s computed `position` becomes `fixed` (confirms the overlay
  mechanism, not merely the class list); `document.activeElement` immediately after Enter is the
  **same** "Collapse menu" button (`aria-label` flips in place, same element — no remount, matching
  the 2nd/3rd-pass fix's element-identity design), `toBeVisible()` true, and its bounding box (`x:
  195, width: 32`) is fully within the 360px viewport — **visible, reachable, and focused**, all
  three watched directly, not inferred. Tabbing onward from there reached the overlay's own two nav
  links ("Cats", "Vaccine catalogue"), both with bounding boxes fully inside the 360px viewport and
  non-zero size — **tabbable and not clipped**. Focusing and pressing Enter on the (now) "Collapse
  menu" button returned the aside to `69.5px` wide (the 64px rail) with zero measured
  `scrollWidth`-over-`clientWidth` overflow (`360`/`360`) — **collapses cleanly, no overflow**. All
  three of this pass's specific 360px checks are confirmed, watched directly.
- **1440px unchanged, per this pass's specific brief**: the same script at 1440px measured the
  aside's `position` as `sticky` (never `fixed` — `max-sm:` only applies below 640px) and its width
  as `240` at the default state, `69.5` once collapsed, and `239` once re-expanded — in-flow
  `w-60`/`w-16` exactly as before this fix, confirmed directly rather than assumed from reading the
  Tailwind class list alone. No overflow at 1440px either (`1440`/`1440`). Script removed before
  committing.
- **Extending the same "not clipped" check beyond the overlay's own nav links, to nearby
  main-content controls, is what surfaced the new finding below**: `document.elementFromPoint` at a
  point inside the "Register a cat" button's own left edge (`/staff/cats`, 360px, sidebar expanded)
  resolved to the sidebar's "Cats" link, not the button — confirmed with a screenshot
  (`test-results/_tmp-overlay-360.png`, not committed) showing the button's label visibly reading
  "egister a cat", its "R" hidden under the sidebar. A follow-up sweep across the same four
  representative screens (`/staff/cats`, the profile, `/medical`, `/weights`) at 360px, checking
  every `main` element whose left edge falls inside the aside's expanded width for whether
  `elementFromPoint` on it resolves inside `<aside>`, found this reproduced deterministically (twice
  more, identical `x ≈ 213` numbers each run) on `/staff/cats` (the "Register a cat" button and a
  filter checkbox), and, transiently, on the profile page only during the already-documented MSW
  startup-race error state (not on normal load — re-checked by retrying past that race and finding no
  occlusion once the page actually loaded) — not on `/medical` or `/weights` within the initial
  viewport. Not present at 1440px (the aside is never `fixed` there). The deterministic `/staff/cats`
  reproduction is now the permanent `staff-shell-manual-expand-occludes-content.spec.ts`, watched
  failing 2/2 (both projects) against the current code, in isolation, in the full serial run, and in
  the full default-parallel run below.
- **Full serial suite**, `npx playwright test --workers=1`: **94 passed, 2 failed**, 6.4 minutes —
  the 2 failures are exactly and only the new spec's 2 executions (chromium + mobile-360), identical
  evidence each time (`document.elementFromPoint` inside `<aside>`). This also confirms the 3rd
  pass's fix: `staff-shell-manual-expand-overflow.spec.ts` (8/8) and `staff-shell-toggle.spec.ts`
  (4/4) both pass clean alongside everything else. 94 is exactly the integrator's reported 94/94 plus
  the 2 new (currently failing) cases this gate added — 96 total.
- **Full default-parallel suite**, `npx playwright test` (optional per this pass's brief, run for
  consistency with the prior passes): **82 passed, 14 failed**, 1.6 minutes. 2 of the 14 are the new
  finding, present identically under contention (same mechanism, not contention-dependent) — this is
  what marks it real rather than flake. The other 12 (`cat-a11y` one screen, `cat-profile-header`
  E3/E4 on both projects, `cat-vaccinations` E6–E9, `cat-transfer` E13) are `element(s) not found` /
  `Test timeout` failures, consistent in kind and rough scale with the 1st/2nd/3rd passes' documented
  parallel-contention flakiness — a different footprint each time, itself consistent with contention
  rather than a repeatable defect (the full serial run above is clean apart from the one real,
  reproduced-everywhere new finding). Recorded here, not as a finding.

### Fifth pass (2026-09-25, commit `8379121`) — re-execution methodology

- `npm run typecheck`, `npm run lint`, `npm run format:check` — all re-run clean (0 errors / 0
  warnings / Prettier clean), including against the one new spec added this pass.
- `npm test` — **98/98 passed** (97 + the fix commit's own new `Escape` case in
  `tests/unit/staff-shell.test.tsx`, now 5 tests in that file). Matches the integrator's reported
  98/98 exactly.
- **Judging the rewritten spec, per this pass's specific brief (step 1)**: `8379121` rewrote
  `staff-shell-manual-expand-occludes-content.spec.ts`'s assertions (its doc comment explains why —
  see the header note above) rather than only its comment, unlike the 3rd→4th pass transition where
  only `staff-shell.tsx` and a comment changed. Confirmed the working tree was clean, restored
  `staff-shell.tsx` to its exact pre-fix (`ee54879`) content
  (`git diff ee54879 HEAD -- web/components/common/staff-shell.tsx` showed only the scrim/`Escape`
  diff described in the header note, nothing else drifted), and re-ran the rewritten spec against it
  (`--project=chromium --workers=1`) — **2 of 3 failed**: `toBeVisible()` timeout on the scrim
  locator (`element(s) not found` — no scrim exists yet) and `toBeFocused()` timeout on the toggle
  after `Escape` (no `Escape` handler exists yet to leave focus in place for). The third case ("no
  scrim at 1440px") **passed against the pre-fix code too** — expected, since neither version has a
  scrim at 1440px, so this case verifies a real invariant but doesn't itself distinguish fixed from
  unfixed. This fails on the real mechanism (missing DOM node / missing event listener), not a
  loosely-worded assertion, so 2 of the 3 cases are not decoration. Restored the fix
  (`git checkout HEAD -- web/components/common/staff-shell.tsx`), confirmed `git diff --quiet HEAD --`
  clean, re-ran — **3/3 passed** again. Nothing from this check was committed.
- **Real-browser check of the drawer contract, per this pass's specific brief (step 2)**: a temporary,
  uncommitted Playwright script at 360px on `/staff/cats` opened the drawer (keyboard, not click) and
  sampled `document.elementFromPoint` at three points across the full width of "Register a cat" (left
  edge, centre, right edge). **All three resolve to either the `<aside>` (the ~27px sliver still under
  the drawer) or the scrim (the rest)** — confirmed directly: no point on the button is
  mouse-clickable while the drawer is open. This is the pointer-input half of the 4th pass's finding,
  now genuinely fixed.
- **Then the question this pass's brief posed directly — "while the drawer is open, can Tab reach
  anything behind the scrim?"** The same script tabbed from the (keyboard-)opened toggle: the first
  two Tabs land on the drawer's own nav links ("Cats", "Vaccine catalogue"), exactly as the 4th pass
  observed; the **third Tab lands on "Register a cat" itself**, `document.activeElement.textContent`
  confirmed directly — while the scrim is still visible and the toggle's `aria-expanded` is still
  `"true"`. Pressing `Enter` there (not a mouse click) fires the button's native click handler, and
  **the page navigates to `/staff/cats/new`**, confirmed via `page.url()` after allowing the
  client-side navigation to settle. Reproduced identically across three additional isolated
  executions (chromium) plus one run across both projects, six observations total, all identical.
  This is a real, deterministic focus/keyboard gap, not a coordinate-sampling or timing artefact: the
  script's own initial (incorrect) assertion form, `expect(page).not.toHaveURL(...)`, passed
  spuriously because Playwright's auto-retrying `not` matcher only waits for a mismatch to *appear*,
  not to persist — it can be satisfied in the instant before `router.push` resolves. Correcting this
  to `waitForURL(...).catch(() => {})` then reading `page.url()` directly removed that false pass and
  reproduced the real navigation. That correction is reflected in the permanent test below, not left
  as a latent flaw. Script removed before committing; the observation is now the permanent
  `tests/e2e/staff-shell-drawer-not-focus-trapped.spec.ts`.
- **E18 in isolation, three runs**, as this pass's brief required, against an already-warm server:
  `--project=chromium --workers=1 tests/e2e/cat-responsive.spec.ts` alone — **4/4 passed** (40.6s);
  `--workers=1` across both projects — **8/8 passed** (1.2m); the same again — **8/8 passed** (1.2m).
  All three clean, 360px never overflowing.
- **Full serial suite**, `npx playwright test --workers=1`: **102 total, 100 passed, 2 failed**, 5.0
  minutes — the 2 failures are exactly and only the new spec's 2 executions (chromium + mobile-360),
  identical evidence each time (`page.url()` containing `/staff/cats/new`). This also confirms the
  4th pass's fix for pointer input: the rewritten `staff-shell-manual-expand-occludes-content.spec.ts`
  (3 tests × 2 projects = 6/6) and `staff-shell-manual-expand-overflow.spec.ts` (8/8) and
  `staff-shell-toggle.spec.ts` (4/4) all pass clean alongside everything else. Arithmetic ties to the
  4th pass exactly: its 96-total run (94 passed + the 2 then-failing occludes-content executions)
  minus those 2, plus the rewrite's 6 executions (now all passing), plus this pass's own 1 new case ×
  2 projects (currently failing) = 96 − 2 + 6 + 2 = **102 total, 100 passed, 2 failed** — exactly what
  was observed.
- **Full default-parallel suite**, `npx playwright test` (optional per this pass's brief, run for
  consistency with the prior passes): **102 total, 100 passed, 2 failed**, 1.3 minutes — identically
  the same 2 failures as the serial run, with **no other contention-flake failures this run**, unlike
  every prior pass's default-parallel run. That the one real finding is the *only* thing that fails,
  identically, under both serial and parallel execution is itself the strongest evidence yet in this
  gate's history that a given failure is real rather than flake.

### Sixth pass (2026-09-25, commit `bb24673`) — re-execution methodology

- `npm run typecheck`, `npm run lint`, `npm run format:check` — all re-run clean (0 errors / 0
  warnings / Prettier clean), including against the two new specs added this pass (Prettier flagged
  their initial formatting once, fixed with `npx prettier --write`, re-checked clean).
- `npm test` — **100/100 passed**, unchanged count. Nothing this pass touched a file U1–U33 exercise
  (`bb24673` and this gate's own additions are all in `tests/e2e/`).
- **This pass's step 1: a full accessibility/behaviour audit of the drawer**, not another
  single-finding chase, covering every item the brief named. Two ways of getting a wrong answer were
  found and corrected before being relied on, both disclosed here rather than silently avoided:
  - **Playwright's own `page.accessibility.snapshot()` no longer exists in Playwright 1.63** (removed
    API) and its replacement, `locator.ariaSnapshot()`, **misleadingly still listed `<main>`'s full
    content** (including "Register a cat") while the drawer was open — an artefact of that
    convenience API's own reduced accessibility computation, not of the real browser. Cross-checked
    directly against Chrome's own accessibility tree via the DevTools Protocol
    (`Accessibility.getFullAXTree`, a real CDP session, not a Playwright wrapper): no "Register a cat"
    node and no `main`-role landmark exist in it at all while the drawer is open — genuinely absent,
    confirming the fix at the level that actually matters for a screen reader, and confirming
    `ariaSnapshot()`'s reading was the wrong one, not the fix.
  - **A one-off local production build (`npm run build && npm run start`) was tried, solely to confirm
    whether the `<nextjs-portal>` dev-tool element observed during the Tab-cycling sweep (see below)
    exists outside development** — it does not (confirmed directly: `document.querySelector('nextjs-portal')`
    is `null` under `next start`). That same local production build separately showed the sidebar's
    Tailwind width utilities not applying at all and its toggle button's `onClick` not updating state,
    while every other interaction on the same page (typing in a form, `<Link>` navigation) worked
    normally — inconsistent with five passes of solid `next dev` evidence and not reproducible via any
    methodology this gate or any prior pass has used. Judged an artefact of this one ad hoc local
    build (this gate's mandate and every prior pass test only against `next dev`, never `next
    build`/`next start`), not a property of the reviewed code. **Not relied on and not reported as a
    finding** — disclosed here only so the dead end isn't silently hidden.
  - **Tab/Shift+Tab cycling**: confirmed Tab never reaches `main`/`header` while the drawer is open
    (consistent with the CDP check above). Run far enough (8 Tabs from the toggle), it transits
    `document.body` and, in dev mode only, the `<nextjs-portal>` element just ruled out for
    production, before landing back on the drawer's own first focusable element ("PurrCare"). The
    same holds Shift+Tab backward. Background content is never reached either direction — recorded as
    a NOTE (not a disciplined APG loop, but the actual named risk in §8.5 doesn't materialise), not a
    requirement violation — see "Gaps left deliberately".
  - **`Escape` and a scrim click, after Tab has moved focus into the drawer's own nav links**: neither
    returns focus to the toggle — see the new finding above. The existing `Escape`/scrim-click cases
    never caught this because they all close immediately after opening, before focus has moved.
  - **A live toast while the drawer is open**: reachable via Tab — see the other new finding above.
  - **Resizing across 640px, both directions, while the drawer/expanded sidebar is open**: narrow→wide
    correctly turns `inert` off and the `<aside>` back to `position: sticky`; wide→narrow (with the
    sidebar already manually expanded in-flow) correctly turns `inert` on and the scrim visible.
    Confirmed directly via `getComputedStyle`/`closest('[inert]')` checks after each resize, not
    inferred from the class list alone.
  - **768/1024/1440px, and the 640px boundary itself**: expanding/collapsing the sidebar at all three
    widths, and at exactly 640px, never applies `inert` and never makes `<aside>` anything but
    `position: sticky` — unchanged in-flow behaviour, confirmed directly. 640px itself is correctly
    treated as "wide" (matches Tailwind's `sm:` breakpoint, which applies from 640px inclusive).
  - **The skip link outside the drawer-open state**: not `inert`, focusable, and the first Tab target
    on a fresh 360px load — confirmed directly, unaffected by the fix.
  - **The `mobile-360` Playwright project**: confirmed it is a viewport-size-only project
    (`{ viewport: { width: 360, height: 780 } }`, no `devices[...]` descriptor, no `hasTouch`) —
    unchanged from every earlier pass's methodology. "Touch" coverage in this suite has always meant
    narrow-viewport mouse/keyboard testing, not touch-event emulation. Not a new gap this pass
    introduces; recorded since the brief asked this pass to specifically cover it.
- **Two new permanent regression tests**, both watched failing on the real mechanism before being
  relied on (not decoration): `tests/e2e/staff-shell-drawer-focus-not-returned-to-trigger.spec.ts`
  (2 cases) and `tests/e2e/staff-shell-drawer-toast-not-inert.spec.ts` (1 case) — 3 new cases × 2
  projects = 6 executions, all 6 watched fail identically in isolation
  (`--project=chromium --workers=1`, then both projects together) before being included in the full
  suite runs below.
- **E18 in isolation, three runs, as required.** The first attempt, against a server `webServer`
  auto-started fresh for that one invocation, hit the same dev-mode cold-start artefact the 3rd pass
  first documented (`waitForLoadState('networkidle')` timeout at 360px only, the first breakpoint
  tried, before any route had been compiled) — **not** a `scrollWidth` overflow, and not counted as
  one of the three required runs. Started `npm run dev` manually instead, curled all seven `SCREENS`
  routes once to force compilation, then ran against that now-warm, persistent server: `--project=chromium
  --workers=1 tests/e2e/cat-responsive.spec.ts` alone — **4/4 passed** (51.3s); `--workers=1` across
  both projects — **8/8 passed** (1.6m); the same again — **8/8 passed** (1.6m). All three clean,
  360px never overflowing once warm — consistent with every prior pass's own note that this is a
  dev-tooling artefact, categorically different from the overflow defect this row exists to catch.
- **Full serial suite**, `npx playwright test --workers=1`: **108 total, 101 passed, 7 failed**, 10.7
  minutes. Six of the seven failures are exactly and only this pass's 3 new cases × 2 projects,
  identical evidence each time. The seventh — `[mobile-360] cat-profile-header.spec.ts` E3 — did
  **not** reproduce on an immediate, isolated re-run of that same spec (`2/2 passed`, 1.7 minutes,
  nothing else running), which is this gate's own established standard for judging a single anomalous
  failure as contention flake rather than a new finding. Recorded here, not as a finding.
- **Full default-parallel suite**, `npx playwright test` (optional per this pass's brief, run for
  consistency with the prior passes). The **first** attempt was run concurrently with the E18
  cold-start retry above — a methodology mistake, disclosed rather than hidden — and showed inflated
  contention (32 failed/76 passed) including three of E18's own sub-tests failing on the shared,
  contended server. **Discarded, not used as evidence.** Re-run cleanly, nothing else executing:
  **108 total, 83 passed, 25 failed**, 2.4 minutes. Six of the 25 are this pass's own new cases (as
  expected); the other 19 are spread across many unrelated, previously-green specs (E3, E5, E6, E7,
  E9, E10, E13, E17, E22, the parasite-control extra case, and — once — one of R13's own
  `staff-shell-manual-expand-occludes-content.spec.ts` cases) in the same scattered, non-reproducing
  pattern this document has documented as resource-contention flakiness on every prior pass's
  default-parallel run (2nd pass: 17, 3rd: 5/13, 4th: 12, this pass: 19) — none of them reproduced in
  the clean serial run above, which is what this gate treats as the reliable signal. Recorded here,
  not as findings.

### Seventh pass (2026-09-25, commits `92a33e5` + `1bf95d8` + `b209ef7` + `c813696`) — re-execution methodology

- `npm run typecheck`, `npm run lint`, `npm run format:check` — all re-run clean (0 errors / 0
  warnings / Prettier clean).
- `npm test` — **101/101 passed** (up from the 6th pass's 100 — `staff-shell.test.tsx` was rewritten
  for the Radix rebuild, net +1 test: 8 tests now, covering the sm+ toggle's unchanged focus-stability
  guarantee and the below-`sm` sheet's own contract, rather than the old drawer's `override`/`inert`/
  scrim/`Escape`-listener internals, which no longer exist to test).
- **Judging the rewritten e2e specs, per this pass's specific brief (step 1)**: `b209ef7` rewrote
  five staff-shell specs and added `tests/e2e/staff-shell-sheet-helpers.ts`. Read every rewritten
  file's own top comment (each explains what changed and why the contract still holds) and confirmed
  by inspection that no assertion was loosened — `staff-shell-manual-expand-overflow.spec.ts` and
  `staff-shell-manual-expand-occludes-content.spec.ts` (whose premise, an in-flow aside that widens
  below `sm`, is now structurally impossible) were rewritten to the equivalent contract against the
  sheet (no overflow while open; nothing behind its overlay is mouse-reachable; both ways out restore
  the page), not relaxed.
  - **The specific concern this pass's brief named directly — could the helper's attribute-based
    locators (`[aria-label="Expand menu"]` instead of `getByRole`) hide a real accessible-name
    regression on the trigger?** Checked directly, not assumed: with the sheet **closed**,
    `page.getByRole('button', { name: 'Expand menu' })` finds the trigger — confirmed both by a
    dedicated audit check and by `staff-shell-drawer-toast-not-inert.spec.ts`'s own setup step, which
    already uses `getByRole` for this and passes. The attribute selector is only ever needed once the
    sheet is **open**, which is exactly when Radix's `modal` behaviour correctly marks the trigger
    (along with the rest of the document) `aria-hidden="true"` — a `getByRole` query is *supposed* to
    fail to find it then, the same as a screen reader would. Not a workaround for a lost accessible
    name.
  - **Watching the two 6th-pass regression specs fail against the pre-rebuild code, then pass against
    the rebuild, per this pass's specific brief (step 1)**: restored both `staff-shell.tsx` and
    `dialog.tsx` to their exact pre-rebuild (`9464acd`) content
    (`git checkout 9464acd -- web/components/common/staff-shell.tsx web/components/ui/dialog.tsx`) and
    re-ran `staff-shell-drawer-focus-not-returned-to-trigger.spec.ts` (2 cases) and
    `staff-shell-drawer-toast-not-inert.spec.ts` (1 case) against it
    (`--project=chromium --workers=1`) — **3/3 failed**, every one on
    `expect(locator).toBeVisible()` for `getByRole('dialog')` timing out (`element(s) not found` — the
    old hand-rolled drawer never rendered anything with `role="dialog"` at all, so
    `openSheetAt360`/the toast spec's own setup step can't even get past opening it), not a
    loosely-worded assertion. Restored the rebuild
    (`git checkout HEAD -- web/components/common/staff-shell.tsx web/components/ui/dialog.tsx`,
    confirmed `git status --short` clean) and re-ran — **3/3 passed**. Working tree was clean before
    and after; nothing from this check was committed.
- **This pass's step 2: one complete audit of the new sheet against UI-1, UI-6, §8.5 and
  NFR-USA-***, covering every item the brief named, against a manually-started, curl-warmed server
  (avoiding the documented dev-mode cold-start artefact):
  - **Focus on open**: focusing the trigger and pressing `Enter` moves `document.activeElement`
    inside `[role="dialog"]` — confirmed directly (Radix's own `onMountAutoFocus` behaviour, not
    inferred from the dialog merely being visible).
  - **Trap, both directions**: 12 consecutive `Tab` presses from the moment the sheet opens cycle
    only through its own three focusable elements ("Cats", "Vaccine catalogue", "Close") and never
    once land in `main`/`header`; 12 consecutive `Shift+Tab` presses do the same backward — both
    watched directly via `document.activeElement.closest('main, header')` after every single
    keypress, not sampled.
  - **`Escape`, an overlay click, and the close button, each returning focus to the trigger**: all
    three watched directly. `Escape` and an overlay click are also each of this file's own two
    permanent regression cases (`staff-shell-drawer-focus-not-returned-to-trigger.spec.ts`); the close
    button (`Close` — Radix's own `<DialogPrimitive.Close>`) was checked ad hoc this pass and returns
    focus to the trigger identically, via the same Radix `onCloseAutoFocus` mechanism as the other two
    paths (`components/ui/dialog.tsx` doesn't special-case any one close path).
  - **A nav-link click closing the sheet and navigating**: clicking "Vaccine catalogue" inside the
    open sheet closes it (`getByRole('dialog')` count 0) and navigates to `/staff/vaccines` —
    confirmed directly via `page.url()`, not merely that the dialog closed.
  - **Screen-reader exposure**: a real Chrome DevTools Protocol `Accessibility.getFullAXTree` query
    while the sheet is open finds no "Register a cat" node anywhere in the tree. Tracing the `main`
    landmark's own subtree in that same query found every one of its rendered content nodes
    `ignored: true` except a `status`-role live region showing "Loading…" (an aria-live region Radix's
    `hideOthers` deliberately excludes from hiding, so it can still announce) — confirmed by direct DOM
    inspection too: `<aside>`, `<header>`, and the skip link each carry `aria-hidden="true"` while the
    sheet is open (and lose it again on close), while `<main>`'s own tag does not, but every one of
    *its* direct children individually does. Functionally equivalent (nothing inside is reachable or
    exposed) but recorded as a NOTE, not a PASS without qualification — see the gate file.
  - **Resizing across 640px while open, both directions**: narrowed from a default 900px-tall/360px
    session down and back up while the sheet was open, and separately opened it, then widened past
    640px — the sheet auto-closes on crossing to `sm`+ (`StaffShell`'s own `matchMedia` listener,
    registered only while `sheetOpen`) rather than stranding the user in a nav-only modal with the
    same links now sitting, uselessly hidden, in the in-flow sidebar behind it; narrowing back
    introduces no stray dialog state. Confirmed via `getByRole('dialog')` counts after each resize, not
    inferred from the class list.
  - **768/1024/1440px unchanged**: at all three widths, no `role="dialog"` ever appears, `<aside>` is
    always `position: sticky` (never `fixed`), and the sm+ collapse/expand toggle works exactly as
    before — confirmed directly at each width, not assumed from the unchanged source diff.
  - **The skip link**: unaffected outside the drawer-open state — first `Tab` target on a fresh 360px
    load, not `aria-hidden` — confirmed directly.
  - **Toasts**: covered structurally, not by discovery — see `staff-shell-drawer-toast-not-inert.spec.ts`
    below; Radix's `FocusScope`/`hideOthers` trap Tab and hide the rest of the document regardless of
    where `<Toaster />` portals to, so this is not a per-sibling fix that could miss a *future* portal
    either, unlike the 6th pass's `inert`-on-hand-picked-siblings approach.
  - **The existing center-docked dialogs not regressed**: `92a33e5`'s `variant` prop leaves `'center'`
    (the default, used by `ConfirmDialog` and everything built on it) byte-for-byte in its rendered
    output — confirmed by reading the diff directly (only the `sheet-left` branch is new). Re-ran
    `tests/unit/confirm-dialog.test.tsx`, `tests/unit/cat/mark-deceased-dialog.test.tsx`,
    `tests/unit/cat/transfer-ownership-dialog.test.tsx`, `tests/unit/cat/high-risk-flag-dialog.test.tsx`
    and `tests/unit/staff-shell.test.tsx` together — **17/17 passed** — and the e2e specs that exercise
    them end-to-end (E10, E11/E12, E13, the parasite-control extra case) — **7/7 passed**.
- **Clean prod build, per this pass's specific brief (step 3)**: `rm -rf .next` then `npm run build`
  — compiled clean (0 errors, 0 type errors, all 9 static + 10 dynamic routes generated). `npm run
  start` against the fresh `.next/`, checked directly rather than assumed: at 360px, `<aside>`'s own
  `boundingBox().width` is `64` (the rail, not read from the class list), `scrollWidth`/`clientWidth`
  are both `360` (no overflow), and the trigger opens a real `role="dialog"` sheet that closes on
  `Escape` and returns focus to the trigger, confirmed the same way as under `next dev`; at 1440px,
  clicking "Collapse menu" moves `<aside>` from a measured `240` to `64` (a real, observed width
  change after allowing the `transition-[width]` animation to settle, not a static class read) and its
  `position` stays `sticky`. Console messages during the 360px load were exactly the two
  already-documented, expected MSW-race `ERR_CONNECTION_REFUSED` errors (§5.1) — no hydration
  warnings, no React errors, no `pageerror` events. **This settles the 6th pass's own unresolved
  finding**: that pass's one-off local build showed the sidebar's Tailwind classes not applying and
  its toggle's `onClick` not updating state, but explicitly declined to rely on or report it, since it
  was inconsistent with every other observation; this pass's independent, clean rebuild finds nothing
  wrong to carry forward, and the anomaly is not treated as a property of the reviewed code.
- **E18 in isolation, three runs, as required**, against a manually-started, curl-warmed server (all
  seven representative `SCREENS` routes, plus `/staff/vaccines` and the parasite-control route, curled
  once first to force Next dev-mode compilation, avoiding the documented cold-start artefact):
  `--project=chromium --workers=1 tests/e2e/cat-responsive.spec.ts` alone — **4/4 passed** (43.7s);
  `--workers=1` across both projects — **8/8 passed** (1.6m); the same again — **8/8 passed** (1.6m).
  All three clean, 360px never overflowing.
- **Full serial suite**, `npx playwright test --workers=1`: **108 total, 107 passed, 1 failed**, 8.4
  minutes. The one failure — `[mobile-360] cat-profile-header.spec.ts` E4` (`getByText('None
  recorded')` timing out) — did **not** reproduce on two immediate, isolated re-runs of that same spec
  (`2/2 passed` each time, ~50s each, nothing else running), this gate's own established standard
  (used identically by the 6th pass for its own single anomalous failure) for judging a single
  non-reproducing failure as contention flake rather than a new finding. Recorded here, not as a
  finding. This run also confirms both of the 6th pass's regression specs pass clean in the full suite,
  not only in isolation: `staff-shell-drawer-focus-not-returned-to-trigger.spec.ts` (4/4) and
  `staff-shell-drawer-toast-not-inert.spec.ts` (2/2).
- **Full default-parallel suite**, `npx playwright test` (optional per this pass's brief, run for
  consistency with the prior passes): **108 total, 103 passed, 5 failed**, 1.6 minutes — all 5 on
  `mobile-360` only, all `Test timeout of 30000ms exceeded` on a form control or `getByLabel` never
  appearing (`cat-profile-header.spec.ts` E3 and E4, `cat-vaccinations.spec.ts` E10,
  `cat-weights.spec.ts` E5, and — once — `staff-shell-drawer-toast-not-inert.spec.ts` itself, timing
  out on its own setup step, `getByLabel('Product')`, before ever reaching the sheet). This is the
  same scattered, non-reproducing resource-contention pattern this document has documented on every
  prior pass's default-parallel run (2nd pass: 17, 3rd: 5/13, 4th: 12, 6th: 19, this pass: 5) — none of
  these 5 reproduced in the clean serial run above (where the same toast spec passed 2/2), which is
  what this gate treats as the reliable signal. Recorded here, not as findings.

### Eighth pass (2026-09-25, commits `b83bd2a` + `f392132` + `8b38cc3` + `ccf29b1` + `631904f`) — re-execution methodology

**Targeted delta, per this pass's own brief**: build gates, one full `npm test`, one full serial
Playwright run, and decoration checks limited to the three genuinely new tests — not a full re-audit
of everything the 7th pass already closed (its own full accessibility audit, the 17/17 + 7/7
center-dialog regression check, and the clean prod build are not repeated here, since this delta
doesn't touch any of the mechanisms they covered).

- `npm run typecheck`, `npm run lint`, `npm run format:check` — all re-run clean (0 errors / 0
  warnings / Prettier clean).
- `npm test` — **102/102 passed** (up from the 7th pass's 101 — the one new test is
  `tests/unit/cat/parasite-control-page.test.tsx`, `U34` in this sheet). Spot-re-ran the files this
  delta's non-test commits touch, directly: `tests/unit/cat/medical-profile-form.test.tsx` +
  `tests/unit/cat/high-risk-flag-dialog.test.tsx` (`8b38cc3`'s `FormFieldRow` extraction) — **4/4
  passed**; `tests/unit/cat/weights-page.test.tsx` (`ccf29b1`'s row-id `Map`) — **1/1 passed**;
  `tests/unit/staff-shell.test.tsx` (`f392132`'s route-change effect and `NavSections` unification)
  — **8/8 passed**, same 8 tests as the 7th pass (the delta only added a `vi.mock('next/navigation')`
  for the new `usePathname()` import, not a new test body).
- **Decoration check, `U34`**, per this pass's brief: checked out
  `app/(staff)/staff/cats/[catId]/parasite-control/page.tsx` to its exact pre-change (`c813696`)
  content and re-ran `parasite-control-page.test.tsx` in isolation — **1 of 1 failed**,
  `screen.findByRole('status')` timing out (`element(s) not found` — the pre-change page renders
  only a toast and a static PDF link, no status region at all), the real mechanism, not a
  loosely-worded assertion. Restored (`git checkout HEAD --`, confirmed `git status --short` clean)
  and re-ran — **1/1 passed**. Nothing from this check was committed.
- **Decoration check, the new A11 e2e assertion**, per this pass's brief: checked out both
  `parasite-control/page.tsx` and `mocks/handlers/areas/cat-profiles.ts` (the mock's own `nextDueOn`
  computation is part of the same C10 amendment) to their exact pre-change (`c813696`) content and
  re-ran `cat-parasite-control.spec.ts` — **1 of 1 failed**, identical `getByRole('status')`
  element-not-found evidence to `U34`'s. Restored both files and re-ran — **1/1 passed**. Nothing
  from this check was committed.
- **Decoration check, the back-navigation e2e — did not fail as expected, investigated rather than
  accepted.** Checked out `staff-shell.tsx` to its exact pre-fix (`c813696`) content and re-ran
  `staff-shell-sheet-closes-on-back-navigation.spec.ts` — it **passed, 1/1**, not the required fail.
  Two temporary, uncommitted diagnostic specs (removed before this commit, nothing from either
  committed) were used to find out why, rather than accepting a silent pass on a proof-of-fix check:
  - The first traced `page.on('request')` for `resourceType() === 'document'` during `goBack()`,
    reproducing the committed spec's own exact setup (`page.goto('/staff/cats')` then
    `page.goto('/staff/vaccines')`, both hard, full-page loads in Playwright). It found a genuine
    document request fired during `goBack()` — a **hard reload**, not the soft, client-side route
    change `f392132`'s fix and CODE_REVIEW's 2nd-pass MEDIUM are about. A hard reload remounts the
    whole app fresh, trivially resetting `sheetOpen` to its initial `false` regardless of whether the
    fix exists — which is exactly why the committed spec passes identically either way.
  - The second rebuilt the same scenario using a real in-sheet `<Link>` click (`onNavigate`) to reach
    `/staff/vaccines` instead of `page.goto()`, then re-opened the sheet there before calling
    `goBack()`. Against the checked-out pre-fix `staff-shell.tsx`: **no** document request fired
    during `goBack()` (a genuine soft nav) and `getByRole('dialog')` count was **1** — the sheet
    stayed open, the real bug, directly reproduced. Restoring the fix and repeating the identical
    steps: no document request, dialog count **0** — the sheet closes. **This confirms `f392132`'s
    production fix is genuinely correct on the real mechanism**, independently of the committed
    spec's own (flawed) proof of it.
  - Restored `staff-shell.tsx` to `HEAD` (confirmed `git status --short` clean) before and after both
    diagnostics; both temporary spec files deleted before this commit.
  - **Reported as a TEST-suite finding** (the committed spec provides no actual regression coverage
    for the MEDIUM it was written to guard, since its own setup can never produce the failure mode),
    **not fixed** — this gate verifies and reports; rewriting an already-committed spec is not this
    pass's call to make unilaterally. See FINDINGS and EXISTING TESTS JUDGED WEAK in
    `docs/gates/HO-CAT-002-TEST.md` for the fix direction (use a real in-sheet `<Link>` click, as the
    second diagnostic did, in place of the second `page.goto()`).
- **One full serial suite**, `npx playwright test --workers=1` (the one full run this pass's reduced
  brief calls for): **110 total, 110 passed, 0 failed**, 7.7 minutes. Up from the 7th pass's 108 —
  the two new executions are `staff-shell-sheet-closes-on-back-navigation.spec.ts` × 2 projects, both
  watched pass. **E18/E19 (`cat-responsive.spec.ts`) passed clean on both projects inside this run**
  — no 360px overflow at any of the four breakpoints, on either project. Per this pass's own brief,
  since the one full run surfaced no 360px problem, the triple isolated E18 re-runs and the clean
  `rm -rf .next` prod build (both done at the 4th/7th passes to settle an actual anomaly) are skipped
  this pass — there is nothing here for them to settle. No default-parallel run was requested or run
  this pass, consistent with the brief's "one full run" instruction.

## Cases added at this gate

Six new automated cases, added only where a §15/§16 case or an earlier gate's fixed finding had
**zero** existing test coverage. No passing test was rewritten.

| File | Covers | Why it was missing |
|---|---|---|
| `tests/unit/cat/high-risk-flag-dialog.test.tsx` (3 tests) | §15 U31's "every form" — `HighRiskFlagDialog` (§11.7) had no dedicated test at all, unlike every other §11.7 dialog | Gap in §15 U31 coverage, not a §19 finding |
| `tests/unit/cat/weights-page.test.tsx` | CODE_REVIEW gate LOW, fixed `ee59d2a` — weight-history row-key collision | The fix commit touched only `page.tsx`; no test was added with it |
| `tests/unit/lib/cats-api-encoding.test.ts` | SECURITY gate LOW, fixed `241d310` — `catId` path-traversal via missing `encodeURIComponent` | The fix commit touched only `lib/api/cats.ts`; no test was added with it |
| `tests/unit/lib/api-client-header-override.test.ts` | SECURITY gate LOW, fixed `241d310` — identity headers spread order (caller could override `X-CSRF-Token`) | Same commit, same gap |

### Cases added at the second pass

One new automated case, added because checking `dbba453`'s effect on the shared `StaffShell`'s
keyboard operability (per this pass's brief) surfaced a real, previously-unreported regression with
**zero** existing coverage anywhere (no dedicated `StaffShell` test existed at all — see the
Environment section above). Not a rewrite of any passing test.

| File | Covers | Why it was missing |
|---|---|---|
| `tests/e2e/staff-shell-toggle.spec.ts` (2 tests × 2 projects) | New regression, found this pass: the sidebar collapse/expand toggle's first activation per mount loses keyboard focus to `<body>` (introduced by `dbba453`) | `StaffShell` has never had a dedicated test of any kind; this is the first |

### Cases added at the third pass

Two new automated cases: one landed with the fix commit itself (audited, not authored, by this
gate), one added by this gate for a new finding its own real-browser diligence check surfaced.

| File | Covers | Why it was missing |
|---|---|---|
| `tests/unit/staff-shell.test.tsx` (4 tests, added by `7304f85`) | The 2nd pass's MEDIUM (focus loss on the toggle's first click) — element identity and focus across the first and second clicks at both viewports, plus a shared manual override | `StaffShell` had no unit test of any kind before this; audited for decoration this pass (see Environment) rather than taken on trust |
| `tests/e2e/staff-shell-manual-expand-overflow.spec.ts` (4 screens × 2 projects = 8 tests, added this gate) | New finding, found this pass: manually expanding the sidebar at 360px introduces 24–28px of horizontal overflow, since the manual override's width classes ignore the viewport by design | No existing case interacts with the toggle and then measures layout — `cat-responsive.spec.ts` never clicks it, `staff-shell-toggle.spec.ts` only checks focus |

### Cases added at the fourth pass

One new automated case, added by this gate for a new finding its own real-browser, keyboard-only
diligence check surfaced while confirming `7f758b3`'s overlay fix.

| File | Covers | Why it was missing |
|---|---|---|
| `tests/e2e/staff-shell-manual-expand-occludes-content.spec.ts` (2 tests, both projects, added this gate) | New finding, found this pass: manually expanding the sidebar at 360px now overlays (rather than overflows past) the viewport, but with no backdrop, so it visually and interactively covers nearby main-content controls — reproduced on `/staff/cats`'s "Register a cat" button | No existing case checks anything beyond the overlay's own nav links for "not clipped" — `staff-shell-manual-expand-overflow.spec.ts` only measures `document.documentElement`'s own scroll width |

### Cases added at the fifth pass

One new automated case, added by this gate for a new finding its own real-browser, keyboard-only
diligence check surfaced while confirming `8379121`'s scrim/drawer fix. The fix commit's own two
other test changes — the rewritten `staff-shell-manual-expand-occludes-content.spec.ts` and the new
`Escape` case in `tests/unit/staff-shell.test.tsx` — were audited for decoration (see Environment)
rather than authored by this gate, so they are not listed here as "added at this gate", but their
audit result is recorded in R13 below.

| File | Covers | Why it was missing |
|---|---|---|
| `tests/e2e/staff-shell-drawer-not-focus-trapped.spec.ts` (1 test, both projects = 2 executions, added this gate) | New finding, found this pass: while the 360px drawer is open, Tab is not trapped inside it — three Tabs from the toggle reach `/staff/cats`'s "Register a cat" button behind the scrim, and `Enter` activates it (navigates to `/staff/cats/new`) while the drawer is still nominally open | No existing case checks keyboard tab order while the drawer is open — the rewritten occludes-content spec only checks Escape and a scrim click; neither exercises Tab |

### Cases added at the sixth pass

Two new automated cases (3 test bodies total), added by this gate for two new findings this pass's
own required **full** accessibility/behaviour audit surfaced while confirming `bb24673`'s `inert`
fix. `bb24673`'s own test changes (the rewritten `staff-shell-drawer-not-focus-trapped.spec.ts` and
the two new inert cases in `tests/unit/staff-shell.test.tsx`) were audited, not authored by this gate
— their result is recorded in R14 below.

| File | Covers | Why it was missing |
|---|---|---|
| `tests/e2e/staff-shell-drawer-focus-not-returned-to-trigger.spec.ts` (2 tests, both projects = 4 executions, added this gate) | New finding, found this pass: neither `Escape` nor a scrim click returns focus to the toggle once Tab has moved focus onto a drawer nav link first — `Escape` leaves it on the (now-collapsed) link, a scrim click drops it to `document.body` | No existing `Escape`/scrim-click case ever moves focus off the toggle before closing the drawer, so none of them could have caught this |
| `tests/e2e/staff-shell-drawer-toast-not-inert.spec.ts` (1 test, both projects = 2 executions, added this gate) | New finding, found this pass: `<Toaster />` is a sibling of `StaffShell`'s entire tree at the root layout, outside the `inert` fix's reach — a live toast's Dismiss button stays fully Tab-reachable while the drawer is open | No existing case combines "a toast is showing" with "the drawer is open"; `StaffShell`'s own tests never render a toast, and the toast specs never touch the sidebar |

### Cases added at the seventh pass

No new automated cases were added by this gate itself — the rebuild's own commits (`b209ef7`,
`c813696`) already rewrote every existing staff-shell spec/unit-test file to match the new Radix-based
sheet, and both of the 6th pass's own regression files
(`staff-shell-drawer-focus-not-returned-to-trigger.spec.ts`,
`staff-shell-drawer-toast-not-inert.spec.ts`) already existed with exactly the right contract — they
needed only their selectors updated (audited, not authored, by this gate; see R15/R16 below and the
Environment section's decoration check). This pass's own diligence (the full accessibility/behaviour
audit) found no new gaps with zero existing coverage — every item the brief named was either already
exercised by a rewritten file or checked ad hoc this pass and found correct, with nothing left over
that would need a new permanent case (see "Gaps left deliberately" for the one NOTE-level test gap
this pass declines to add unilaterally: a dedicated, disciplined APG wrap-around Tab-trap test beyond
what `staff-shell-drawer-not-focus-trapped.spec.ts` already checks).

### Cases added at the eighth pass

No new automated cases were added by this gate itself this pass — both new tests
(`tests/unit/cat/parasite-control-page.test.tsx` and
`tests/e2e/staff-shell-sheet-closes-on-back-navigation.spec.ts`) landed with the owner-decided fix
commits themselves (`b83bd2a`, `f392132`) and were audited, not authored, by this gate — see `U34`
and the new end-to-end row below, and the decoration checks in the Environment section.

| File | Covers | Audited, not authored |
|---|---|---|
| `tests/unit/cat/parasite-control-page.test.tsx` (`U34`, landed with `b83bd2a`) | A11's "shows its next-due date" half, unblocked by HO-CAT-001's C10 amendment | Proven non-decorative this pass — see Environment |
| `tests/e2e/staff-shell-sheet-closes-on-back-navigation.spec.ts` (landed with `f392132`) | CODE_REVIEW gate 2nd-pass MEDIUM — the sheet surviving browser Back/Forward | **Judged decorative for its stated purpose this pass** — it does not fail against the pre-fix code; the production fix is confirmed correct by a separate, ad hoc check instead. See Environment and `docs/gates/HO-CAT-002-TEST.md`'s EXISTING TESTS JUDGED WEAK section |

## Unit/component cases (HO-CAT-002 §15)

**Second pass**: all 32 rows below re-executed via the same `npm test` run reported in the
Environment section (93/93 passed, unchanged pass/fail shape) — no row's status or underlying test
file changed between passes, so evidence text is left as originally written rather than repeated
verbatim per row.

**Third pass**: U1–U32 re-executed via the same `npm test` run reported in the Environment section
(97/97 passed) — unchanged. U33 is new this pass, for `staff-shell.test.tsx` (landed with the fix
commit `7304f85`, audited rather than authored by this gate — see the Environment section's
decoration check).

**Fourth pass**: U1–U33 re-executed via the same `npm test` run reported in the Environment section
(97/97 passed, unchanged count) — none of their underlying files changed this pass (`7f758b3` touched
only `staff-shell.tsx`'s JSX classes, not its toggle-mounting logic `U33` exercises, and an e2e spec's
doc comment).

**Fifth pass**: U1–U33 re-executed via the same `npm test` run reported in the Environment section
(98/98 passed). U33's own file (`staff-shell.test.tsx`) gained a 5th test this pass — the fix
commit's new `Escape` case — landed with `8379121` and audited (not authored) by this gate; see U33's
updated evidence below.

**Sixth pass**: U1–U33 re-executed via the same `npm test` run reported in the Environment section
(100/100 passed). U33's own file gained 2 more tests with `bb24673` (the two `inert` cases, stubbing
`window.matchMedia`) — audited, not authored, by this gate; see U33's updated evidence below.

**Seventh pass**: U1–U32 re-executed via the same `npm test` run reported in the Environment section
(101/101 passed) — unchanged. U33's own file (`staff-shell.test.tsx`) was rewritten wholesale by
`c813696` for the Radix rebuild — the `override` tri-state, `matchMedia`-driven `inert` effect, hand-
rolled scrim and manual `Escape` listener it used to cover are gone, so every assertion tied to them
was replaced rather than merely extended. Audited, not authored, by this gate — see the Environment
section's decoration check (the two 6th-pass regression e2e specs, not this unit file, are what was
put through a fail-then-pass decoration check this pass, since `staff-shell.test.tsx` itself doesn't
carry either of the 6th pass's own findings). See U33's updated evidence below.

**Eighth pass**: U1–U33 re-executed via the same `npm test` run reported in the Environment section
(102/102 passed) — unchanged; `staff-shell.test.tsx` (`U33`) gained only a `vi.mock('next/navigation')`
for `f392132`'s new `usePathname()` import, still the same 8 tests, all pass. `U34` is new this pass,
for `tests/unit/cat/parasite-control-page.test.tsx` (landed with `b83bd2a`, audited rather than
authored by this gate) — **the handover's own §15 table also numbers this case `U33`; that ID was
already taken in this sheet, so it is `U34` here** (see the header note). Proven non-decorative — see
Environment.

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| U1 | `AgeDisplay`, exact DOB | Exact age, no "estimated" | PASS | `age-display.test.tsx` — watched pass |
| U2 | `AgeDisplay`, estimated DOB | Marked approximate (US-CAT-01 AC2) | PASS | same file — watched pass |
| U3 | `AgeDisplay`, no DOB | "Unknown", no crash | PASS | same file — watched pass |
| U4 | `CatProfileHeader` with allergies | Every allergen rendered | PASS | `cat-profile-header.test.tsx` — watched pass |
| U5 | `CatProfileHeader`, `hasRecordedAllergies: false` | "None recorded" (US-CAT-03 AC2) | PASS | same file — watched pass |
| U6 | `CatProfileHeader`, high-risk cat | Badge + note, before name in DOM order (US-CAT-04 AC1) | PASS | same file — watched pass |
| U7 | `CatProfileHeader`, not high-risk | No badge | PASS | same file — watched pass |
| U8 | `CatProfileHeader`, deceased cat | Deceased banner with date | PASS | same file — watched pass |
| U9 | `CatProfileHeader` latest weight | "kg" shown (UI-8) | PASS | same file — watched pass |
| U10 | `VaccinationStatusBadge`, three statuses | Correct label + icon each, not colour alone | PASS | `vaccination-status-badge.test.tsx`, 3 sub-tests — watched pass |
| U11 | Status panel, `DueSoon` | Due date + booking link (US-CAT-07 AC2) | PASS | `vaccination-status-panel.test.tsx` — watched pass |
| U12 | Status panel, `Overdue` | Boarding-blocked warning (US-CAT-07 AC3) | PASS | same file — watched pass |
| U13 | Status panel, never-administered core vaccine | Listed overdue, not omitted | PASS | same file — watched pass |
| U14 | `PhotoField`, 8 MB file | Rejected, names 5 MB limit (US-CAT-01 AC3) | PASS | `photo-field.test.tsx` — watched pass |
| U15 | `PhotoField`, `image/gif` | Rejected, names accepted types | PASS | same file — watched pass |
| U16 | `PhotoField`, valid 2 MB JPEG | Accepted | PASS | same file — watched pass |
| U17 | `CatForm`, Exact without DOB | Blocks submit, field error | PASS | `cat-form.test.tsx` — watched pass |
| U18 | `CatForm`, Estimated without DOB | Submits | PASS | same file — watched pass |
| U19 | `CatForm`, empty name | Field error, announced | PASS | same file — watched pass |
| U20 | `NeuterStatusForm`, Neutered without date | Field error | PASS | `neuter-status-form.test.tsx` — watched pass |
| U21 | `WeightEntryForm` | Unit visible; 0 and 31 kg rejected | PASS | `weight-entry-form.test.tsx` — watched pass |
| U22 | `WeightTrendChart` | Y axis "kg"; hidden data table for screen readers | PASS | `weight-trend-chart.test.tsx` — watched pass for the hidden table. **Caveat, disclosed by the test's own comment**: recharts renders nothing inside `ResponsiveContainer` in jsdom (0×0 layout), so the "Y axis labelled kg" half of this case is verified by reading `WeightTrendChart.tsx`'s `label={{ value: 'kg', ... }}` source, not by observing a rendered axis. Recorded as a judged weakness, not hidden. |
| U23 | `applyValidationErrors`, matching propertyName | Lands on that field | PASS | `errors.test.ts` — watched pass |
| U24 | `applyValidationErrors`, unmatched propertyName | Form-level error, not swallowed | PASS | same file — watched pass |
| U25 | `applyValidationErrors` | `errorCode` nowhere in the DOM (NFR-USA-4) | PASS | same file — watched pass. **Caveat**: the existing assertion checks that `errorCode` is never passed into the `setError` payload's `message`, which is the mechanism by which it could reach the DOM — it does not itself render a form and inspect the DOM. Judged adequate (react-hook-form forms in this app render only `.message`), but weaker than the case's literal wording; flagged rather than silently accepted. |
| U26 | `MarkDeceasedDialog` | Names consequences incl. record kept (UI-3) | PASS | `mark-deceased-dialog.test.tsx` — watched pass |
| U27 | `TransferOwnershipDialog` | States history transfers + owner loses access (UI-3) | PASS | `transfer-ownership-dialog.test.tsx` — watched pass |
| U28 | `ConfirmDialog` | Escape closes; focus returns to trigger | PASS | `confirm-dialog.test.tsx`, 4 sub-tests — watched pass |
| U29 | `DataTable` | Filter/sort/page written to URL | PASS | `data-table.test.tsx` — watched pass |
| U30 | `DeceasedBanner` present | Edit controls disabled with explanation, not hidden | PASS | `edit-cat-page.test.tsx` — watched pass |
| U31 | Every form | Every input has an associated `<label>` | PASS | `forms-accessibility.test.tsx`, 7 forms. **Gap found and closed at this gate**: `HighRiskFlagDialog` (§11.7, a required-reason form) was not among the 7 and had no test anywhere — see `high-risk-flag-dialog.test.tsx`, added this gate, watched pass |
| U32 | Every list | Loading, empty, error each render | PASS | `cat-list-page.test.tsx` (all three states, both empty-state titles) + `data-table.test.tsx` (the one shared implementation §11.7 says every list uses) + `vaccine-list-page.test.tsx` — watched pass. (`vaccinations`/`parasite-control` pages have no list to test — a documented HO-CAT-001 contract gap, ARCHITECTURE gate note, not a coverage miss here.) |
| U33 | `StaffShell` sidebar toggle, both viewports | **(rewritten 7th pass, `c813696`)** sm+ toggle: same element (not remounted) across the first and second clicks, focus retained (unchanged guarantee, simplified now that `collapsed` is a plain boolean). Below-`sm` sheet: closed by default and opens on the rail trigger; has an accessible title and full, un-abbreviated nav labels; a nav-link click inside it closes it; `Escape` closes it and returns focus to the rail trigger (Radix default); resizing past `sm` while open closes it rather than stranding the user; `matchMedia` is never touched while closed | PASS | `staff-shell.test.tsx`, 8 tests (2 sm+ toggle + 6 below-`sm` sheet) — watched pass 8/8, both in the full `npm test` run (101/101) and in isolation. The pre-rewrite file's original 4 sm+-toggle-era tests were **proven non-decorative at the 3rd pass**; see that pass's Environment section — the sm+ toggle's own logic is otherwise unchanged by the 7th pass, so that result still stands for the 2 tests carrying it forward. The below-`sm` sheet's 6 new tests are audited, not authored, by this gate (landed with `c813696`); they exercise `StaffShell`'s own integration (which button opens what, nav-link-click-closes, resize-closes, `matchMedia` not touched while closed) rather than re-proving Radix's own focus-trap/`Escape`/overlay-click/focus-return guarantees, which `components/ui/dialog.tsx`'s contract and `confirm-dialog.test.tsx` already cover and which this gate's own e2e specs (R15, R16, and the rewritten R13/R14 specs) independently confirm hold for the sheet specifically. **This closes the 6th pass's own gap** ("neither the `Escape` case nor the two `inert` cases exercise Tab order, focus-return-after-close, or anything outside `StaffShell`'s own render tree") structurally: there is no longer a hand-rolled mechanism whose coverage could lag behind its implementation the way `bb24673`'s `inert` fix did |
| U34 | Parasite control page, after a successful record | Shows the server-returned `nextDueOn` as visible, accessible text (`role="status"`, not only a toast) — **US-CAT-06, A11**. Added 2026-09-25 with HO-CAT-001's C10 amendment. **The handover's own §15 table numbers this case `U33`; already taken in this sheet — see the header note** | PASS | `tests/unit/cat/parasite-control-page.test.tsx` (landed with `b83bd2a`, audited not authored by this gate) — watched pass 1/1, both in the full `npm test` run (102/102) and in isolation. **Proven non-decorative this pass**: checked out `parasite-control/page.tsx` to its exact pre-change (`c813696`) content and re-ran — 1/1 failed (`findByRole('status')` timed out — the pre-change page has no status region at all), then restored and re-confirmed 1/1 pass. See Environment |

## Regression cases from earlier gates

**Second pass**: R1–R8 re-confirmed via the same `npm test`/`npm run lint` runs reported in the
Environment section; none of their underlying files changed between passes. R9 and R10 are new this
pass, closing regression coverage for the two findings this pass's fix commits addressed.

**Third pass**: R1–R10 re-confirmed via the same `npm test`/`npm run lint`/`npx playwright test
--workers=1` runs reported in the Environment section; none of their underlying files changed
between passes. R11 is new this pass, closing regression coverage for the 2nd pass's fixed MEDIUM.

**Fourth pass**: R1–R11 re-confirmed via the same `npm test`/`npm run lint`/`npx playwright test
--workers=1` runs reported in the Environment section; none of their underlying files changed between
passes. R12 is new this pass, closing regression coverage for the 3rd pass's fixed MEDIUM.

**Fifth pass**: R1–R12 re-confirmed via the same `npm test`/`npm run lint`/`npx playwright test
--workers=1` runs reported in the Environment section; none of their underlying files changed between
passes. R13 is new this pass, closing regression coverage for the 4th pass's fixed MEDIUM (fixed for
pointer input — see this pass's own new finding below for the keyboard gap the fix's tests didn't
cover).

**Sixth pass**: R1–R13 re-confirmed via the same `npm test`/`npm run lint`/`npx playwright test
--workers=1` runs reported in the Environment section; none of their underlying files changed between
passes. R14 is new this pass, closing regression coverage for the 5th pass's fixed MEDIUM.

**Seventh pass**: R1–R13 re-confirmed via the same `npm test`/`npm run lint`/`npx playwright test
--workers=1` runs reported in the Environment section; none of their underlying files changed between
passes. R14's own underlying spec was rewritten against the Radix sheet (contract unchanged — see its
updated evidence). R15 and R16 are new this pass, closing regression coverage for the 6th pass's two
fixed MEDIUMs.

**Eighth pass**: R1–R16 re-confirmed via the same `npm test`/`npx playwright test --workers=1` runs
reported in the Environment section; none of their underlying files changed between passes. R17 and
R18 are new this pass, for CODE_REVIEW's 2nd-pass MEDIUM (`f392132`) and ACCEPTANCE's BLOCKER 1
(`b83bd2a`) respectively — R17 carries a caveat (see its own evidence and the header note).

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| R1 | CODE_REVIEW | MEDIUM — vaccine catalogue pagination ignored the URL page param (fixed `a76aca5`) | Page 1 vs page 2 fetch different rows | PASS | `vaccine-list-page.test.tsx` (added with the fix) — watched pass |
| R2 | CODE_REVIEW | MEDIUM — `MedicalProfileForm` repeatable-row errors never rendered (fixed `07e0733`) | Blocked submission renders + announces every required-row error | PASS | `medical-profile-form.test.tsx` (added with the fix) — watched pass |
| R3 | CODE_REVIEW | LOW — cat list empty-state title ternary was a no-op (fixed `6e71fae`) | `includeDeceased=true` empty state shows a different title | PASS | `cat-list-page.test.tsx`, 4th test (added with the fix) — watched pass |
| R4 | CODE_REVIEW | LOW — weight-history same-date row-key collision (fixed `ee59d2a`) | Two same-date weight entries render as two distinct rows | PASS | `weights-page.test.tsx` — added at the 1st pass; watched pass again this pass, now also covering R10 below in the same test |
| R5 | SECURITY | LOW — `catId` not encoded in API paths, client-side path-traversal (fixed `241d310`) | A `catId` with `/`/`?` stays a single, percent-encoded path segment | PASS | `cats-api-encoding.test.ts` — added at the 1st pass; watched pass again |
| R6 | SECURITY | LOW — identity headers spread before caller headers, so a caller could override them (fixed `241d310`) | A caller-supplied `X-CSRF-Token` never overrides the cookie-derived one | PASS | `api-client-header-override.test.ts` — added at the 1st pass; watched pass again |
| R7 | ARCHITECTURE | HIGH (2nd pass) — a `components/**` file imported data hooks, breaking §8.2 rule 4 (fixed `bb9fef1`) | No `components/**` file imports `useQuery`/`useMutation`/`useInfiniteQuery` | PASS | `npm run lint` clean — the `no-restricted-imports` rule (`eslint.config.mjs`) forbids exactly this import anywhere outside `lib/hooks/`, and would fail the build if it recurred. Watched run clean again this pass |
| R8 | ARCHITECTURE | LOW (3rd pass) — doc comments cited an ESLint rule that doesn't exist (fixed `06121d9`) | Comments in `lib/catEnumOptions.ts`/`lib/api/cats.ts` no longer claim an enforcing rule | PASS | Read both files directly again this pass: unchanged, still says "No lint rule enforces this; it is a convention." |
| R9 | TEST (1st pass) | MEDIUM — `StaffShell`'s sub-640px sidebar collapse depended on a mount-time `matchMedia` effect, causing a reproducible 360px horizontal overflow (E18, §18 A20) (fixed `dbba453`) | Every screen at 360/768/1024/1440px has no horizontal scroll | PASS | `cat-responsive.spec.ts` — watched pass on: three independent isolated/serial runs of this spec alone (4/4, then 8/8, then 8/8), the full serial suite (part of 82/82), and the full default-parallel suite (part of 65/82, not among the 17 contention failures). See Environment for all four |
| R10 | TEST (1st pass) | LOW — `WeightTrendChart`'s own hidden a11y table repeated the exact same-date row-key collision R4 had just fixed in the sibling `DataTable` (fixed `b19928e`) | Two same-date weight entries render as two distinct rows in *both* tables, with no duplicate-key console warning | PASS | `weights-page.test.tsx`, extended by `b19928e` — watched pass; specifically watched the `console.error` spy assert no "same key" warning fires (the exact signal that surfaced this defect during the 1st pass) |
| R11 | TEST (2nd pass) | MEDIUM — `StaffShell`'s sidebar toggle lost keyboard focus to `<body>` on its first activation per navigation, because `dbba453` swapped a two-button fragment for a single button at the same JSX position and React remounted rather than updated (fixed `7304f85`) | Same element (not remounted) across the first and second clicks at both viewports; focus retained | PASS | `tests/e2e/staff-shell-toggle.spec.ts` (renamed by the fix, watched pass 4/4 in the full serial suite) and `tests/unit/staff-shell.test.tsx` (new, watched pass 4/4, proven non-decorative by reverting the component to its pre-fix content and watching 3/4 fail — see Environment) |
| R12 | TEST (3rd pass) | MEDIUM — manually expanding `StaffShell`'s sidebar at the 360px floor pushed the viewport 24–28px past its own width, because the manual override's `w-60`/`w-16` classes applied unconditionally, with no `sm:` guard (fixed `7f758b3`) | No horizontal scroll on four representative `/staff/**` screens after manually expanding the sidebar at 360px | PASS | `tests/e2e/staff-shell-manual-expand-overflow.spec.ts` — watched pass 8/8 (4 screens × 2 projects) in the full serial suite. **Proven non-decorative this pass**: restored `staff-shell.tsx` to its exact pre-fix (`6baf0bb`) content and re-ran — 4/4 failed with the identical `384 > 361` evidence the 3rd pass recorded (see Environment), then restored and re-confirmed 4/4 |
| R13 | TEST (4th pass) | MEDIUM — the 360px overlay had no backdrop, so it visually and interactively covered nearby main-content controls, e.g. `/staff/cats`'s "Register a cat" button (fixed `8379121`, for pointer input) | While the drawer is open, no point across the full width of "Register a cat" is mouse-clickable (resolves to `<aside>` or the scrim); a scrim click or `Escape` closes the drawer and restores the button | PASS | Rewritten `tests/e2e/staff-shell-manual-expand-occludes-content.spec.ts` (3 tests × 2 projects = 6/6, watched pass in the full serial suite) and `tests/unit/staff-shell.test.tsx`'s new `Escape` case (watched pass in the 98/98 `npm test` run). **Proven non-decorative this pass**: restored `staff-shell.tsx` to its exact pre-fix (`ee54879`) content and re-ran the rewritten spec — 2 of 3 failed (scrim/`Escape` cases; see Environment for exact errors), then restored and re-confirmed 3/3. **This fix is only for pointer input** — see the new finding below for the keyboard gap it left open, which this row's cases do not cover and were never claimed to |
| R14 | TEST (5th pass) | MEDIUM — the 360px drawer's scrim protected the mouse but not the keyboard: Tab reached, and Enter activated, `/staff/cats`'s "Register a cat" button behind the scrim (fixed `bb24673`, superseded 7th pass) | While the sheet is open, Tab never escapes it, and `Enter` cannot navigate to `/staff/cats/new` | PASS | **7th pass**: `staff-shell-drawer-not-focus-trapped.spec.ts` rewritten by `b209ef7` against the Radix sheet (its own top comment explains the change) — watched pass 2/2 in the full serial suite. The `bb24673`-era mechanism this row originally covered (`inert` on the header/`<main>` column) no longer exists; Radix's `FocusScope` traps Tab at the document level instead — confirmed by this pass's own full audit (12 consecutive Tabs each direction, never escaping the dialog) and by the CDP accessibility-tree check below. **6th pass** (superseded): rewritten `tests/e2e/staff-shell-drawer-not-focus-trapped.spec.ts` (watched pass 2/2 in the full serial suite, both in isolation and inside the run) and `tests/unit/staff-shell.test.tsx`'s two new inert cases (watched pass in the full `npm test` run). **Independently confirmed at the accessibility-tree level, not just the tab-order level**: a Chrome DevTools Protocol `Accessibility.getFullAXTree` query while the drawer is open finds no "Register a cat" node and no `main`-role landmark at all — genuinely absent from the browser's own accessibility tree, not merely excluded from the tab order (see Environment; Playwright's own `ariaSnapshot()` was tried first and gave a misleadingly "still exposed" reading, cross-checked against the real CDP tree before either result was trusted). **Proven non-decorative this pass**: restored `staff-shell.tsx` to its exact pre-fix (`8379121`) content and re-ran the current spec — 2/2 failed (`toBe(false)` assertion receiving `true` — the real pre-fix mechanism, focus genuinely reaching `main`), then restored and re-confirmed 2/2 pass. **This fix did not, by itself, close every gap on the same §8.5 text** — see R15/R16 below, now fixed by the 7th pass's rebuild |
| R15 | TEST (6th pass) | MEDIUM — closing the 360px drawer did not return focus to its trigger unless focus never left it: neither `Escape` nor a scrim click ever called `.focus()` on the toggle (fixed `1bf95d8`) | `Escape`, and separately an overlay click, after Tab has moved focus onto a sheet nav link, both return focus to the trigger | PASS | `tests/e2e/staff-shell-drawer-focus-not-returned-to-trigger.spec.ts` (updated selectors by `b209ef7`, contract unchanged) — watched pass 4/4 in the full serial suite. **Proven non-decorative this pass**: restored `staff-shell.tsx`/`dialog.tsx` to their exact pre-rebuild (`9464acd`) content and re-ran — 2/2 failed (`getByRole('dialog')` timing out — the old hand-rolled drawer has no `role="dialog"` node at all, so `openSheetAt360` can't even open it), then restored and re-confirmed 2/2 pass (part of the combined 3/3 decoration check — see Environment). The fix is structural, not a patch to the old handlers: Radix's `<DialogTrigger>` restores focus to itself on close for both `Escape` and an outside click, with no extra `StaffShell` code at all |
| R16 | TEST (6th pass) | MEDIUM — a live toast sat outside the drawer's `inert` fix and stayed fully Tab-reachable while the drawer was open, because `<Toaster />` renders at the root layout, a sibling of `StaffShell`'s entire tree (fixed `1bf95d8`) | While the sheet is open, a live toast's Dismiss button is not Tab-reachable | PASS | `tests/e2e/staff-shell-drawer-toast-not-inert.spec.ts` (updated selectors by `b209ef7`, contract unchanged) — watched pass 2/2 in the full serial suite. **Proven non-decorative this pass**: restored `staff-shell.tsx`/`dialog.tsx` to their exact pre-rebuild (`9464acd`) content and re-ran — 1/1 failed (same `getByRole('dialog')` timeout), then restored and re-confirmed 1/1 pass (part of the combined 3/3 decoration check — see Environment). The fix is structural, not per-sibling: Radix's `FocusScope`/`hideOthers` trap Tab and hide the rest of the document at the document level, regardless of where `<Toaster />`'s `ToastViewport` portals to — the exact gap the 6th pass's finding named in an `inert`-on-hand-picked-siblings approach doesn't exist for a library-level trap |
| R17 | CODE_REVIEW (2nd pass) | MEDIUM — `StaffShell`'s below-`sm` nav sheet stayed open across the browser's own Back/Forward navigation, which bypasses the sheet's in-DOM `onNavigate` entirely, leaving it trapping focus over the previous page (fixed `f392132`) | The nav sheet closes on browser Back and does not trap focus afterwards | PASS, **with a caveat** | `tests/e2e/staff-shell-sheet-closes-on-back-navigation.spec.ts` (landed with `f392132`) — watched pass 1/1. **Decoration check found the committed spec does not fail against the pre-fix code** (checked out `staff-shell.tsx` to its exact pre-fix `c813696` content and re-ran — passed 1/1, not the required fail) — its own setup (`page.goto()` twice) produces a hard document reload on `goBack()`, not the soft client-side transition the bug requires, confirmed via request tracing. **The production fix is independently confirmed correct** via a separate, ad hoc, link-based reproduction of the true soft-nav scenario (pre-fix: dialog stays open, count 1; fixed: dialog closes, count 0) — see Environment. Reported as a TEST-suite finding (the committed spec provides no real regression protection), not fixed by this gate — see FINDINGS and EXISTING TESTS JUDGED WEAK in the gate file |
| R18 | ACCEPTANCE (1st pass) | BLOCKER 1 — A11 ("a parasite treatment records and shows its next-due date") could not be evidenced: HO-CAT-001's C10 returned no `nextDueOn`, and recomputing it client-side would duplicate the aggregate rule ARCHITECTURE already rejected once (resolved by an owner amendment to HO-CAT-001 C10, `b83bd2a`) | The parasite-control page shows the server-returned `nextDueOn` as visible, accessible text after a successful record | PASS | `U34` (`tests/unit/cat/parasite-control-page.test.tsx`) and the extended `tests/e2e/cat-parasite-control.spec.ts` assertion — both watched pass, both proven non-decorative this pass (fail against the pre-amendment `c813696` code, pass against the fix) — see Environment and the U34/A11 rows |

**Findings carried over, not "fixed" and so not given a regression row**: SECURITY LOW 2 (photo
`storageKey` embeds the filename) was judged *not a defect* — accepted by design per §11.6/§20.4 —
so there is nothing to regress-test. The parasite-control doc-comment LOW (ARCHITECTURE: the old
comment named the wrong aggregate, `CatWeightRecordAggregate`) is superseded, not tracked as a live
regression row: `b83bd2a` rewrote that same doc comment wholesale for the A11 amendment, and the new
text cites only `CatAggregate.RecordParasiteTreatment`, no longer the wrong name — see "Gaps left
deliberately" for the record. ARCHITECTURE's `useAllVaccines` no-ceiling LOW is now addressed in
**code** by `631904f` (a 100-page ceiling that throws rather than looping forever), but still has no
dedicated regression test of its own — see "Gaps left deliberately", not given a full regression row
here since nothing watches it fail/pass.

## End-to-end cases (HO-CAT-002 §16)

Run against MSW only (§5.1) — see Environment for the parallel-vs-serial methodology.

**Second pass**: E1–E17 and E20–E22 re-confirmed via the same full-serial and full-parallel runs
reported in the Environment section; none of their specs changed between passes. E18/E19 re-run and
flipped from FAIL to PASS (see the updated row and finding below). One new row added (the last one
in this table) for the regression this pass found and gave a permanent test.

**Third pass**: E1–E22 and the parasite-control extra row re-confirmed via the same full-serial and
full-parallel runs reported in the Environment section; none of their specs changed. The `StaffShell`
toggle row flips from FAIL to PASS (the fix is confirmed — see the updated row and superseded finding
below). One new row added (the last one in this table) for the new finding this pass's own
diligence check found.

**Fourth pass**: E1–E22 and the parasite-control extra row re-confirmed via the same full-serial and
full-parallel runs reported in the Environment section; none of their specs changed. The 3rd pass's
manual-expand-overflow row flips from FAIL to PASS (the fix is confirmed — see the updated row and
superseded finding below). One new row added (the last one in this table) for the new finding this
pass's own real-browser, keyboard-only diligence check found.

**Fifth pass**: E1–E22 and the parasite-control extra row re-confirmed via the same full-serial and
full-parallel runs reported in the Environment section; none of their specs changed. The 4th pass's
overlay-occludes-content row flips from FAIL to PASS (fixed for pointer input — the rewritten spec
and the decoration check both confirm this; see the updated row and superseded finding below). One
new row added (the last one in this table) for the new finding this pass's own real-browser,
keyboard-only diligence check found: the drawer does not trap focus.

**Sixth pass**: E1–E22 and the parasite-control extra row re-confirmed via the same full-serial and
full-parallel runs reported in the Environment section; none of their specs changed. The 5th pass's
drawer-not-focus-trapped row flips from FAIL to PASS (the fix is confirmed — see the updated row and
superseded finding below). Two new rows added (the last two in this table) for the two new findings
this pass's own full accessibility/behaviour audit found: focus is not returned to the trigger on
close, and a live toast is not covered by the drawer's `inert` fix.

**Seventh pass**: E1–E22 and the parasite-control extra row re-confirmed via the same full-serial and
full-parallel runs reported in the Environment section; none of their specs changed. All five
`StaffShell`-specific rows below whose specs were rewritten for the Radix sheet (toggle,
manual-expand-overflow, manual-expand-occludes-content, drawer-not-focus-trapped, and the two 6th-pass
rows) are re-confirmed against the new selectors — see each row's updated evidence and R11–R16 above.
**The 6th pass's two FAIL rows (focus not returned to the trigger; a live toast not covered) both flip
to PASS** — the Radix rebuild fixes both, confirmed on the real mechanism (see R15/R16 and the
Environment section's decoration check), not merely re-run against the rewritten spec. No new rows
added this pass — see "Cases added at the seventh pass" above for why.

**Eighth pass**: E1–E22 re-confirmed via the same full-serial run reported in the Environment
section; none of their specs changed. The parasite-control extra row flips from a "records only"
caveat to full "records and shows its next-due date" evidence, closing ACCEPTANCE's BLOCKER 1 (see
R18). E6's evidence gets a note recording the owner's 2026-09-25 acceptance of this sheet's own
wording, resolving ACCEPTANCE's "undeclared deviation" item. One new row added (the last one in this
table) for `f392132`'s Back/Forward fix — watched pass, but see its own caveat and R17.

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| E1 | Register a cat, land on profile | Cat created; header shows entered data | PASS | `cat-register.spec.ts` — watched pass, serial run |
| E2 | Register with empty name | Inline error, no navigation, data preserved (NFR-USA-5) | PASS | same file — watched pass |
| E3 | High-risk cat, every tab | Badge + note on every tab (US-CAT-04 AC1, UI-7) | PASS | `cat-profile-header.spec.ts` — watched pass on both projects (chromium 39.8s, mobile-360 27.4s) |
| E4 | No-allergies cat, every tab | "None recorded" on every tab (US-CAT-03 AC2) | PASS | same file — watched pass, both projects |
| E5 | Record a weight, open chart | Point appears; header weight updates (US-CAT-02 AC2) | PASS | `cat-weights.spec.ts` — watched pass, both projects |
| E6 | Record a vaccination | Appears in status with next-due (US-CAT-05 AC1) | PASS | `cat-vaccinations.spec.ts` — watched pass, both projects. **Note, 8th pass**: HO-CAT-002 §16's own "Expected" column reads "Appears in history with its next-due date"; this row's wording ("appears in status") was written to match what the API actually supports (HO-CAT-001 has no vaccination-history read endpoint, only Q4's `vaccination-status`) rather than to match §16's own text verbatim. The owner's 2026-09-25 amendment (HO-CAT-002.md §1) explicitly accepts this sheet's wording as correct and this note as the missing explanation ACCEPTANCE's "undeclared deviation" item asked for — resolved, not a live finding |
| E7 | Vaccination with expired batch | 400 → field error on batch expiry (US-CAT-05 AC2) | PASS | same file — watched pass, both projects |
| E8 | Cat with overdue core vaccine | Overdue badge + boarding warning (US-CAT-07 AC3) | PASS | same file — watched pass, both projects |
| E9 | Record vaccination, reopen status tab | Refreshed, not stale (§8.4 invalidation) | PASS | same file — watched pass, both projects |
| E10 | Flag high-risk, no reason | Blocked with field error | PASS | same file — watched pass, both projects |
| E11 | Mark a cat deceased | Consequences named; banner; controls disabled (US-CAT-09) | PASS | `cat-deceased.spec.ts` — watched pass, both projects |
| E12 | Cat list after E11 | Deceased absent by default; visible with filter (US-CAT-09 AC1/AC2) | PASS | same file (combined with E11 in one spec) — watched pass |
| E13 | Transfer ownership | States what transfers; succeeds (US-CAT-08 AC1) | PASS | `cat-transfer.spec.ts` — watched pass, both projects |
| E14 | Download medical-history PDF | A PDF downloads (US-CAT-10) | PASS | `cat-medical-history-and-cage-card.spec.ts` — watched pass, both projects |
| E15 | Open cage card | QR renders; print layout applies (US-CAT-11) | PASS | same file — watched pass, both projects |
| E16 | Keyboard-only register a cat | Completable, no mouse; focus visible (UI-6) | PASS | `cat-register.spec.ts` — watched pass, both projects |
| E17 | axe on every §8.3 screen | Zero serious/critical violations (UI-6) | PASS | `cat-a11y.spec.ts`, 17 screens × 2 projects = 34 sub-tests — **all 34 watched pass in the clean serial run**. The first, parallel run showed 2 of these 34 failing (`scrollable-region-focusable`/axe `focusable-content` on `.overflow-x-auto`, mobile-360, weights + vaccines pages); both passed cleanly on serial re-run with nothing else contending for the CPU. Recorded per the Environment note as resource-contention flakiness, not a finding — but it is one axe rule to watch if it recurs, since a genuinely non-scrollable `.overflow-x-auto` wrapper (`components/ui/table.tsx:12`) with no `tabindex` **would** be a real UI-6 gap the moment a table's content is actually wide enough to scroll |
| E18 | Every screen at 360 px | No horizontal scroll, no clipped control (UI-1) | **PASS (2nd pass)** | `cat-responsive.spec.ts`, fixed by `dbba453`. **1st pass**: FAILed, reproduced three separate times (see the superseded finding text below, kept for the record). **2nd pass**: re-run clean on **six** separate executions — isolated `--project=chromium --workers=1` (4/4), isolated `--workers=1` both projects (8/8, run twice), the full serial suite (part of 82/82), and the full default-parallel suite (part of 65/82 — not among the 17 contention failures). 360px never failed once across any of these. **7th pass**: re-confirmed clean on three further isolated/serial executions against the Radix-rebuilt `StaffShell` (4/4, 8/8, 8/8), plus a clean `rm -rf .next` prod build checked directly at 360px (`scrollWidth`/`clientWidth` both 360). See "Finding, superseded" below |
| E19 | Every screen at 768/1024/1440 px | Layout intact (UI-1) | PASS | same spec, reconfirmed on every one of the six 2nd-pass executions above, and on the six 1st-pass executions before them |
| E20 | Simulated 409 on edit | User told record changed, offered reload, input not discarded | PASS | `cat-error-handling.spec.ts` — watched pass, both projects |
| E21 | Simulated 500 | Plain-language message, no stack trace/GUID (NFR-USA-4) | PASS | same file — watched pass, both projects |
| E22 | Cat list, 25 cats, page size 10 | Pagination works; state survives reload (UI-4) | PASS | `cat-pagination.spec.ts` — watched pass, both projects |
| — | (not a §16 ID) A parasite treatment records **and shows its next-due date** | Treatment recorded; success toast; next-due date shown as visible, accessible text (US-CAT-06, A11) | **PASS (8th pass), full evidence** | `cat-parasite-control.spec.ts` — extra coverage beyond §16, watched pass, both projects. **1st–7th passes**: PASS for "records" only — the "shows its next-due date" half had no evidence and none was possible under HO-CAT-001's original C10 contract (no `nextDueOn` in the response, no history `GET`) — this was ACCEPTANCE's BLOCKER 1. **8th pass**: unblocked by the owner's amendment to HO-CAT-001 C10 (`b83bd2a`); the extended spec now also asserts `page.getByRole('status').getByText('Next due 19 Dec 2026')` — watched pass 2/2 (both projects), and **proven non-decorative**: checked out both `parasite-control/page.tsx` and the mock handler to their exact pre-amendment (`c813696`) content and re-ran — 1/1 failed (`getByRole('status')` not found), then restored and re-confirmed 1/1 pass. See Environment and R18 |
| — | (not a §16 ID, introduced 2nd pass) `StaffShell` sidebar toggle — keyboard focus survives its first activation, at 360px and 1440px | Pressing Enter on the visible collapse/expand control moves focus somewhere still part of the page, never to `<body>` | **PASS (3rd pass)** | `staff-shell-toggle.spec.ts`, renamed by `7304f85` — watched pass 4/4 (both widths × both projects) in the full serial run. **2nd pass**: FAILed, reproduced 4/4 (see the superseded finding text below, kept for the record). **7th pass**: re-confirmed 4/4 against the Radix rebuild — below `sm` the control now opens a `Dialog` sheet instead of expanding the aside in place, but the same "focus never falls back to `<body>`" contract holds (this file's own top comment explains the update) |
| — | (not a §16 ID, introduced 3rd pass) `StaffShell` sidebar toggle, manually expanded at 360px — no horizontal scroll on representative `/staff/**` screens | Expanding the sidebar via the visible toggle at the 360px floor introduces no horizontal scroll | **PASS (4th pass)** | `staff-shell-manual-expand-overflow.spec.ts`, fixed by `7f758b3` — watched pass 8/8 (4 screens × 2 projects) in the full serial run, three further isolated E18 executions unaffected, and the full default-parallel run. **3rd pass**: FAILed, reproduced 8/8 (see the superseded finding text below, kept for the record). **Proven non-decorative this pass**: restored `staff-shell.tsx` to its exact pre-fix (`6baf0bb`) content and re-ran the chromium project — 4/4 failed with the identical `384 > 361` evidence, then restored and re-confirmed 4/4. **7th pass**: rewritten by `b209ef7` against the sheet (its premise — an in-flow aside that widens below `sm` — is now structurally impossible; the file's own top comment explains the change) — watched pass 8/8 (4 screens × 2 projects) in the full serial run |
| — | (not a §16 ID, introduced 4th pass) `StaffShell` sidebar overlay at 360px does not visually or interactively cover nearby main-content controls | Expanding the sidebar at 360px leaves the page's own controls (e.g. `/staff/cats`'s "Register a cat" button) fully visible and clickable, not covered by the overlay | **PASS (5th pass), for pointer input** | Rewritten `staff-shell-manual-expand-occludes-content.spec.ts` (now 3 tests × 2 projects = 6/6, fixed by `8379121`) — watched pass 6/6 in the full serial run and the full default-parallel run. **4th pass**: FAILed, reproduced 2/2 (see the superseded finding text below, kept for the record). **Proven non-decorative this pass**: restored `staff-shell.tsx` to its exact pre-fix (`ee54879`) content and re-ran — 2 of 3 failed (scrim/`Escape` cases), then restored and re-confirmed 3/3. **A real-browser mouse-hit-test check this pass** (`document.elementFromPoint` across the button's full width, not just one edge point) confirms no point is mouse-clickable while the drawer is open. **7th pass**: rewritten again by `b209ef7` against Radix's own `DialogOverlay` (`z-50 fixed inset-0`) instead of the hand-rolled scrim — watched pass 6/6 in the full serial run |
| — | (not a §16 ID, introduced 5th pass) `StaffShell` sidebar drawer at 360px does not trap keyboard focus | While the sheet/drawer is open, Tab cannot escape it, and nothing outside it is keyboard-activatable | **PASS (6th pass)** | Rewritten `staff-shell-drawer-not-focus-trapped.spec.ts`, fixed by `bb24673` — watched pass 2/2 (both projects) in isolation and in the full serial run. **5th pass**: FAILed, reproduced 2/2 (see the superseded finding text below, kept for the record). **Independently confirmed at the accessibility-tree level this pass**: a Chrome DevTools Protocol query finds no "Register a cat" node and no `main` landmark at all while the drawer is open. **7th pass**: rewritten again by `b209ef7` against Radix's `FocusScope` (`loop: true, trapped: true`) — watched pass 2/2 in the full serial run; this pass's own audit extended the check to 12 consecutive Tabs/Shift+Tabs each direction (ad hoc, not committed — see Environment), all staying inside the dialog |
| — | (not a §16 ID, 6th pass, **fixed 7th pass**) closing the `StaffShell` sheet does not return focus to its trigger unless focus never left it | `Escape` or an overlay click, after Tab has moved focus onto a sheet nav link, must return focus to the trigger | **PASS (7th pass)** | `staff-shell-drawer-focus-not-returned-to-trigger.spec.ts` (selectors updated by `b209ef7` for the Radix sheet; contract unchanged) — watched pass 4/4 in isolation and in the full serial run. **6th pass**: FAILed, reproduced 4/4 (see "Finding, superseded by the 7th pass" below, kept for the record). **Proven fixed on the real mechanism this pass**: restored `staff-shell.tsx`/`dialog.tsx` to their exact pre-rebuild (`9464acd`) content and re-ran — 2/2 failed (`getByRole('dialog')` timing out, not a loosely-worded assertion), then restored and re-confirmed 2/2 pass (part of the combined 3/3 decoration check — see Environment and R15) |
| — | (not a §16 ID, 6th pass, **fixed 7th pass**) a live toast is not covered by the `StaffShell` sheet's isolation from the rest of the page | While the sheet is open, a live toast's Dismiss button must not be Tab-reachable | **PASS (7th pass)** | `staff-shell-drawer-toast-not-inert.spec.ts` (selectors updated by `b209ef7` for the Radix sheet; contract unchanged) — watched pass 2/2 in isolation and in the full serial run. **6th pass**: FAILed, reproduced 2/2 (see "Finding, superseded by the 7th pass" below, kept for the record). **Proven fixed on the real mechanism this pass**: restored `staff-shell.tsx`/`dialog.tsx` to their exact pre-rebuild (`9464acd`) content and re-ran — 1/1 failed (same `getByRole('dialog')` timeout), then restored and re-confirmed 1/1 pass (part of the combined 3/3 decoration check — see Environment and R16). Also passed clean in the full default-parallel run except a single non-reproducing timeout on this spec's own setup step (contention flake — see Environment) |
| — | (not a §16 ID, introduced 8th pass) the `StaffShell` nav sheet closes on browser Back/Forward | The sheet must not survive a route change that bypasses its own `onNavigate` (most notably the browser's Back/Forward buttons); focus must be free afterwards, not trapped over the previous page | **PASS, with a significant caveat — see R17** | `staff-shell-sheet-closes-on-back-navigation.spec.ts` (landed with `f392132`) — watched pass 1/1. **This pass's own decoration check found the committed spec does not fail against the pre-fix `c813696` `staff-shell.tsx`** (it passed 1/1 there too) — its setup (`page.goto()` twice) produces a hard document reload on `goBack()`, not the soft client-side transition the underlying bug requires, confirmed by tracing `page.on('request')` for `resourceType() === 'document'` during `goBack()` (non-empty — a real document request fired). **The production fix is independently confirmed correct** via a separate, ad hoc, non-committed diagnostic using a real in-sheet `<Link>` click (not `page.goto()`) to build the forward-navigation history: against the pre-fix code, `goBack()` produced no document request (a genuine soft nav) and the sheet stayed open (`getByRole('dialog')` count 1 — the real bug); against the fix, the identical steps produced no document request and the sheet closed (count 0). Reported as a TEST-suite finding, not a production defect — see FINDINGS and EXISTING TESTS JUDGED WEAK in the gate file |

### Finding, superseded by the 2nd pass — E18 / UI-1 / §18 A20: real 360 px horizontal-overflow defect

**Status as of the 2nd pass: fixed by `dbba453`, confirmed above. Kept verbatim below for the
record**, per this sheet's own rule that a result is recorded, not erased, once superseded.

Reproduced on three independent runs, on both Playwright projects, always at exactly the 360 px
breakpoint (768/1024/1440 never failed): document `scrollWidth` exceeds `clientWidth` by
28–100 px. **Which** screen the loop's first failing assertion lands on varies between runs
(`/staff/cats/{catId}` twice, `/staff/cats/new` once) — the loop throws on the first failure and
stops, so later screens in the same run are never reached and may or may not also overflow.

That the *specific screen* varies while the *breakpoint* never does is itself informative: one
`error-context.md` snapshot (chromium, the run that failed on the profile page) shows the page
still in its **loading skeleton** (`"Loading cat profile…"`) at the moment `scrollWidth` was
measured, despite `page.waitForLoadState('networkidle')` having already resolved. The test's own
comment names the mechanism this is consistent with: `StaffShell`'s sidebar collapses below
640 px via a **mount-time `matchMedia` effect**, not CSS alone, and `networkidle` has no way to
wait for a JS effect that isn't a network request. Each `page.goto` is a full navigation that
remounts the shell fresh, re-racing that effect against measurement every time — which would
explain why the failure is 100% reproducible at 360 px specifically, while which screen exhibits
it first is not. This is offered as the most likely mechanism, not confirmed by fixing it — fixing
is the next gate's or the implementer's job, not this one's.

**This is reported as a finding, not fixed.** It is real (three-for-three reproductions, serial and
isolated), it is not mock-dependent (pure client-side layout, no HO-CAT-001 involvement), and it
sits on UI-1, which HO-CAT-002 §7 calls non-aspirational and §18 A20 lists as an acceptance
criterion. It is why this gate's overall STATUS is FAIL rather than PASS.

### Finding, superseded by the 2nd pass — WeightTrendChart repeats the exact row-key defect R4 just fixed elsewhere

**Status as of the 2nd pass: fixed by `b19928e`, confirmed as R10 above. Kept verbatim below.**

While writing R4's regression test (two weight entries on the same `recordedOn`), Vitest printed,
unprompted, from `WeightTrendChart.tsx`:

```
Encountered two children with the same key, `2026-06-01`. Keys should be unique so that components
maintain their identity across updates. Non-unique keys may cause children to be duplicated
and/or omitted...
```

`WeightTrendChart.tsx:60` renders its own visually-hidden accessibility table — the one the
component's own doc comment says "is what actually satisfies UI-6" for this component (U22) —
with `key={point.recordedOn}`. This is the identical same-date collision class CODE_REVIEW found
and `ee59d2a` fixed in the *other* weight-history table (`app/(staff)/staff/cats/[catId]/weights/
page.tsx`'s `DataTable`), just never applied to this second, independent implementation of the
same list. **Reported as a finding, not fixed** — same LOW severity as the original, same fix
shape (`` `${point.recordedOn}-${index}` ``) would apply.

### Finding, superseded by the 3rd pass — StaffShell's sidebar toggle loses keyboard focus on its first activation

**Status as of the 3rd pass: fixed by `7304f85`, confirmed as R11 above. Kept verbatim below for
the record.** `7304f85` mounts both per-viewport `<Button>`s always, with stable `key`s, each
deriving its state from `override ?? <its own viewport default>` — a click only ever updates props
on an already-mounted element now, so there is no longer a `Fragment`-to-single-element type change
at the same JSX slot for React to remount across. Confirmed two ways this pass: the renamed
`tests/e2e/staff-shell-toggle.spec.ts` watched pass 4/4 (both widths × both projects, full serial
run), and the new `tests/unit/staff-shell.test.tsx` watched pass 4/4 — proven non-decorative by
reverting `staff-shell.tsx` to its pre-fix content and watching 3 of its 4 cases fail against it
(see the Environment section's "Decoration check" for the exact assertions and error messages).

**Not present before this gate's own `dbba453` fix.** Found while confirming that fix didn't
regress the shared `StaffShell`'s keyboard operability, per this pass's brief.

`dbba453` replaced the toggle's single, always-present `<Button>` with two branches at the same
JSX position: while `override === null` (no manual choice made yet, i.e. every page immediately
after mount/navigation), it renders a `<>` of **two** `<Button>`s — one hidden below `sm` via CSS,
one hidden at `sm` and up — so exactly one is visible/focusable per width without any JS deciding
which. Once `override` becomes `true`/`false` (the user has clicked one of them), it renders a
**single** `<Button>` instead. React reconciles by position *and element type*: a `Fragment` of two
buttons and a lone `Button` are different types at the same slot, so the toggle's first click
**unmounts** the whole two-button subtree and **mounts** a fresh single button, rather than
updating the clicked button's props on the same element. A browser cannot carry keyboard focus
across an unmount; it falls back to `<body>` — the standard "nothing is focused" state. Every
keyboard/screen-reader user who activates this control for the first time after any full navigation
(the toggle resets to `override === null` on every remount) loses their place in the page entirely,
same as if they'd pressed Escape with nothing to catch focus. Subsequent clicks (once `override` is
already non-null) are fine — the button is then the same stable element instance across renders, so
an ordinary prop change never moves focus.

**Reproduced directly, not inferred**: a temporary, uncommitted Playwright script pressed Tab until
reaching the visible toggle, read `document.activeElement`, pressed Enter, and read it again — at
both 360px and 1440px, `document.activeElement === document.body` was `true` immediately after the
first Enter, and `false` (correctly still a real focusable element) after a second click on the
now-stable button. That observation is now a permanent, committed regression test —
`tests/e2e/staff-shell-toggle.spec.ts` — watched failing on all 4 of its executions (2 widths × 2
projects) in this pass's full serial re-run.

**Severity judgement: MEDIUM.** It is real (directly observed, twice, at both a narrow and a wide
width, with a committed test that reproduces it on demand), it is not mock-dependent (pure
client-side focus management, no HO-CAT-001 involvement), and because `StaffShell` wraps every
`/staff/**` route across all 18 functional areas (not just Cat Profiles), its blast radius is far
wider than this one handover — but it is narrow in *how* it manifests: only the very first toggle
activation per navigation, the assistive-tech/keyboard operability of the toggle itself is otherwise
intact (correct `aria-expanded`/`aria-label` throughout, confirmed by the same script), and no axe
rule catches it (axe scans static DOM per page load; this is an interaction-triggered focus-loss
defect no static scan can see, which is exactly why `cat-a11y.spec.ts`'s 34/34 clean run coexists
with it). This sits on HO-CAT-002 §18 A23 (WCAG 2.1 AA) via `StaffShell`, which every one of A23's
existing cases (U28, U31, E16, E17) happens not to exercise, since none of them click this specific
control.

**Reported as a finding, not fixed** — this gate verifies and reports; it does not repair
production code, and `StaffShell` is shared infrastructure no single area's gate should patch
unilaterally. Fix direction for whoever picks this up (from the new test's own doc comment): give
both the `override === null` buttons and the single fallback `<Button>` the same explicit `key`
matching their `aria-label` pairing, or collapse back to one `<Button>` element whose props are
derived from `override` with a CSS-only (not JS-timing-dependent) fallback, so there is only ever
one element instance across the transition.

### Finding, superseded by the 4th pass — manually expanding the sidebar at 360px reintroduces horizontal overflow

**Status as of the 4th pass: fixed by `7f758b3`, confirmed as R12 above. Kept verbatim below for
the record.** `7f758b3` makes the expanded aside `max-sm:fixed max-sm:inset-y-0 max-sm:left-0
max-sm:z-40 max-sm:shadow-lg` below `sm` — an overlay laid on top of the page instead of pushed
alongside it, so its 240px width no longer adds to the document's own layout width — plus a
`w-16 sm:hidden` spacer so the content's existing 64px offset doesn't move when the override is
`false`. Confirmed this pass: `staff-shell-manual-expand-overflow.spec.ts` watched pass 8/8 (both
full-suite runs), and proven non-decorative by restoring `staff-shell.tsx` to its exact pre-fix
(`6baf0bb`) content and watching all 4 of its chromium-project cases fail again with the identical
`384 > 361` evidence recorded below, before restoring the fix (see Environment).

**This fix, however, is also exactly what caused the new finding below** — turning the sidebar into
an overlay stops it from pushing the document wider, but an overlay with no backdrop instead visually
and interactively covers whatever main content shares its footprint.

**Not fixed or introduced by `7304f85`** — this is a property of `dbba453`'s own fix approach for
the *default* state, never previously observed because no existing case interacts with the toggle at
360px and then measures layout. Found while checking, per this pass's specific brief, that a manual
toggle "follows the choice at both widths" with a real browser.

It does follow the choice at both widths, correctly — that part of `dbba453`'s design works exactly
as intended (confirmed directly this pass; see the Environment section's real-browser check). The
problem is that "follows the choice regardless of viewport" is also exactly what re-creates the
*original* E18 defect on demand: `StaffShell`'s own top-of-file comment states plainly that "the
fixed w-60 (240px) sidebar plus any real content … doesn't fit UI-1's 360px floor" — this is why
`dbba453` made the *default* below `sm` collapse to `w-16` via pure CSS. But the manual override's
width classes (`override === true && 'w-16'`, `override === false && 'w-60'`) apply unconditionally,
with no `sm:` guard at all, so clicking the toggle to expand at 360px puts the sidebar into precisely
the width the component's own comment says doesn't fit there — just by an explicit user action this
time, rather than a mount-timing race.

There is also a plausible, non-hypothetical reason a real user would do this: below 640px, the
sidebar's nav items show only a single-letter abbreviation by default (the second, `aria-hidden`
`<span>` in each `Link`, sliced to `label.slice(0, 1)`) — a low-vision (not necessarily
screen-reader-dependent) user relying on the visible label rather than the accessible name has a
direct, sighted motive to press "Expand menu" specifically at this width, to read full item names.
That is exactly the trigger for this defect, not an edge case requiring an unusual interaction.

**Reproduced directly, not inferred**, on four representative `/staff/**` screens (list, profile,
medical, weights) at 360px: press the visible toggle once (`focus()` + `press('Enter')`), poll the
sidebar's `boundingBox().width` until it settles past 200px (confirming the expand landed and the
`transition-[width]` animation has finished), then read
`document.documentElement.scrollWidth`/`clientWidth`. All four screens overflow by 24–28px, on both
Playwright projects, in both the full serial run and the full default-parallel run (`384` vs a
`361px` tolerance, identically, every time) — deterministic, not contention-dependent. Not present
at 1440px (the manual override's `w-60` fits comfortably there), and not present at 360px without
the manual click (`cat-responsive.spec.ts`'s E18 continues to pass 8/8 on every isolated and full-run
execution this pass, see Environment) — this is specific to the manual override, not a repeat of the
already-fixed default-state E18 defect. That observation is now a permanent, committed regression
test — `tests/e2e/staff-shell-manual-expand-overflow.spec.ts` — watched failing on all 8 of its
executions (4 screens × 2 projects) in both full-suite runs this pass.

**Severity judgement: MEDIUM.** It is real (directly observed and reproduced on demand across four
different screens and both projects, with identical numbers every time — not a rounding artefact,
and not contention-dependent, since it fails identically under full serial *and* full parallel
runs), it is not mock-dependent (pure client-side layout, no HO-CAT-001 involvement), and because
`StaffShell` wraps every `/staff/**` route across all 18 functional areas, its blast radius is as
wide as the already-fixed 2nd-pass finding and the originally-fixed 1st-pass E18 finding — both of
which this gate treated as real, non-aspirational, and gate-failing for sitting on the same UI-1/§18
A20 criterion. Judged the same way here for consistency. It is narrower than either of those in
*trigger* (requires an explicit toggle click, not merely loading a page), which is why it is judged
MEDIUM rather than repeating their treatment outright — but the trigger itself is plausible and
foreseeable, as argued above, not contrived.

**Reported as a finding, not fixed** — this gate verifies and reports; it does not repair production
code, and `StaffShell` is shared infrastructure no single area's gate should patch unilaterally. Fix
direction (from the new test's own doc comment): cap the manual "expand" choice's width below `sm`
(the override could still flip the visible labels/content while keeping the *forced* width at
`w-16` below 640px, since there is no room for `w-60` there regardless of user intent — UI-1's 360px
floor is a hard floor per `StaffShell`'s own comment, not a preference to be overridden), or make the
sidebar an overlay (positioned over the content, not in-flow beside it) below `sm` so an expanded
state never competes with page content for the same 360px.

### Finding, superseded by the 5th pass — the 360px overlay has no backdrop, so it visually and interactively covers nearby main-content controls

**Status as of the 5th pass: fixed by `8379121`, for pointer input, confirmed as R13 above. Kept
verbatim below for the record.** `8379121` gives the expanded overlay a scrim (`data-testid=
"staff-shell-scrim"`, `fixed inset-0 z-30 bg-ink/40 sm:hidden`) between the aside (`z-40`) and the
page — exactly the first of the two fix directions this finding's own test originally offered. This
pass's real-browser mouse-hit-test check confirms no point across the full width of "Register a cat"
is mouse-clickable while the drawer is open. **But the same fix, and specifically its scrim's own
doc-comment claim to mark the covered content "inert", is what the 5th pass's new finding below shows
is only true for pointer input, not for the keyboard** — a different, narrower gap than this one, not
a repeat of it (there is no visual clipping any more; the button is fully, opaquely covered while a
mouse looks at it).

**Not present before `7f758b3`** — the previous (3rd-pass) code overflowed the viewport instead;
this finding only exists because the overlay fix removed that overflow by laying the sidebar on top
of the page rather than beside it. Found while extending this pass's required "the overlay's own nav
links are tabbable and not clipped" check to the natural next question once "the overlay covers the
page below `sm`" was confirmed as the fix's actual mechanism (see Environment): if the overlay covers
the page, does it cover anything the page needs?

It does. Below `sm`, `dbba453`'s spacer only reserves 64px for the content's offset (matching the
*collapsed* rail width), but the expanded overlay is 240px wide — a 176px band of `<main>`, starting
right after the spacer, sits directly underneath the overlay's higher `z-40` stacking, with no
backdrop/scrim to signal that this band is temporarily inert. On `/staff/cats`, the page's own
"Register a cat" button (`x ≈ 213–336`) and a filter checkbox both fall partly inside that band.

**Reproduced directly, not inferred.** `document.elementFromPoint` sampled at a point inside the
"Register a cat" button's own left edge (`x ≈ 215`, within the button's `213–336` range) resolves to
the sidebar's own "Cats" `<a>`, not the button — confirmed identically on three separate runs, same
`x` coordinates each time. A screenshot taken at the same moment shows this directly: the button's
visible label reads "egister a cat", its leading "R" hidden under the sidebar, and a page heading a
few pixels above it is cut down to "…nts". This is not a rendering glitch or a coordinate-sampling
artefact — it is the overlay's own opaque background genuinely painted on top of that content, at a
higher `z-index`, exactly where a user would look and click.

A follow-up sweep across all four of `staff-shell-manual-expand-overflow.spec.ts`'s own representative
screens (list, profile, medical, weights), checking every focusable `<main>` element whose left edge
falls inside the overlay's expanded width for whether `elementFromPoint` on it resolves inside
`<aside>`, found: **`/staff/cats`** — the "Register a cat" button and a filter checkbox, reproduced on
three separate runs with identical coordinates; **the profile page** — only transiently, during the
already-documented MSW-startup-race error state (`ErrorState`'s "Try again" button happened to sit in
the band), and *not* reproduced once the page was retried past that race onto its normal, loaded
content; **medical and weights** — nothing found inside the initial viewport. This means the defect's
per-screen *incidence* varies with what each screen happens to place in that specific 176px-wide,
roughly 64–240px-from-the-left band near the top of the page — but the underlying *mechanism* (an
un-scrimmed overlay covering that band) is universal to every `/staff/**` screen `StaffShell` wraps,
since it is a property of the shared shell, not of any one page's content. The representative,
deterministic reproduction (`/staff/cats`) is now the permanent regression test
`tests/e2e/staff-shell-manual-expand-occludes-content.spec.ts`, watched failing 2/2 (both projects) in
isolation, in the full serial suite, and in the full default-parallel suite (identical evidence under
contention, which is what marks it as real rather than flake — see Environment).

Not present at 1440px (the aside is `position: sticky` there, never `fixed` — confirmed directly this
pass, see Environment) and not present at 360px without the manual expand (the default, collapsed
state is `w-16`, far short of the 64px spacer's reserved offset).

**Severity judgement: MEDIUM.** It is real (directly observed with `elementFromPoint` and a
screenshot, reproduced identically on three separate runs, not a coordinate-sampling artefact), it is
not mock-dependent (pure client-side layout/stacking, no HO-CAT-001 involvement), and it sits on the
exact literal wording of UI-1 ("360 px is the hard floor — no horizontal scroll, no clipped
controls") — the *second* named failure mode, not the first this handover's earlier passes already
fixed. Because `StaffShell` wraps every `/staff/**` route across all 18 functional areas, its
mechanism's blast radius matches the earlier UI-1 findings this gate already treated as real and
gate-failing, judged the same way here for consistency. It is judged MEDIUM rather than more severely
because, unlike the horizontal-overflow findings (which broke every screen's layout outright), this
one requires both the same explicit "manually expand at 360px" trigger the 3rd pass's finding did
*and* the specific screen happening to place an interactive control in the affected band — narrower in
per-screen incidence, though not in underlying mechanism.

**Reported as a finding, not fixed** — this gate verifies and reports; it does not repair production
code, and `StaffShell` is shared infrastructure no single area's gate should patch unilaterally. Fix
direction (from the new test's own doc comment): give the expanded overlay a backdrop that also
captures/blocks clicks on whatever it visually covers (the standard off-canvas-drawer pattern used by
most mobile nav menus), or narrow the overlay's `max-sm:` width so it never exceeds the 64px the
spacer already reserves — the two fix directions this same test's doc comment offers.

### Finding, superseded by the 6th pass — the 360px drawer's scrim protects the mouse, but does not trap keyboard focus, so Tab reaches and Enter activates content the scrim claims is inert

**Status as of the 6th pass: fixed by `bb24673`, confirmed above (see R14 and the updated
end-to-end row). Kept verbatim below for the record — this is what was found and why it mattered.**

**Not present before `8379121`** — the previous (4th-pass) code had no scrim at all, so there was
nothing claiming the covered content was "inert" for this finding to contradict; this finding exists
*because* the fix introduced that claim without also implementing it for the keyboard. Found while
answering this pass's own required question directly: "while the drawer is open, can Tab reach
anything behind the scrim?"

It can, and it is operable once there. The scrim is a plain `aria-hidden="true"` `<div>` with an
`onClick`; nothing gives `<main>` an `inert` attribute or a `tabindex="-1"` sweep, and nothing loops
Tab/Shift+Tab back to the drawer's own first/last focusable element. Tab order is pure DOM order,
unrelated to the `z-index` stacking the scrim relies on for pointer input, so three Tabs from the
toggle — past the drawer's own two nav links ("Cats", "Vaccine catalogue") — land squarely on
`/staff/cats`'s "Register a cat" button, the same control the 4th pass's finding named.

**Reproduced directly, not inferred.** A temporary, uncommitted Playwright script opened the drawer
via the keyboard (`toggle.focus()` + `Enter`, not a click), pressed Tab three times, and read
`document.activeElement.textContent` — it is "Register a cat", confirmed identically across three
further isolated executions plus one run across both projects (six observations total). At that exact
moment, the scrim is still `toBeVisible()` and the toggle's `aria-expanded` is still `"true"` —
confirmed directly, not assumed — so this is unambiguously "while the drawer is open", not after some
earlier, unnoticed close. Pressing `Enter` (not a mouse click; a focused native `<button>` responds to
`Enter` regardless of what a mouse would hit-test at its own coordinates) fires the button's `onClick`
and **the page navigates to `/staff/cats/new`**, confirmed via `page.url()` after allowing the
client-side navigation to settle (an initial, looser `expect(page).not.toHaveURL(...)` assertion
passed spuriously here, since Playwright's auto-retrying `not` matcher is satisfied by a mismatch
existing at any instant, including the instant before `router.push` resolves — corrected before
this became the permanent test, see Environment).

By contrast, **mouse input is fully protected**: `document.elementFromPoint` sampled across the
entire width of "Register a cat" (left edge, centre, right edge) resolves to either the `<aside>` or
the scrim at every one of the three points while the drawer is open — no mouse click can reach it.
This is what makes the finding specifically a *keyboard* gap, not a repeat of the 4th pass's
(now-fixed) pointer-input finding.

This sits directly on HO-CAT-002 §8.5's own, explicit, "non-negotiable" text: **"Keyboard operable end
to end. Every dialog traps focus, closes on `Escape`, and returns focus to its trigger. shadcn/ui's
Radix primitives give this — which is why the library was chosen. Hand-rolled dialogs do not."** The
drawer *does* close on `Escape` and return focus correctly (confirmed by the rewritten
`staff-shell-manual-expand-occludes-content.spec.ts`'s own `Escape` case and
`tests/unit/staff-shell.test.tsx`'s new `Escape` unit case, both watched pass) — but it is a
hand-rolled overlay, introduced by this very fix commit, with a scrim whose own doc comment claims to
mark the covered content "inert" without implementing the one thing (`8.5`) says a dialog must have to
back that claim up for a keyboard user: trapped focus. It reproduces, in a brand-new piece of shared
infrastructure, precisely the failure mode the handover names by that exact term.

**Judging the rewritten spec itself, per this pass's specific brief (step 1)**: the drawer contract the
integrator substituted for the original "stays uncovered while open" assertion is a legitimate,
non-decorative fix for the specific defect the 4th pass raised (a half-exposed, still-mouse-clickable
control) — confirmed both by the decoration check (2 of 3 cases fail against the pre-fix component,
see Environment) and by this pass's own full-width mouse-hit-test sweep. **It is not, however, a
complete answer to UI-1/§8.5 as written**, because "covering content with a scrim while a drawer is
open" and "hand-rolled dialogs do not trap focus" are two different, independently-checkable
properties, and the rewritten spec — like the fix's own new unit test — only exercises the first
(`Escape`, a scrim click) and never Tab. That gap in the spec's own coverage is exactly why the
integrator's reported 98/98 unit and 100/100 (single, presumably default-parallel or otherwise
non-adversarial) Playwright run did not surface this. **This is recorded as a decision for ACCEPTANCE
and the owner, not a silent pass**: whether the drawer, as shipped, is acceptable without a focus
trap, or whether §8.5's dialog contract must be satisfied in full before this MEDIUM can be closed.

**Severity judgement: MEDIUM.** It is real (six independent, identical observations across isolated
executions and both full-suite runs, with the client-navigation timing artefact in the *test's* first
draft found and corrected before being relied on — see Environment), it is not mock-dependent (pure
client-side focus/DOM-order behaviour, no HO-CAT-001 involvement), and it sits on both UI-1's "no
clipped controls" wording (the scrim's claim to inertness is exactly what's being violated, just for
keyboard rather than visual clipping) and, more directly, on HO-CAT-002 §8.5's explicit, named
"hand-rolled dialogs do not [trap focus]" warning via `StaffShell`'s §18 A23 (WCAG 2.1 AA). Because
`StaffShell` wraps every `/staff/**` route across all 18 functional areas, its blast radius matches
every other UI-1/A23 finding this gate has treated as real and gate-failing, judged the same way here
for consistency. It is judged MEDIUM rather than HIGH because it requires the same explicit "manually
expand at 360px" trigger every prior finding in this shell has required, and because a mouse-only user
— the majority path — is fully protected by the scrim; the exposure is specific to keyboard/assistive
technology users navigating with Tab, which is exactly the population UI-6/§8.5 exist to protect, which
is why it is not judged LOW either.

**Reported as a finding, not fixed** — this gate verifies and reports; it does not repair production
code, and `StaffShell` is shared infrastructure no single area's gate should patch unilaterally,
consistent with every prior finding in this shell. Fix direction (from the new test's own doc
comment): either trap focus within the `<aside>` while the drawer is open (loop Tab/Shift+Tab at its
first/last focusable element back to the other end, the standard APG dialog pattern — the same
guarantee §8.5 says Radix's `Dialog` already gives, which is why the handover recommends using it
rather than hand-rolling), or give `<main>` (and anything else the scrim visually covers) a real
`inert` attribute for the duration, so the browser itself removes it from the tab order rather than
relying on `z-index` stacking, which only ever governed pointer hit-testing.

### Finding, superseded by the 7th pass — closing the drawer does not return focus to its trigger unless focus never left it

**Status as of the 7th pass: fixed by the `1bf95d8`/`92a33e5` Radix `Dialog` rebuild, confirmed as
R15 above and in the updated end-to-end row. Kept verbatim below for the record — this is what was
found and why it mattered.** The integrator didn't patch the old `Escape`/scrim-click handlers to add
a missing `.focus()` call — it replaced the whole hand-rolled drawer with a real Radix `Dialog`, whose
`<DialogTrigger>` already restores focus to itself on close for both `Escape` and an outside click,
with no extra `StaffShell` code at all. Proven fixed on the real mechanism, not the rewritten spec's
own say-so: restoring `staff-shell.tsx`/`dialog.tsx` to their exact pre-rebuild (`9464acd`) content and
re-running `staff-shell-drawer-focus-not-returned-to-trigger.spec.ts` against it reproduces both
failures (`getByRole('dialog')` timing out — the old drawer never had a `role="dialog"` node), and
restoring the rebuild returns 2/2 passes (see Environment).

Found during the 6th pass's required **full** audit of the drawer (not another single-item chase): the
brief's own "focus return after close" item. `bb24673` fixed the 5th pass's finding by making the
skip link and the header/`<main>` column `inert` while the drawer is open — genuine and confirmed
(R14 above). **But neither the `Escape` `keydown` handler nor the scrim's `onClick` ever calls
`.focus()` on the toggle.** They only set `override` back to `null`; nothing moves focus anywhere.

The existing regression coverage never noticed because it never needed to: `staff-shell.test.tsx`'s
`Escape` case and `staff-shell-manual-expand-occludes-content.spec.ts`'s `Escape`/scrim-click cases
all close the drawer **immediately after opening it**, before Tab has moved focus off the toggle. In
that one case, focus was never anywhere else, so it trivially "stays on the toggle" — which looks
identical to "returns focus to the toggle" from the outside, but is not the same guarantee, and
disappears the moment a real keyboard user does what §8.5 assumes they will: tab into the drawer's
own nav links before deciding to close it.

**Reproduced directly, not inferred**, on the real dev server, in a fresh session with no other
suite running concurrently:
- Opened the drawer at 360px via keyboard (`toggle.focus()` + `Enter`), pressed Tab twice (landing on
  "Vaccine catalogue", the drawer's own second nav link — confirmed via
  `document.activeElement.textContent`), then pressed `Escape`. The drawer genuinely closes (scrim
  count 0, toggle `aria-expanded="false"`, `<aside>`'s class reverts to the collapsed `w-16 sm:w-60`
  default) — but `document.activeElement` is still the "Vaccine catalogue" link, not the toggle.
  Its own `getBoundingClientRect()` is `209.5px` wide even though the sidebar it sits in has
  collapsed to a 64px rail — the link's own layout box doesn't shrink with its container, so the
  keyboard focus indicator is left on a control that no longer looks like what it did when it was
  focused. A further Tab from there lands directly on `/staff/cats`'s "Register a cat" button — the
  page is fully keyboard-usable again post-close, just not from where §8.5 says it should resume.
- Opened the drawer the same way, then clicked the scrim (not the toggle). The drawer closes
  correctly, but `document.activeElement === document.body` — focus is dropped entirely, not merely
  left on the wrong element. This is the same "focus lost to `<body>`" failure mode this exact
  component's own 2nd-pass MEDIUM (`staff-shell-toggle.spec.ts`) was about, reached here by a
  different path (the scrim, not the toggle's own first click).

Both are now the permanent regression test
`tests/e2e/staff-shell-drawer-focus-not-returned-to-trigger.spec.ts` (2 cases × 2 projects = 4
executions), watched failing 4/4 in isolation and identically inside the full serial run (see
Environment). **Proven non-decorative**: both cases assert `toBeFocused()` on the toggle itself (a
real, observable DOM outcome), not a looser proxy; both fail with `Received: "inactive"` — the real
mechanism, not a flaky wait.

This sits on the same HO-CAT-002 §8.5 text the 5th pass's finding cited: **"Every dialog traps focus,
closes on `Escape`, and returns focus to its trigger... Hand-rolled dialogs do not."** The drawer does
close on `Escape` (and on a scrim click) — but "returns focus to its trigger" has never actually been
implemented for either path; it only ever *looked* implemented in the one trivial case where focus
had nowhere to have moved from.

**Severity judgement: MEDIUM**, for the same reasons this shell's other MEDIUMs have been judged
MEDIUM rather than HIGH or LOW: real and directly reproduced (not mock-dependent, pure client-side
focus/DOM behaviour), requires the same explicit "manually expand at 360px, then interact with the
drawer's own contents" trigger every prior finding here has required, and its blast radius is
`StaffShell`'s — every `/staff/**` route across all 18 functional areas — matching every other
UI-6/§8.5 finding this gate has treated as real and gate-failing.

**Reported as a finding, not fixed** — `StaffShell` is shared infrastructure no single area's gate
should patch unilaterally. Fix direction: give the toggle button a ref, and call `.focus()` on it
at the end of both the `Escape` handler and the scrim's `onClick` — the same "return focus to
trigger" step Radix's own `Dialog` (and this exact codebase's own `ToastClose`/`ConfirmDialog`,
per U28) already provide for free, which is precisely why §8.5 recommends them over a hand-rolled
implementation.

### Finding, superseded by the 7th pass — a live toast is outside the drawer's `inert` fix and stays fully Tab-reachable while the drawer is open

**Status as of the 7th pass: fixed by the `1bf95d8`/`92a33e5` Radix `Dialog` rebuild, confirmed as
R16 above and in the updated end-to-end row. Kept verbatim below for the record.** The fix is
structural, not a matter of also marking `<Toaster />` `inert`: Radix's `FocusScope`/`hideOthers` trap
Tab and hide the rest of the document at the document level, regardless of where a portal (like
`<Toaster />`'s `ToastViewport`) renders to — the exact gap an `inert`-on-hand-picked-siblings approach
has (it only protects whatever it explicitly enumerates) doesn't exist for a library-level trap.
Proven fixed on the real mechanism: restoring `staff-shell.tsx`/`dialog.tsx` to their exact pre-rebuild
(`9464acd`) content and re-running `staff-shell-drawer-toast-not-inert.spec.ts` against it reproduces
the failure (`getByRole('dialog')` timing out), and restoring the rebuild returns 1/1 pass (see
Environment).

Also found during the 6th pass's required full audit, specifically the "screen-reader exposure of the
inert content" item, extended past `<main>`/`<header>` to *everything else* the scrim visually
covers. `bb24673`'s `inert` fix only reaches `<main>`/`<header>` and the skip link — all three are
descendants of `StaffShell`'s own render tree. **`<Toaster />` is not.** `app/providers.tsx` renders
it as a sibling of `{children}` (which is where `StaffShell` lives) at the root layout, once for the
whole app — entirely outside anything `StaffShell` can mark `inert` from within itself.

`components/ui/toast.tsx`'s `ToastClose` is a real `<button aria-label="Dismiss">`. If a toast is on
screen at the moment the drawer opens, that button is not a descendant of the `inert` wrapper, so
Tab reaches it exactly as if the drawer weren't open at all.

**Reproduced directly**, using `cat-parasite-control.spec.ts`'s own steps (recording a parasite
treatment leaves a "Treatment recorded" toast on screen) at 360px: with the toast still visible,
opened the drawer via keyboard, and pressed Tab four times from the toggle — `document.activeElement`
on the fourth Tab has `aria-label="Dismiss"`, confirmed directly, with the scrim still `toBeVisible()`
at that moment. Also confirmed structurally, not just behaviourally: the Dismiss button's own
`.closest('[inert]')` is `null` while the drawer is open — it genuinely sits outside the `inert`
subtree, not merely reachable by some Tab-order quirk within it.

Now the permanent regression test `tests/e2e/staff-shell-drawer-toast-not-inert.spec.ts` (1 case × 2
projects = 2 executions), watched failing 2/2 in isolation and identically inside the full serial run
(see Environment). **Proven non-decorative**: the assertion (`reachedDismiss` must be `false`) fails
with `Received: true` — the real Dismiss button was really reached, not a loosely-worded proxy.

Same HO-CAT-002 §8.5 text as both other findings in this shell: "every dialog traps focus... hand-
rolled dialogs do not." This is a different piece of content than the 5th pass's `<main>`/`<header>`
gap (already fixed) or this pass's own focus-return gap above — content that lives entirely outside
`StaffShell`'s own tree, which is exactly the risk of a hand-rolled, `inert`-based fix scoped to one
component's own subtree rather than a library primitive (Radix `Dialog`) that manages this at the
document level regardless of where a portal happens to render.

**Severity judgement: MEDIUM**, though the narrowest-triggering finding in this shell so far: it
requires not only the usual "manually expand at 360px" trigger, but also a toast actually being on
screen at that exact moment — a real but comparatively rare overlap (e.g. opening the drawer right
after a save/record action). Judged MEDIUM rather than LOW because, when the overlap does occur, the
exposure is total (the toast's action is fully reachable and activatable, no partial mitigation),
and because the mechanism — content entirely outside an `inert` fix's own subtree — is structural,
not a one-off: any future toast, and any future root-level portal, would have the identical gap.
Judged MEDIUM rather than HIGH because of how narrow the trigger window is relative to every other
finding this shell has raised.

**Reported as a finding, not fixed** — `StaffShell` is shared infrastructure, and `<Toaster />` lives
at the root layout, outside HO-CAT-002's own `web/**` component scope for `StaffShell` specifically.
Fix direction: either lift the drawer-open `inert` state to a component that also wraps `<Toaster />`
(e.g. the root layout/`Providers`), or replace the `inert`-on-siblings approach with an actual
focus-trap implementation (per the other new finding's fix direction) that traps Tab at the document
level rather than relying on marking specific, individually-discovered siblings `inert`.

## Acceptance-criteria coverage (HO-CAT-002 §18)

| Criterion | Covered by | Status |
|---|---|---|
| A1 — register a cat | E1 | PASS |
| A2 — estimated DOB shows approximate age | U2, E1 | PASS |
| A3 — oversized/disallowed photo rejected clearly | U14, U15 | PASS |
| A4 — neuter status requires date | U20 | PASS |
| A5 — weight trend chart with units | U21, U22, E5 | PASS (U22 has the disclosed jsdom-rendering caveat above) |
| A6 — allergies in header, every screen | U4, E3 | PASS |
| A7 — "None recorded", never blank | U5, E4 | PASS |
| A8 — high-risk badge + note, every screen | U6, E3 | PASS |
| A9 — record vaccination with batch/date/vet, next-due | E6 | PASS |
| A10 — expired batch rejected on the right field | E7 | PASS |
| A11 — parasite treatment records, shows next-due | U34, the parasite-control e2e | **PASS (8th pass), full evidence.** **1st–7th passes**: PASS for "records" only; "shows its next-due date" had no evidence and none was possible under HO-CAT-001's original C10 contract — this was ACCEPTANCE's BLOCKER 1. **8th pass**: unblocked by the owner's amendment to HO-CAT-001 C10 (`b83bd2a`) — `U34` and the extended e2e assertion both show the server-returned `nextDueOn` as visible, accessible text, and both are proven non-decorative (fail against the pre-amendment code, pass against the fix). See R18 |
| A12 — all-current shows "Current" | U10 | PASS |
| A13 — "Due soon" shows date + booking link | U11 | PASS. §20.5's choice — a real, always-enabled link to the not-yet-built booking route — is confirmed by U11's assertion on the link's `href` |
| A14 — "Overdue" warns boarding blocked | U12, E8 | PASS |
| A15 — ownership transfer confirms specifics | U27, E13 | PASS |
| A16 — deceased confirms; leaves default list | U26, E11, E12 | PASS |
| A17 — deceased record stays viewable, not editable | U30, E12 | PASS |
| A18 — medical history PDF downloads | E14 | PASS |
| A19 — cage card QR + print | E15 | PASS |
| A20 — 360/768/1024/1440 px | E18, E19 | **PASS.** For the *default* state on every page load, confirmed again this pass (E18/E19 clean on three fresh isolated executions plus a clean prod build — see Environment). 1st pass's FAIL (default-state overflow), 3rd pass's FAIL (manual-expand overflow), 4th pass's FAIL (un-scrimmed overlay occluding pointer input), 5th pass's FAIL (Tab reaching content behind the scrim), and 6th pass's FAIL (focus not returned to the trigger; a live toast reachable) are all superseded/fixed, kept on record above. This pass confirmed 768/1024/1440px are unaffected by the Radix rebuild (aside stays `position: sticky`, never a `Dialog`, at all three widths and at the exact 640px boundary) and that resizing across 640px while the sheet is open — both narrowing and, while open, widening past `sm` — is handled correctly (widening auto-closes the sheet rather than stranding the user in it). **7th pass**: below-`sm` can no longer widen the in-flow `<aside>` at all under any state (`1bf95d8`'s own top comment), so the mechanism behind the 3rd/4th/5th passes' findings no longer exists structurally, not merely patched; a clean `rm -rf .next` prod build settles the 6th pass's own unresolved build anomaly (not reproduced). **8th pass**: this delta doesn't touch layout/width mechanics; E18/E19 re-confirmed clean on both projects in the one full serial run this pass's own (reduced) brief called for — no isolated re-runs or prod build needed since nothing there needed settling |
| A21 — destructive actions confirm specifics | U26, U27 | PASS |
| A22 — lists filter/sort/paginate | U29, E22 | PASS |
| A23 — WCAG 2.1 AA | U28, U31, E16, E17 | **PASS.** (E17's parallel-run failures judged flake — see Environment). **2nd/3rd/4th/5th/6th-pass caveats, all now resolved by the 7th pass's Radix rebuild**: below `sm`, `StaffShell` no longer hand-rolls any part of the dialog contract — focus-trapping, `Escape`, overlay-click-to-close, and focus-return-to-trigger all come from `@radix-ui/react-dialog` itself, the exact primitive §8.5 names ("shadcn/ui's Radix primitives give this... hand-rolled dialogs do not"). This pass's own full audit (12 consecutive Tabs/Shift+Tabs each direction never escaping the dialog; `Escape`/overlay-click/close-button each returning focus to the trigger, watched directly; a live toast confirmed unreachable via both a real Tab walk and `.closest('[inert]')`-equivalent structural check; a real CDP `Accessibility.getFullAXTree` query finding no "Register a cat" node while the sheet is open) found no further violations — only two carried-forward NOTEs (no `aria-modal="true"` on Radix's `Dialog.Content` in this version; the `<main>` tag itself isn't marked `aria-hidden`, though every one of its rendered children individually is, so nothing inside is reachable or exposed despite the tag surviving as an empty landmark). Neither NOTE is a requirement violation on §8.5's literal (behavioural, not ARIA-role) wording. The center-docked dialogs (`ConfirmDialog` and everything built on it) are confirmed unregressed by the same rebuild (17/17 unit, 7/7 e2e) |
| A24 — header carries full required set | U4–U9, E3 | PASS |
| A25 — units always shown | U9, U21, U22 | PASS |
| A26 — plain-language errors, no code/id | U25, E21 | PASS (U25 caveat noted above) |
| A27 — form data survives validation error | E2 | PASS |
| A28 — 409 conflict explained, input preserved | E20 | PASS |

## Cases I could not run

| ID | Why | What would unblock it |
|---|---|---|
| §5.1 — re-run E1–E22 (and the extra parasite-control case) against the real HO-CAT-001 API | HO-CAT-001 does not exist yet; every case above ran only against MSW mocks, per §5.1's own rule that a mock-only pass "has verified your mock, not the system" | Build/deploy HO-CAT-001; set `NEXT_PUBLIC_USE_MOCKS=false` and a real `NEXT_PUBLIC_API_BASE_URL`; re-run `npm run test:e2e` in full |
| ARCHITECTURE gate MEDIUM — paging-cap assumption (`pageSize+1` requests, the `pageSize=1000` weight series, `useAllVaccines`'s loop — now capped at 100 pages client-side by `631904f`, but the server's own `PageSize` policy is still unverified) | Needs the real Platform `Query<T>` base's actual `PageSize` capping/rejection behaviour; MSW cannot fake a backend policy that doesn't exist yet | HO-CAT-001 merge; confirm large `PageSize` values are capped or rejected, not silently honoured or 500'd |
| ARCHITECTURE/SECURITY gate MEDIUM — credentialed CORS (`credentials: 'include'` needs the API to echo `http://localhost:3000` with `Access-Control-Allow-Credentials: true`, not `AllowAll`/`*`) | Needs a running API host with its real CORS policy; nothing to dial in this environment | HO-CAT-001 merge; confirm the CORS policy from a real browser request, not just read the `UseCorsToAllowAll()` source |
| §18 A16/A17's related guarantee, US-CAT-08 AC2 — "the previous owner can no longer view the cat" | Structurally unverifiable from this UI by HO-CAT-002 §4.2/§20.3 design: there is no login, so there is no way to *be* the previous owner in this UI to prove they lost access | The Accounts slice (real login/roles) plus HO-CAT-001's real authorization (today a query filter only, per HO-CAT-001 §20.3) |

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| **(resolved in code, 8th pass, still untested)** ARCHITECTURE gate LOW — `useAllVaccines` had no page-count ceiling | `631904f` caps the loop at 100 pages and throws past that rather than looping forever — resolved in production code. **No dedicated test exercises the ceiling itself** (a 101st-page scenario, or the thrown error), since MSW's fixture catalogue is far short of 100 pages and constructing one would be disproportionate for a LOW guard-rail | Whoever next touches the vaccine-catalogue hooks; add a unit test mocking `getVaccines` to always return `hasNextPage: true` and asserting the thrown error, if this is ever wanted as a committed regression |
| **(resolved 8th pass)** ARCHITECTURE gate LOW — parasite-control page's doc comment cited the wrong aggregate | `b83bd2a` rewrote the doc comment wholesale for the A11 amendment; the new text cites only `CatAggregate.RecordParasiteTreatment`, no longer the wrong `CatWeightRecordAggregate` name — confirmed by reading the file directly this pass | Resolved; no longer a gap |
| U22's Y-axis-label assertion is by source inspection, not rendering | jsdom/recharts cannot lay out an `SVG` at a real size; documented in the test itself | Acceptable as-is; only worth revisiting if the project ever adds a browser-mode Vitest runner |
| **(resolved 8th pass)** No dedicated component test asserted A11's next-due display | HO-CAT-001's C10 amendment (`b83bd2a`, owner decision 2026-09-25) now returns `nextDueOn`; `U34` (`parasite-control-page.test.tsx`) asserts it directly, proven non-decorative | Resolved; no longer a gap |
| **(8th pass)** The committed `staff-shell-sheet-closes-on-back-navigation.spec.ts` does not fail against the pre-fix `StaffShell` — it provides no real regression protection for CODE_REVIEW's 2nd-pass MEDIUM, because its own setup (`page.goto()` for both forward navigations) produces a hard document reload on `goBack()`, sidestepping the soft client-side transition the bug requires | Reported, not fixed, per this gate's mandate — rewriting an already-committed spec is not this pass's call to make unilaterally. The production fix itself is independently confirmed correct via an ad hoc, non-committed, link-based diagnostic — see Environment, R17 and the gate file's FINDINGS | Whoever owns `tests/e2e/staff-shell-*`; replace the spec's second `page.goto('/staff/vaccines')` with a real in-sheet `<Link>` click (as the ad hoc diagnostic did) so `goBack()` exercises Next's actual client-side history instead of a hard reload |
| **(2nd pass, resolved 3rd pass)** `StaffShell` had no dedicated unit/component test of any kind | The 2nd pass's fix commit (`7304f85`) added `tests/unit/staff-shell.test.tsx`; this gate audited it for decoration rather than treating the gap as still open — see the Environment section's decoration check | Resolved; no longer a gap |
| **(3rd pass, resolved 4th pass for the overflow shape)** `StaffShell` has no dedicated test of *layout* (only of focus/element-identity) despite being shared infrastructure for all 18 staff areas | `7f758b3` fixed the 3rd pass's overflow finding, confirmed by this pass; but this pass's own new occlusion finding shows the same underlying gap (no test of the overlay's effect on nearby content) — see below | Whoever owns `components/common/**` cross-cutting coverage, or an ACCEPTANCE-gate decision to require the fix from this pass's new finding before sign-off |
| **(4th pass, resolved 5th pass for pointer input)** `StaffShell`'s 360px overlay has no dedicated test covering *every* screen for content occlusion — only `/staff/cats`, the one deterministic reproduction this pass found | Sweeping every `/staff/**` screen × every possible control position is open-ended and out of this gate's scope for one area's handover; the transient occlusion observed on the profile page (during the documented MSW-race error state only) was not made into a test, since it wasn't reproducible on normal load | Whoever owns `components/common/**` cross-cutting coverage, or an ACCEPTANCE-gate decision on whether a backdrop/scrim (rather than per-screen testing) is the right fix, which would close this class of gap outright |
| **(5th pass, resolved 7th pass)** No test anywhere exercised keyboard Tab order while the `StaffShell` drawer is open, on any screen other than `/staff/cats` | The Radix rebuild's focus trap is a document-level, library-provided mechanism (`FocusScope`, `trapped: true`) rather than a per-sibling `inert` sweep — it does not depend on which screen it's exercised against the way the 6th pass's `inert` fix did (that fix had to individually mark `<main>`/`<header>`/the skip link; the sheet's trap doesn't enumerate anything). Still only exercised by two screens' worth of committed tests (`/staff/cats`, the parasite-control page); a sweep of every `/staff/**` screen was not added, since the mechanism no longer varies by what a screen happens to render, but see below | Whoever owns `components/common/**` cross-cutting coverage, if a full per-screen sweep is still wanted for defence in depth |
| **(5th pass, narrowed 6th pass, resolved 7th pass)** Whether the `StaffShell` drawer, as shipped, satisfies HO-CAT-002 §8.5's "every dialog traps focus... hand-rolled dialogs do not" text | **Resolved**: it is no longer a hand-rolled drawer at all. The below-`sm` control now opens a real `@radix-ui/react-dialog` sheet — the exact primitive §8.5 names — and this pass's own full audit (focus-on-open, trap both directions, `Escape`/overlay/close-button all returning focus, screen-reader exposure via CDP, resizing across 640 both ways, 768/1024/1440 unchanged, the skip link, toasts) found no further violations. No longer an open question for ACCEPTANCE to adjudicate | Resolved; no longer a gap |
| **(6th pass, resolved 7th pass)** No dedicated APG-style focus-trap test exists (nothing asserts that Tab/Shift+Tab, run past the drawer's own first/last focusable element enough times, only ever cycles back into the drawer and never truly leaves the document) | **Narrowed, not fully closed**: `staff-shell-drawer-not-focus-trapped.spec.ts` still only checks 3 Tabs land inside the dialog, not a full wraparound loop. This pass verified, ad hoc and not as a committed test, that 12 consecutive Tabs and 12 consecutive Shift+Tabs each stay inside the dialog's own three focusable elements and never reach `main`/`header` — confirming Radix's `FocusScope`'s `loop: true` genuinely wraps, not merely that the first few presses don't escape — but did not commit this as a permanent test, since it duplicates what `loop: true` is Radix's own, independently-maintained guarantee for, and the existing 3-Tab check already catches a regression to `loop: false` or `trapped: false` | Whoever wants defence-in-depth against a future Radix upgrade changing this default; not required for this gate's own verdict, since the ad hoc check confirms the guarantee holds today |
| **(7th pass)** No committed test asserts `aria-modal="true"` on the sheet's `role="dialog"` node, or that a screen reader announces "dialog" on open | Read directly from `node_modules/@radix-ui/react-dialog`'s own source (this Radix version's `DialogContent` sets `role="dialog"` but not `aria-modal`) and confirmed live in the DOM (`getAttribute('aria-modal')` is `null`). §8.5's own quoted text is behavioural ("traps focus, closes on Escape, returns focus to its trigger"), not about ARIA role/modal semantics, and `FocusScope`/`hideOthers` already remove the covered content from the accessibility tree regardless — so this is a NOTE, not a violation, on the handover's literal wording | Owner's discretion; would need a `Dialog`/`DialogContent`-level change in `components/ui/dialog.tsx` affecting every dialog in the app, not something to patch for `StaffShell` alone |
| **(7th pass)** The `<main>` landmark tag itself is not marked `aria-hidden` while the sheet is open, though every one of its rendered children individually is (via Radix's `hideOthers`) | Functionally equivalent to being hidden (nothing inside is reachable by Tab or exposed to a real CDP accessibility-tree query — confirmed directly), but a screen reader's own landmark list would show an anomalous empty "main" region while the sheet is open. This is `hideOthers`' (the `aria-hidden` npm package Radix depends on) own behaviour, not `StaffShell`- or `PurrCare`-specific code, and not something this gate's mandate covers fixing | Owner's discretion; upstream (`aria-hidden`/`@radix-ui/react-dialog`) behaviour, not a `PurrCare` defect |
