# HO-ACC-002 — Accounts and Identity (frontend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-ACC-002** |
| Title | Accounts and Identity — frontend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §1 — **US-ACC-01, 04, 05, 06, 07, 08, 10, 11, 13, 14**. US-ACC-12 has no browser half. Withdrawn: US-ACC-02, 03, 09 |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — FR-ACC-1, 3, 4, 5, 6, 7, 9, 10, 11, 13, FR-POR-6, FR-ADM-2; **UI-1, UI-3, UI-4, UI-6**; **NFR-USA-1, NFR-USA-4, NFR-USA-5, NFR-USA-6**; NFR-SEC-1, NFR-SEC-2, NFR-SEC-4; C-9 |
| Pairs with | [`HO-ACC-001`](HO-ACC-001.md) — **its §12 is the API contract this slice consumes.** Where this document and that one disagree about the wire, **HO-ACC-001 wins** and the difference is a §20.7 stop |
| Builds on | [`HO-CAT-002`](HO-CAT-002.md) — the `web/` scaffold, stack, layering and conventions. **This slice extends it and must not recreate it** |
| Supersedes | The 12 per-story frontend handovers `HO-US-ACC-01-FE` … `HO-US-ACC-13-FE`, removed on consolidation (2026-09-22). They left session transport undecided, claimed overlapping `ADR-FE` numbers and built OTP, verification and 2FA screens the owner has removed. **Nothing in them binds this build** |
| Approved design | **None.** No design artifact exists for these screens. Build to §11 with the shadcn/ui tokens HO-CAT-002 establishes; do not invent a visual language |
| Baseline commit | `4007225` |
| Amended | **2026-09-22 — by [`HO-NTF-002`](HO-NTF-002.md), before implementation.** The notification-preferences page (§11.7) gains `PreferredChannelField` inside the same explicit Save, and the staff customer page gains a **Messages** link. Both are built by HO-NTF-002 §11.2 and §11.5. **2026-09-22 — by [`HO-APT-002`](HO-APT-002.md), before implementation.** The staff customer page gains an `AppointmentSummaryPanel`, and the sign-in `?next=` allow-list accepts `/book` so booking links survive sign-in. **2026-09-25 — by the HO-ACC-001 SECURITY gate fix on `81cf928`, before this handover's implementation.** §11.7's `ProfileForm` gains a **Current password** field (a `PasswordField` with `purpose="current"`), shown and required only when the email field is being changed from its loaded value — the backend now requires `currentPassword` in `UpdateCustomerContactDetailsCommand` whenever `EmailAddress` actually changes, the same step-up the account-deletion flow already uses, because email is also a login and password-reset identifier (see HO-ACC-001 §1 Amended (17)). A `400 ReauthenticationFailed` on this field maps to the same message pattern as the deletion step-up's own current-password error. No change to any other `ProfileForm` field. **2026-09-25 — by the HO-ACC-001 SECURITY gate fix on `81cf928`, before this handover's implementation.** Q-H2 (`GET /api/staff/customers?mobile=`) now returns `mobileMatchedOn` (`"Primary"` / `"Additional"` / `null`) per result — `StaffCustomerSummaryList`/`ExistingCustomerNotice` (§11.11) should surface this distinction (e.g. a badge or note) rather than treat every hit the same, since an "Additional" match is an unverified claim, not a confirmed identity. §11.11's walk-in behaviour also changes: **only a PRIMARY-number match refuses registration** (`201` on an additional-number-only match, where earlier it also refused) — a walk-in that succeeds despite an existing additional-number hit is not itself an error case for the form to handle. **2026-09-25 — by the HO-ACC-001 ARCHITECTURE/SECURITY gate fixes on this branch (integrator-authorised edit, this handover's own build not yet implemented).** §11.12's duplicates report gains the same `mobileMatchedOn` label per group member (Q-I1) — `DuplicateGroupList` should show it alongside each record (e.g. "Additional number only") so staff can tell a genuine shared primary number from an unverified additional-number claim before choosing **Review merge**; §11.11's `ProfileForm`/self-service equivalent (`PUT /api/customers/me`) now refuses (`400 AdditionalPhoneNumberMatchesAnotherCustomer`) an additional number equal to another active customer's primary mobile — surface this the same way as any other field-level `400`. **2026-09-25 — by the HO-ACC-001 ARCHITECTURE gate fifth-pass follow-up, before this handover's implementation.** `ProfileForm`'s `PUT /api/customers/me` now carries its own rate-limit policy (`profile-update`, 5 per 60 minutes, partitioned by the signed-in customer rather than shared with deletion/export), so repeated saves within the window can return `429` — the same `ApiRateLimitedError` handling §11.x already needs for the sign-in and reset endpoints should cover this response too; no new client-visible shape. **2026-09-26 — owner decision, after this handover's own implementation, correcting ADR-FE-002 and superseding §12.2's same-origin-proxy proposal.** Cross-origin session cookies: the browser calls the API directly, cross-origin, with `credentials: 'include'` — `lib/api/client.ts`'s existing `NEXT_PUBLIC_API_BASE_URL` design (inherited from HO-CAT-002, never actually replaced by a `next.config.ts` rewrite) is the one that ships, not §12.2's proposed same-origin proxy. The requirement this imposes: a `SameSite=Lax` cookie is only ever sent on a same-**SITE** request, which is stricter than CORS and includes the scheme ("schemeful same-site") — so **production** puts the web app and the API on two subdomains of one registrable domain, both `https` (e.g. `www.example.com` / `api.example.com`), and **development** must use the API's `http` launch profile (`http://localhost:5005`), matching `npm run dev`'s own plain-`http` origin, never an `https://localhost:7xxx` profile — see `web/.env.local.example` and `web/README.md`'s "Calling the API — same-SITE, not just CORS". **2026-09-26 — owner decision, integrator-authorised edit.** `app/page.tsx` (a HO-CAT-002 shared file) no longer redirects to the first area to register a nav item, nor unconditionally to §8.3's `/staff/cats` — it now branches on which session cookie (if either) is present: `pc_staff_session` → `/staff/cats`, `pc_session` → `/portal/cats`, neither → `/sign-in`, the same two defaults `(auth)/sign-in`/`(auth)/staff/sign-in` already use after a real sign-in. This supersedes HO-CAT-002 §8.3's unconditional root redirect. **2026-09-26 — integrator-authorised edit.** `components/common/DataTable.tsx` (a HO-CAT-002 shared file) now wraps its own table markup directly (rather than the shared `components/ui/table.tsx` `<Table>` primitive, which bakes in its own unlabelled, non-focusable `overflow-x-auto` wrapper with no way to opt out) in a `role="region"`/`aria-label`/`tabIndex={0}` scrollable region, fixing the missing-keyboard-access-to-a-horizontally-scrolling-table finding (axe `scrollable-region-focusable`) at the source instead of per call site; `components/admin/StaffAccountTable.tsx`'s own workaround wrapper is removed. Proven via an isolated-port comparison against a clean master checkout (§16, this handover's own report) that HO-CAT-002's `tests/e2e/cat-a11y.spec.ts` `/staff/vaccines` case — which also goes through `DataTable` — passed on master and only started failing on this branch once `DataTable` was in play without this fix; also fixed, in the same pass, a genuine pre-existing defect in HO-CAT-002's `components/cat/WeightTrendChart.tsx` (`accessibilityLayer` now explicitly `false`) that this branch's staff-session-guard/idle-lock addition to the shared `(staff)` layout made reliably reproducible (by shifting page-load timing) rather than introduced — both fixes and the comparison method are recorded in this handover's own report. **2026-09-26 — integrator-authorised edit, first-pass gate fix round (ARCH/SEC/CR verdicts on `05e1fa6`).** Two housekeeping items outside §13's literal file list, both already in effect and recorded here rather than reverted: (1) `web/playwright.config.ts` reads its port from `process.env.PORT` (default unchanged, `3000`) so this worktree's e2e run and another worktree's dev server never collide on the same port — this build runs Playwright with `PORT=3102`. (2) Several files this build needed are outside §13's literal list because §13 named specific per-area files/globs rather than the scaffold's own actual conventions: `web/nav/areas/accounts.ts` (every area populates its own nav registry file per HO-CAT-002's "Adding a functional area" section, §13 simply omitted it), `web/mocks/handlers/areas/{accounts,staff-accounts,staff-customers}.ts` (HO-CAT-002's scaffold nests per-area handlers one level deeper, under `areas/`, than §13's literal `mocks/handlers/{accounts,staff-accounts,staff-customers}.ts` path), `web/tests/e2e/fixtures.ts` (shared sign-in helpers used by every spec in this slice, not itself one of §13's named spec files), `web/tests/unit/staff-shell.test.tsx` and `web/tests/components/cross-cutting.test.tsx` (cover the shared `(staff)` layout and cross-area concerns, not one single area folder), and `web/lib/staffIdleLockBridge.ts` (created during the original build to bridge a since-removed global session-error handler to `IdleLock`; deleted again in this same gate fix round once each guard started registering its own handler — see this round's own report, item 2 — so it nets to no file at all, listed here only for a complete paper trail). **2026-09-26 — SECURITY gate item 7 (first-pass gate fix round, owner decision 2026-09-26), integrator-authorised edit.** `app/page.tsx` no longer reads `pc_staff_session`/`pc_session` itself (a Server Component `cookies()` read, `next/headers`) — that only ever worked by coincidence in local development, where the web app's own Next.js server and the API share a hostname; ADR-FE-002's actual production topology puts them on two different subdomains, so a request arriving at the web app's own server never carries a cookie scoped to the API's separate origin at all, and this page would have read "signed out" for everyone in production regardless of their real session state. It now calls `useCustomerSession()`/`useStaffSession()` client-side (the same hooks `CustomerSessionGuard`/`StaffSessionGuard` use), and fails closed to `/sign-in` on anything but a resolved session — a `session_idle_timeout` on the staff query still redirects to `/staff/cats`, where `IdleLock` shows the lock overlay, since the account is real, only locked. `web/README.md`'s same-SITE section gains the matching `Account:CsrfCookieDomain` requirement (`pc_csrf` needs a `Domain` in a real cross-subdomain deployment so the web app's own `document.cookie` can read it; `pc_session`/`pc_staff_session` stay host-only). See the matching Amended row in [`HO-ACC-001`](HO-ACC-001.md) §1 for the backend half (`SessionCookieWriter`, `AccountOptions.CsrfCookieDomain`, `CustomerProfileController.DeleteAccount`'s cookie-expiry fix). **2026-09-26 — CR MEDIUM 2 (first-pass gate fix round, item 13), integrator-authorised edit.** `MergeReview.tsx`'s own doc comment named a real §20.7 contract gap: HO-ACC-001's fresh single-customer read (Q-H1, this component's own data source, ADR-FE-011) reported only `catCount`, not a per-cat list, unlike the duplicates report (Q-I1); and the duplicates report itself carried no `recordStatus`/`registrationChannel` per member, despite §11.12's prose asking for both. Closed on the backend (see the matching row in [`HO-ACC-001`](HO-ACC-001.md) §1) and rendered here: `lib/api/staff-customers.ts`'s `StaffCustomer` gains `cats` (alongside the unchanged `catCount`), `DuplicateCustomerMember` gains `recordStatus`/`registrationChannel`; `MergeConfirmDialog.tsx` now lists the actual cats moving from each retired record by name instead of only a count; `DuplicateGroupList.tsx` now shows each member's `CustomerStatusBadge` and registration channel ("Walk-in"/"Self-service"), and its own doc comment (previously describing this as an unresolved, deliberate contract disagreement) is corrected. No new component — an existing shared component (`CustomerStatusBadge`) is reused, not duplicated. **2026-09-26 — ARCHITECTURE gate second pass, BLOCKER and MEDIUM, integrator-authorised.** `web/eslint.config.mjs` is HO-ACC-002 §14 forbidden scope ("no new dependency or config, §10") — it was edited in the first-pass fix round (the per-folder `no-restricted-imports` block and its `SignOutButton` override) without a §1 Amended row, which the ARCHITECTURE gate's second pass raised as a BLOCKER despite the edit itself being integrator-authorised and sound. Recorded here, together with the second pass's own fixes to it: the rule is now `error`, not `warn` (`npm run lint` also gains `--max-warnings 0`, so either severity would fail the build the same way, but `error` says what is actually meant); the hooks-folder ban's `patterns` now also catches a relative import (`../../lib/hooks/useCaptcha`), not only the `@/lib/hooks/*` alias form; and `SignOutButton.tsx`'s override no longer turns the whole rule off (which silently re-permitted an un-audited raw `useQuery`/`useMutation` import there too) — it now repeats the `useQuery`/`useMutation` ban and lifts only the hooks-folder-import ban. Also recorded, all integrator-authorised and previously unrecorded: `web/components/ui/dialog.tsx`'s additive `overlayClassName` prop (SEC gate LOW, first pass — named integrator-authorised only in the 7865ee5 commit message and a code comment until now); the 12 HO-CAT-002 `web/tests/e2e/cat-*.spec.ts` specs, the 7 `staff-shell-*.spec.ts` specs and `home.spec.ts`, each a consequential edit once the shared e2e fixture (`tests/e2e/fixtures.ts`) stopped signing in both audiences automatically (first-pass gate fix item 11) and needed an explicit `signInAsStaff`/`signInAsCustomer` call added; `web/tests/e2e/{account-area,accounts-auth,accounts-a11y-responsive,real-api-verification}.spec.ts` (flagged unrecorded in the first pass too); and `web/tests/unit/lib/next-path.test.ts` (item 16's own new test file). The row this handover previously recorded for `web/tests/components/staff/staff-shell.test.tsx` named the wrong path — corrected above to the file actually edited, `web/tests/unit/staff-shell.test.tsx`. **2026-09-26 — ARCHITECTURE gate second pass, LOW, deviation recorded.** §16's E-X3 ("after signing out, `localStorage`/`sessionStorage` empty; no `pc_session` readable from `document.cookie`") is not literally what `tests/e2e/accounts-a11y-responsive.spec.ts`'s own `E-X3` case asserts, for two reasons neither §16 nor this handover's earlier text named explicitly: (1) MSW's own interceptor library writes an internal `__msw-cookie-store__` bookkeeping entry into `localStorage` as soon as any cookie has ever been set in the browser context — mock-harness plumbing, not this app's own state, present regardless of what this app does; and (2) `sessionStorage` deliberately holds `mocks/handlers/areas/accounts.ts`'s own persisted mock "database" (the stand-in for the real API's Postgres, `canPersist`'s own comment), which is expected to keep customer/staff records after a sign-out exactly as a real database would — clearing it on sign-out would be testing a property no real backend has. What the test asserts instead, matching what sign-out actually promises: the session itself is gone (no `pc_session` in `document.cookie`), and — per MEDIUM/LOW gate item 12 (first pass) — no plaintext password is present anywhere in that persisted mock "database" either, which is the part of A26/E-X3's actual wording ("No token, password, answer or draft in browser storage") a real backend's `sessionStorage`-equivalent (server-side memory) genuinely does satisfy. **2026-09-27 — ARCHITECTURE gate third pass, BLOCKER and MEDIUM, integrator-authorised.** `web/package.json` is HO-ACC-002 §14 forbidden scope ("no new dependency or config") — `test:e2e` changed to `--project=chromium --project=mobile-360` and a new `test:e2e:real-api` script were added in the second-pass fix round (`ed412e9`) without a §1 Amended row, which the ARCHITECTURE gate's third pass raised as a BLOCKER despite the change adding no dependency and being sound. Recorded here, together with the out-of-§13 files the same pass found unrecorded: `web/playwright.config.ts`'s `real-api` project and its `testIgnore` on the two default projects (`ed412e9`; only the earlier `PORT` change was recorded before this), and `web/tests/unit/lib/pagination-caps.test.ts` (`0f12fc9`, a new file — `tests/unit/**` is not in §13). Also fixed in this same pass: `playwright.config.ts`'s own comment claimed the `real-api` project was "not run at all unless `--project=real-api` is passed explicitly", which was only true of this repo's own npm scripts — a bare `npx playwright test`, with no `--project` filter, runs every project Playwright knows about, `real-api` included, and it then fails outright against MSW. The `real-api` project entry itself is now only ever added to the config's `projects` array when `E2E_REAL_API=1` is set (`test:e2e:real-api` sets it), so a bare `npx playwright test` — or even `npx playwright test --project=real-api` without the variable — genuinely cannot run it any more, matching what the comment always claimed. **2026-09-27 — ARCHITECTURE gate fourth pass, HIGH, integrator-authorised edit (recorded here and in [`HO-CAT-002`](HO-CAT-002.md) §1).** The third pass's `StaffLockProvider` fix (`web/components/staff/IdleLockContext.tsx`) toggled `queryClient.setDefaultOptions({queries: {refetchOnWindowFocus: false, refetchOnReconnect: false}})` while locked, which never reaches an ALREADY-MOUNTED query observer — `useBaseQuery` computes and pushes its own options into the observer once, and a page/layout whose element identity is stable across a lock (the whole point of `IdleLock` keeping `children` mounted) never re-renders just because `lock()` ran. Replaced with pausing TanStack's own global `focusManager`/`onlineManager` singletons directly (`setEventListener` swapped to a no-op while locked, restoring each manager's own default setup — reproduced in this file, since neither library class exposes a way to read back whatever setup is currently installed — on unlock and on unmount while still locked). Two more direct-looking alternatives were tried and rejected, both confirmed empirically during this round's own test development: `focusManager.setFocused(false)`/`onlineManager.setOnline(false)` (manually overriding what each manager reports) also gates TanStack's own retryer (`canContinue()`), pausing an ALREADY-IN-PROGRESS retry (e.g. `useStaffAccount`'s `retry: noRetryOn403`) until the override lifts — proven live by a query hanging indefinitely instead of settling into the `isError` state its own page's `isError && !data` guard depends on; and `onlineManager.setOnline(false)` alone also paused every OTHER `networkMode: 'online'` operation while locked, including the re-sign-in `useMutation` call `IdleLock` itself uses to unlock, which would then never reach the network at all. The third pass's own `cancelQueries(undefined, {revert: false})` call is REMOVED, not kept: with `revert: false`, a cancelled query is dispatched as `isError: true` regardless of whether it had any prior data, which could turn a page's very first load into an error screen; the comment that shipped alongside it also had `revert` backwards (`revert: true`, the untouched default, is what preserves a query's prior data across a cancelled fetch). `lock()`/`unlock()` are now correctly idempotent (a `pausedRef` guard, since there is no "captured original state" left to protect from being overwritten by a second call) and restore on unmount while still locked, closing the second-pass MEDIUM the same review found (the App Router has no route-change lifecycle hook the guard could otherwise use). ADR-FE-010 (`web/docs/decisions.md`) is updated to describe the new mechanism. **Correction (2026-09-27, ARCHITECTURE gate fifth pass, LOW): this row's own integrator-authorisation claim was incomplete.** It said the edit was "recorded here and in HO-CAT-002 §1" but never itself named the two out-of-scope files that authorisation actually covers: `app/_components/cat/CatProfileDataShell.tsx` (the `isError && !data` narrowing) and `mocks/handlers/areas/cat-profiles.ts` (the `forceNextCatQueryToReportSessionInvalid`/`__forceCatQuerySessionInvalid` test hook) — both are HO-CAT-002-owned files, edited on this branch under the same integrator authorisation this row already claims, and both are recorded in full in [`HO-CAT-002`](HO-CAT-002.md) §1's own matching row; named here too now, for a complete paper trail from this side as well. **2026-09-27 — CODE_REVIEW gate fourth pass, MEDIUM, self-amendment (this handover's own build).** `web/components/account/UnsavedChangesGuard.tsx`'s sentinel-history-entry technique (ADR-FE-013) relied on `window.history.length > 1` to decide whether to push a sentinel at all -- `length === 1` remains fully reliable (unchanged: no sentinel, Back left entirely native), but `length > 1` does not reliably mean something is behind, since it counts forward entries too: a page that is genuinely the tab's first entry could still report `length > 1` after a back→forward→dirty→Back sequence, and a confirmed Leave's `history.go(-2)` would then silently no-op, stranding the user on the sentinel with no visible sign anything went wrong (a swallowed Back). Fixed by making a wrong `length > 1` guess safe rather than trying to make the guess itself reliable: each component instance (`useId()`) stamps its own real entry once, on mount, with a base marker in `history.state`, and every sentinel it pushes carries that marker plus its own id, so `onPopState` only re-intercepts a landing it can actually attribute to its own sentinel being consumed -- any other landing is left alone (no re-push, no dialog, `beforeunload` remains the only guard for that transition). The confirmed-Leave replay itself changed from a single `history.go(-2)` to two sequential `history.back()` calls, the second only issued once the first's own `popstate` arrives -- the first leg always succeeds (the sentinel is guaranteed to be immediately behind a page that genuinely exists), so even when nothing is truly behind that page the user still lands there instead of staying stuck on the sentinel. See `web/docs/decisions.md` ADR-FE-013's matching amendment for the full design and rejected-alternative reasoning. **2026-09-27 — CODE_REVIEW gate fourth pass, LOW, integrator-authorised edit (recorded in full in [`HO-ACC-001`](HO-ACC-001.md) §1).** `AccountOptions.CsrfCookieDomain`'s IP-literal rejection (HO-ACC-001 §1 Amended (22)/(24)) still accepted several browser-recognised alternate-base/shorthand IPv4 forms that are not four-part decimal dotted-quads (octal, hex, shorthand, a bare 32-bit integer) -- fixed by rejecting whenever the value's last dot-separated label is all-digits or begins with `0x`/`0X`, since a real DNS TLD is never either; see HO-ACC-001 §1 Amended (25) for the full design and the corrected/added `CsrfCookieDomainTests.cs` case counts. **2026-09-27 — ARCHITECTURE gate fifth pass, MEDIUM, self-amendment (this handover's own build).** Pausing `onlineManager`'s listener while locked (fourth-pass HIGH, above) also freezes its REPORTED `isOnline()` value at whatever it was the instant `lock()` ran, since `onlineManager` never reads `navigator.onLine` itself, only ever a real `online`/`offline` event -- if the terminal was already offline at that instant, `isOnline()` stays frozen `false` for the whole lock, and `useSignInStaff()`'s default `networkMode: 'online'` then refuses to even start the re-sign-in mutation once the terminal genuinely reconnects (`unlock()` runs only after that mutation succeeds, so the lock could never lift -- a genuine deadlock). Fixed by giving `useSignInStaff()` (`lib/hooks/useStaffAuthMutations.ts`) `networkMode: 'always'` instead, so it runs regardless of what `onlineManager` reports, and by resyncing `onlineManager.setOnline(navigator.onLine)` whenever the real listener is reattached (`unlock()` and the unmount-while-locked cleanup, via a shared `restoreManagers()` helper in `IdleLockContext.tsx`), so any online/offline transition during the lock is reflected immediately rather than waiting for the next real event. New test in `idle-lock.test.tsx` reproduces the deadlock (confirmed it fails without the fix) and proves the resync. **2026-09-27 — ARCHITECTURE gate fifth pass, MEDIUM, integrator-authorised edit (out-of-§13 file record).** `web/scripts/run-real-api-e2e.mjs` (new, ARCHITECTURE gate fourth-pass LOW's own Windows `cmd.exe` fix) and the changed `web/package.json` `"test:e2e:real-api": "node scripts/run-real-api-e2e.mjs"` command (superseding the inline `E2E_REAL_API=1 playwright test --project=real-api` the third-pass record above still describes) were built in the fourth-pass round but never recorded here -- recorded now. The script itself was also hardened this same pass (CODE_REVIEW note on the fourth-pass ARCHITECTURE gate): it used to spawn `npx` with `shell: true`, which passes forwarded CLI args through a shell unquoted; `spawnSync('npx.cmd', ..., { shell: false })` on Windows fails outright (`EINVAL` -- confirmed empirically, `npx.cmd` is a batch shim Windows cannot launch directly without a shell), so rather than spawn `npx` at all, the script now resolves Playwright's own declared `bin` entry point (`@playwright/test/package.json`'s `bin.playwright`, via `createRequire`) and spawns it directly with `process.execPath` and `shell: false` -- no shell, no shim, no `PATH` lookup, cross-platform. **2026-09-27 — CODE_REVIEW gate fifth pass, LOW, self-amendment (this handover's own build).** `idle-lock-context.test.tsx`'s `fireVisibilityChange` dispatched a non-bubbling event on `document`, a different `EventTarget` than the `window` `defaultFocusEventSetup` actually attaches its listener to -- the event never reached it, whether locked or not, so the "stays at 1" assertions passed vacuously even with no pause in effect. Fixed to dispatch on `window` directly, plus an unlocked positive control added for both the focus and the online case (proving the same dispatch technique DOES cause a real refetch when nothing is paused). `tests/e2e/staff-auth.spec.ts` also gains E-G3: a real `visibilitychange` event dispatched on `window` in an actual browser, after the lock has already engaged, proving the cat query never even attempts to refetch -- E-G2 (fourth-pass HIGH, above) only ever exercised the `isError && !data` fallback via `__invalidateQueries`, which bypasses `focusManager` entirely, so it alone would not have caught a regression in the pause mechanism itself; the `__invalidateQueries` variant (E-G2) is kept as-is. **2026-09-27 — CODE_REVIEW gate fifth pass, MEDIUM, self-amendment (this handover's own build).** The fourth-pass `history.state` marker `UnsavedChangesGuard.tsx` used to answer "is this popstate ours" (`__unsavedGuardBase`/`__unsavedGuard`) was found to be silently strippable by Next.js's own App Router: it patches `history.pushState`/`replaceState` globally, and its own reactive effect calls `history.replaceState({}, '', canonicalUrl)` for the CURRENT entry (discarding any custom key) whenever a `router.refresh()`, a Server Action, or a same-URL `<Link>`/`router.push()` resolves without the canonical URL actually changing -- silently, with no `popstate` fired, and no way for the guard to detect or recover once stripped: every subsequent Back press for the rest of that mounted instance's life would then stop showing the confirm dialog at all, exactly the class of failure NFR-USA-5 exists to prevent. Not currently triggered by `ProfileForm.tsx`/`NotificationPreferenceMatrix.tsx` (neither calls `router.refresh()` or a Server Action), but latent for a future page reusing this component. Fixed by moving the signal into `sentinelActiveRef`, a plain component-local ref that already existed -- since this component is the only thing that ever pushes or pops its own sentinel, and a same-entry `replaceState` never changes WHICH entry is current, the ref is immune to Next's own stripping, unlike anything stored in `history.state`. `useId()` and the mount-time base-marker stamp are removed entirely. New test reproduces Next's own `replaceState({}, ...)` call by hand and confirms the guard still reacts to the next Back press (confirmed it fails against the prior marker-based implementation). See `web/docs/decisions.md` ADR-FE-013's matching amendment, which also records a narrower, accepted instance of the existing double-Back race in the two-step replay (CODE_REVIEW gate fifth pass, SUGGESTION-strength). **2026-09-27 — TEST gate F1 (HIGH), self-amendment (this handover's own build).** `web/components/staff/IdleLock.tsx`'s two independent 401 paths — `useStaffSession()`'s own `sessionError` effect (locks on `StaffSessionIdleTimeoutError`) and the shared `setSessionErrorHandler` callback (locked on `session_idle_timeout`, otherwise cleared the cache and redirected on any other 401 code) — had an unsynchronized race the TEST gate reproduced directly (E-G2, `staff-auth.spec.ts:211`, mobile-360, `--workers=1`, ~1 failure in 3 isolated repeats): the real API ends a staff session on the FIRST request that detects the idle timeout and reports `session_invalid`, not `session_idle_timeout`, to every OTHER concurrent request racing it in the same window-focus refetch batch — a bystander page query is one of those. `client.ts` calls `setSessionErrorHandler` synchronously the instant that bystander's own response resolves (no React involved), while the session query's own `session_idle_timeout` needs its query to settle AND a React re-render to commit `lockedRef.current = true` — strictly more hops — so the bystander's `session_invalid` could reach the "not already locked" branch first and sign the user out, discarding the draft, even when the session query's own request is the one that actually holds `session_idle_timeout` for this idle window. Fixed by routing a non-idle 401 from the shared handler through one authoritative, serialized check instead of deciding from that response alone: `queryClient.fetchQuery` on `STAFF_SESSION_QUERY_KEY` (`resolveBystanderSessionInvalid`, `IdleLock.tsx`) reuses the session query's own in-flight fetch when one is already running for the same batch (`@tanstack/query-core`'s `Query.fetch()` returns the existing retryer's promise rather than starting a second request when `fetchStatus !== 'idle'`, verified against the installed 5.103.2) — so this performs no extra network call in exactly the scenario the race occurs in, and resolves to the one true outcome: `StaffSessionIdleTimeoutError` locks in place, same as the direct path; a resolved `null` (or any other error) means the session was genuinely invalid — revoked or unknown — and the original clear-and-redirect still runs, unchanged. A new deterministic unit test in `idle-lock.test.tsx` (`web/tests/components/staff/idle-lock.test.tsx`) gates the session query's own response behind a manually-released promise so the bystander's `session_invalid` is guaranteed to resolve first — confirmed failing (immediate redirect) against the pre-fix code and passing after; a second new test keeps the existing "genuine `session_invalid` still signs out" behaviour covered directly. See `docs/gates/HO-ACC-002-TEST.md` Finding F1 and `docs/test-cases/TC-HO-ACC-002.md` (E-G2/R3) for the original finding this closes. **2026-09-27 — CODE_REVIEW gate seventh pass, HIGH and LOW, self-amendment (this handover's own build). Correction to the immediately preceding TEST gate F1 row.** That row's "no extra network round trip" claim was correct only for the case it tested (the session query already mid-fetch), and incomplete: `resolveBystanderSessionInvalid`'s `queryClient.fetchQuery` call passed no `staleTime`, so it inherited the app-wide default (`app/providers.tsx`, `staleTime: 30_000`) — `fetchQuery` only calls `query.fetch()` at all when `query.isStaleByTime(staleTime)` is true, so whenever the session query was NOT mid-fetch but had succeeded less than 30s earlier (an entirely ordinary case, not merely a lab condition), the authoritative check skipped the network entirely and resolved straight to that cached "still signed in" answer, silently ignoring a session that had genuinely just ended or gone idle. Every existing test's `QueryClient` used `retry: false` only (an implicit `staleTime: 0`), so none of them exercised this branch, and F1's own fix therefore shipped with this HIGH still open. Fixed by passing `staleTime: 0` explicitly on that `fetchQuery` call, forcing `isStaleByTime` to `true` unconditionally so the check always attempts a fetch — this still dedupes onto an in-flight fetch exactly as before (the branch inside `Query.fetch()` that returns the existing retryer's promise is checked before any options are consulted, so it is unaffected by `staleTime`), and now also performs a genuine fresh fetch when the session query was not already in flight, rather than trusting a merely-recent cache entry. A new regression test in `idle-lock.test.tsx` uses a `QueryClient` with `defaultOptions.staleTime: 30_000` (matching `app/providers.tsx`), pre-seeds a fresh successful session read, and proves a bystander `session_invalid` for a session that has genuinely gone idle still locks — confirmed failing without `staleTime: 0` and passing with it. Also fixed, same pass (LOW): the redirect's `?next=` value is now captured from `window.location.pathname` before the `await`, not after — a losing bystander (one that resolves after the direct `session_idle_timeout` path, or another bystander, has already navigated) previously risked reading `/staff/sign-in` back off `window.location` and producing a useless `?next=%2Fstaff%2Fsign-in`. See `docs/gates/HO-ACC-002-CODE_REVIEW.md` "Seventh pass" for the original finding this closes. **2026-09-27 — ACCEPTANCE gate MEDIUM M6, self-amendment (this handover's own build).** Three out-of-§13 files were touched by earlier passes and recorded only in gate artifacts, never in this §1: `web/tests/unit/lib/register-schema.test.ts` (new; `tests/unit/**` is not in §13 — precedent: ARCHITECTURE third pass rated the sibling `pagination-caps.test.ts` the same way, and that file's own row above already covers it); the `window.__apiFetch` hook in `web/app/providers.tsx` (added by the TEST gate first pass, gated `NODE_ENV !== 'production'` identically to the pre-existing `__apiBaseUrl`/`__invalidateQueries` hooks beside it, reviewed for layering by CODE_REVIEW seventh pass); and `tests/CatEcommerce.IntegrationTests/AccountsIntegrationTestBase.cs` (an 11-line `ExtractSetCookieHeader` helper, added under the SECURITY gate item 7 authorisation already recorded above and in HO-ACC-001 §1, listed in that gate's own scope check but never named here). All three are sound and already reviewed on their merits by the gates named; only this record was missing. **Also declared here, per the same finding:** the E-X1/E-X2 route-coverage extension in `web/tests/e2e/accounts-a11y-responsive.spec.ts` — `ROUTES` grew from a 7-route sample (disclosed only in that file's own header comment) to all 19 §8.3 screens this handover owns, plus two new E-X1 cases ("with errors shown", "idle lock open") — closing the ACCEPTANCE gate's HIGH on A24/A25. See `docs/test-cases/TC-HO-ACC-002.md`'s own "Third pass" section for the full run evidence; no product code changed, since no axe or overflow violation was found on any of the newly-covered routes or states. *(append further rows here)* |
| Amended | **2026-10-13 -- customer landing is `/portal` (HO-POR-002; owner: "Land on /portal").** The default destination after a customer sign-in, and the redirect of a signed-in customer at `/`, change from `/portal/cats` to the `DEFAULT_CUSTOMER_LANDING` constant (`/portal`, `web/lib/routing/portal-links.ts`): one line each in `web/app/(auth)/sign-in/page.tsx` (the `sanitizeNextPath` fallback) and `web/app/page.tsx`. The allow-list and `sanitizeNextPath` behaviour are unchanged, so an explicit `?next=` still wins. `tests/e2e/home.spec.ts` asserts the new landing.<br><br>**2026-10-14 — Amended (integrator, HO-XCT-002 §5.3; backlog XCT rows 61 and 107); ratifies edits already made on `ho/XCT-002`, recorded after the fact as the integrator's OQ-1 duty (ACCEPTANCE gate `docs/gates/HO-XCT-002-ACCEPTANCE.md`, record-only BLOCKER on §19/§20.2).** Appends only; no existing text is reworded. (26) **§4.3 and ADR-FE-002 — the IndexedDB carve-out (HO-XCT-002 ADR-FE-141):** IndexedDB is permitted for the offline snapshot and the device flag only, and only from `web/lib/offline/**` (one database, two stores: `snapshot` and `device`). §4.3's "No token in JavaScript" rule stands for every other use: IndexedDB still never holds a session, a reset token, a password, a CAPTCHA answer or a draft, and the snapshot stores no customer phone number (HO-XCT-002 Amended (23)). The U-X4 / E-X3 storage audits stand unchanged for `localStorage` and `sessionStorage`, and gain "no session token, password, CAPTCHA answer or draft in IndexedDB either" (HO-XCT-002 U-X2 / E-X3). (27) **§11 (staff customer page):** `web/app/(staff)/staff/customers/[customerId]/page.tsx` gains `ErasePersonalDataPanel` (`web/components/retention/ErasePersonalDataPanel.tsx`, HO-XCT-002 §11.5); no other line of the page changes. (28) **§8.4:** on sign-out, on `session_invalid` and on a person change, the offline store is cleared as well as the query cache (HO-XCT-002 §8.5). As built, `SignOutButton` is not edited: `OfflineSnapshotSync` sees the staff session query leave the cache and wipes the store (HO-XCT-002 Amended (14)). HO-XCT-002 §20.2 makes (26) the feature's licence. Why: HO-XCT-002 §5.3. Each edit was made by the HO-XCT-002 build before this row existed (declared in its §1 Amended (17) and (22)); each matches the §5.3 text, lies inside HO-XCT-002 §13, and was reviewed by ARCHITECTURE (re-check 2 PASS, `0b69692a`). The integrator ratifies them as made; nothing is to be rebuilt. |

---

## 2. Objective

Build every screen a person uses to **become known to the system and prove who they are**: a cat
owner registering, signing in, recovering a password, managing their profile, addresses and
marketing consent, downloading their data and deleting their account; and a staff member signing in,
being locked out of an idle terminal without losing their work, an administrator creating staff
accounts and roles, a receptionist registering a walk-in, and an administrator merging duplicates.

It also **replaces the hardcoded identity** that HO-CAT-002 shipped (`lib/identity.ts`). After this
slice the browser holds no identity of its own: the API sets `HttpOnly` session cookies, and every
screen learns who is signed in by asking. That change reaches every existing staff and portal page,
which is why §8 is the part to read first.

Three constraints dominate, and most defects available here are failures of one of them:

1. **Nothing sensitive persists in the browser.** No password, reset token, CAPTCHA answer or draft
   in web storage, a URL, a TanStack cache entry, a log or an analytics call. Session tokens are never
   visible to JavaScript at all (ADR-FE-002).
2. **Screens do not disclose account existence.** One sign-in error; one reset confirmation; the
   duplicate-mobile message identical whatever else is on the account (ADR-FE-007).
3. **Every screen meets WCAG 2.1 AA at 360 px** — and the CAPTCHA, the idle lock and the preference
   matrix are the three places that is hardest.

---

## 3. In scope

### 3.1 Decisions inherited from HO-CAT-002 — do not revisit

| Decision | Choice |
|---|---|
| Framework | Next.js 15 App Router + React 19 + TypeScript (strict) |
| Styling / components | Tailwind CSS v4; shadcn/ui copied in and owned |
| Server state | TanStack Query v5 |
| Forms | react-hook-form + zod |
| Layering | `app/` → `components/` → `lib/hooks/` → `lib/api/`, one direction |
| Location | `web/`, sibling to `src/` |
| Error mapping | `applyValidationErrors` against `ValidationResponse`; `errorCode` never rendered |
| Tests | Vitest + Testing Library; Playwright + `@axe-core/playwright`; MSW |

### 3.2 Stories and screens

| Story | Screens / behaviour delivered here | Route |
|---|---|---|
| US-ACC-01 | Registration form with CAPTCHA; success state "ready to use" | `/register` |
| US-ACC-04 | Customer sign-in; sign-out; portal session guard | `/sign-in`, `(portal)` layout |
| US-ACC-05 | Forgot password (customer and staff variants); reset password from a link | `/forgot-password`, `/staff/forgot-password`, `/reset-password` |
| US-ACC-06 | Account area: profile, addresses, notification preferences | `/portal/account/{profile,addresses,notifications}` |
| US-ACC-13 | Personal-data download; account deletion with password confirmation; confirmation page | `/portal/account/data`, `/portal/account/data/delete`, `/account-deleted` |
| US-ACC-14 | Staff sign-in; staff session guard; sign-out; `lib/identity.ts` removed | `/staff/sign-in`, `(staff)` layout |
| US-ACC-08 | Idle lock overlay with in-place re-authentication that preserves unsaved form data; activity heartbeat | `(staff)` layout |
| US-ACC-07 | Staff account list, create, detail with role change; capability preview from the API; permission-denied state | `/staff/admin/staff-accounts/**` |
| US-ACC-10 | Walk-in form; customer detail with *incomplete* badge; existing-record hand-off on duplicate | `/staff/customers/new`, `/staff/customers/[customerId]` |
| US-ACC-11 | Duplicates report; merge review with explicit survivor choice; result | `/staff/customers/duplicates/**` |

---

## 4. Out of scope

Building any of it here is a **FAIL** at the acceptance gate.

### 4.1 Withdrawn — never build

OTP entry or resend screens, "verify your email/phone" banners or steps, a verification status badge,
2FA enrolment or challenge screens, recovery codes. The success, profile and sign-in screens must
**not mention verification, codes sent by SMS, or checking your inbox** — except the password-reset
confirmation, which is about a reset link.

### 4.2 Not this slice

| Excluded | Why |
|---|---|
| **Any backend change** | HO-ACC-001 owns `src/**`. A missing field or endpoint is a §20.7 stop |
| Audit log screens | FR-ADM-6; no read endpoint exists |
| Role guards or new screens for Cat Profiles | HO-CAT-002's. Its pages gain a session guard through the shared `(staff)`/`(portal)` layouts **only** (§8.3) — no page inside them changes |
| Orders, invoices, visits, balances anywhere | Contexts do not exist — stated as "not yet recorded" (ADR-FE-012) |
| A CAPTCHA vendor widget, an audio CAPTCHA | ADR-FE-006 |
| Client-side breached-password list, `zxcvbn`, a strength meter | ADR-FE-005 |
| A "check if this number is free" lookup before submit | An enumeration oracle (ADR-FE-007) |
| Social login, SSO, a "remember me" toggle | Not in any story; session length is the server's |
| Dark mode, i18n | Not required |

### 4.3 Explicitly forbidden shapes

- **No global store** — no Redux, Zustand, Context-as-store. Sessions are TanStack Query state
  (ADR-FE-003). React context is allowed **only** to pass the idle-lock controller down the staff
  layout (§11.9) — it holds no server data.
- **No token in JavaScript.** No `localStorage`, `sessionStorage`, IndexedDB, Cache API or
  JS-written cookie holding a session, a reset token, a password, a CAPTCHA answer or a draft.
- **No `useQuery`/`useMutation` inside a component** — hooks live in `lib/hooks/`.
- **No second `fetch` call site** — `lib/api/client.ts` only.
- **No hardcoded copy of SRS Appendix D** — the catalogue comes from `GET /api/staff-roles`.
- **No client-side authorization decision.** Hiding a button is presentation; the server's `403` is
  the only "not permitted" the UI recognises (ADR-FE-009).
- **No `dangerouslySetInnerHTML`.**

---

## 5. Prerequisites

| Required | State |
|---|---|
| Node.js 20 LTS+ and npm | ❌ not on PATH at `4007225` (HO-CAT-002 §20.1) |
| **The `web/` scaffold from HO-CAT-002 §12.1** | ❌ **not built.** This slice cannot start without it — §20.1 |
| The HO-ACC-001 API | ❌ not implemented. Build against MSW per §5.1 |

### 5.1 Build against the contract, then prove it against the API

HO-ACC-001 §12 is complete and authoritative: routes, request and response shapes, error codes,
cookie names, the CSRF header, the `401`/`403`/`429` bodies. **Transcribe `lib/api/types.ts` from it**,
generate MSW handlers from it (§12.4), and:

- **Mocks may not contain a field HO-ACC-001 does not specify.** A convenient extra field produces a
  screen that works in development and breaks on integration.
- **Every §16 case is re-run against the real API** once each HO-ACC-001 phase lands, and the test
  sheet records both runs. A case that passed only against a mock verified the mock.

---

## 6. Governing documents

| Document | What you need from it |
|---|---|
| [`HO-ACC-001`](HO-ACC-001.md) | **§12.4 the endpoint table and response rules R1–R8; §12.5 cookies and CSRF; §12.6 CAPTCHA consumption; §12.7 limits; §12.8 actions; §10.3 the codes to map.** Read before any `lib/api/` code |
| [`HO-CAT-002`](HO-CAT-002.md) | §3.1 stack; §8 layering, routes, state, accessibility; §9 design conventions; §10 dependencies; §11.2 client; §11.8 error mapping; §12.1 scaffold. **Inherited in full** |
| [`docs/USER_STORIES.md`](../USER_STORIES.md) §1 | The acceptance criteria quoted in §18 |
| [`docs/SRS.md`](../SRS.md) §3.1, §6.4, Appendix D | UI-1/3/4/6; NFR-USA-1/4/5/6; the matrix the capability preview renders (never transcribed) |
| `.claude/agents/frontend-developer.md` | Component size, tokens, flag-don't-introduce libraries |

**The `.ai/` documents describe the .NET backend only** and do not govern this diff — same position
as HO-CAT-002 §6.

---

## 7. The SRS requirements, as build rules

| Req | Rule | Enforced in |
|---|---|---|
| **UI-1** | Every screen works at 360 / 768 / 1024 / 1440 px; 360 is the floor | E-X2 |
| **UI-3** | Destructive actions confirm, **stating exactly what will be affected** | Account deletion (§11.8), merge (§11.13), removing an address, removing a role. **Not** registration or sign-in |
| **UI-4** | Lists paginate and filter | Staff accounts, duplicates |
| **UI-6 / NFR-USA-6** | WCAG 2.1 AA: labels, announced errors, keyboard, visible focus, 4.5:1, colour never the only signal | §8.6; every U-x "a11y" row; E-X1 |
| **NFR-USA-1** | Register + add a cat + book in under 3 minutes unassisted | Registration is five fields and one short CAPTCHA; no verification step; no confirm-password round trip. E-B8 |
| **NFR-USA-4** | Errors say what happened and what to do; no codes, ids or stack traces | §11.14; U-X2 |
| **NFR-USA-5** | Entered data survives validation errors and accidental navigation | Every form. **Exceptions: passwords and CAPTCHA answers are cleared** (security / single-use). `UnsavedChangesGuard` on account forms; the idle lock preserves staff forms |
| **NFR-SEC-4** | Short-lived signed tokens, CSRF defence, strict CSP | ADR-FE-002; the client sends `X-CSRF-Token`; nothing weakens the scaffold's CSP |
| **FR-ACC-3** | One generic sign-in failure | ADR-FE-007 |
| **C-9** | Staff actions attributable to a named user | A draft started by staff A is never restored under staff B (ADR-FE-010) |

---

## 8. Frontend architecture

### 8.1 Where it lives

Inside HO-CAT-002's `web/`. Extend; never recreate (§20.1).

### 8.2 Layering

Unchanged from HO-CAT-002 §8.2. New API modules, one per area:

```
lib/api/auth.ts          captcha, register, customer sign-in/out/session, reset request/confirm
lib/api/account.ts       /me profile, addresses, preferences, export, deletion
lib/api/staff-auth.ts    staff sign-in/out, session status, activity, staff reset request
lib/api/staff-admin.ts   staff accounts, role catalogue
lib/api/staff-customers.ts  walk-in, lookup, detail, duplicates, merge
```

### 8.3 Route map

```
web/app/
  (auth)/                          ⭐ NEW public group — minimal shell, no navigation, no identity
    layout.tsx
    register/page.tsx                                      US-ACC-01
    sign-in/page.tsx                                       US-ACC-04
    forgot-password/page.tsx                               US-ACC-05 (customer)
    reset-password/page.tsx                                US-ACC-05 (link target, both kinds)
    account-deleted/page.tsx                               US-ACC-13
    staff/
      sign-in/page.tsx                                     US-ACC-14
      forgot-password/page.tsx                             US-ACC-05 / 14 (staff)

  (portal)/
    layout.tsx                     ⚠ MODIFIED — CustomerSessionGuard + sign-out in the header
    portal/account/
      layout.tsx                   ⭐ account tab shell (ADR-FE-008)
      page.tsx                     → redirect to profile
      profile/page.tsx                                     US-ACC-06
      addresses/page.tsx                                   US-ACC-06
      notifications/page.tsx                               US-ACC-06
      data/page.tsx                                        US-ACC-13 export
      data/delete/page.tsx                                 US-ACC-13 deletion

  (staff)/
    layout.tsx                     ⚠ MODIFIED — StaffSessionGuard, IdleLock, sign-out, name in header
    staff/
      admin/staff-accounts/
        page.tsx                   list                    US-ACC-07
        new/page.tsx               create                  US-ACC-07
        [staffAccountId]/page.tsx  detail + roles          US-ACC-07
      customers/
        new/page.tsx               walk-in                 US-ACC-10
        [customerId]/page.tsx      detail                  US-ACC-10 / 11
        duplicates/page.tsx        report                  US-ACC-11
        duplicates/review/page.tsx merge review            US-ACC-11
```

> **`/staff/sign-in` lives in `(auth)`, not `(staff)`.** Route groups do not change the URL, so
> `(auth)/staff/sign-in` and `(staff)/staff/cats` coexist. Putting sign-in under the `(staff)`
> layout — whose job is to require a session — is how a redirect loop is built.

> **Superseded — §1 Amended, 2026-09-26 (owner decision).** `app/page.tsx` no longer redirects
> unconditionally to `/staff/cats`, or to whichever area registered a nav item first — it branches
> on which session cookie is present (staff → `/staff/cats`, customer → `/portal/cats`, neither →
> `/sign-in`). The note above about `/staff/sign-in` living in `(auth)` still holds; only the root
> redirect's own target changed.

### 8.4 Session model — ADR-FE-002, ADR-FE-003

- The API sets `pc_session` / `pc_staff_session` as **`HttpOnly`** cookies. **JavaScript never sees a
  session token**, so there is nothing to store, leak or clear by hand.
- ~~**Same origin.** The browser talks only to the Next.js origin; `next.config.ts` rewrites
  `/api/:path*` to the API origin from the **server-side** env var `API_ORIGIN`. Cookies are then
  first-party and `SameSite=Lax` works with no credentialed CORS.~~ **Superseded — §1 Amended,
  2026-09-26 (owner decision): the browser calls the API directly, cross-origin, with
  `credentials: 'include'` (this is what `lib/api/client.ts` already did throughout this
  handover's build — the same-origin proxy above was never actually implemented). `SameSite=Lax`
  cookies require same-**SITE**, not same-origin: separate subdomains of one registrable domain in
  production, the same scheme as the web app in development (`web/README.md`).
  `NEXT_PUBLIC_API_BASE_URL` stays (§12.2's "replace it with `API_ORIGIN`" no longer applies).
- **CSRF.** `lib/api/client.ts` reads the non-HttpOnly `pc_csrf` cookie and sends it as
  `X-CSRF-Token` on every `POST`/`PUT`/`DELETE` (HO-ACC-001 R7).
- **Who is signed in** is server data:

| Query key | Source | Used by |
|---|---|---|
| `['session', 'customer']` | `GET /api/auth/session` → view model, or `null` on `401` | `CustomerSessionGuard`, portal header |
| `['session', 'staff']` | `GET /api/staff/session` → status, or `null` on `401` | `StaffSessionGuard`, idle countdown, staff header |

Rules:
- Sign-in success → **invalidate** the session key, *then* navigate. Navigating first shows the guard
  a stale `null` and bounces back to sign-in.
- Sign-out, deletion, or any `session_invalid` → **`queryClient.clear()`** — the whole cache, not
  one key. Leaving one customer's cat data in memory for the next person at the same browser is a data
  leak.
- Any `401 session_invalid` from any call → clear and route to the right sign-in with `?next=`.
- A `401 session_idle_timeout` → **do not clear and do not navigate** — the idle lock takes over
  (§11.9).

### 8.5 State

| Kind | Where |
|---|---|
| Sessions | TanStack Query keys above |
| CAPTCHA challenge | `['auth', 'captcha']`, `staleTime: Infinity`, `gcTime: 0`, `retry: false`, no refetch on focus/reconnect; `refetch()` on "New code", after **every** failed submit, and ~10 s before expiry |
| Profile, addresses, preferences, staff lists, customer detail, duplicates | TanStack Query, keys per resource; mutations invalidate their resource |
| Form values | react-hook-form + zod |
| Reset token | Component state only, read once from the URL fragment (ADR-FE-007) |
| Idle-lock state | `useState` in `IdleLock`, exposed to children only through the lock controller context |
| `?next=` destination | URL |

### 8.6 Accessibility — UI-6, non-negotiable

HO-CAT-002 §8.5 in full, plus:

- **Real `<label>` for every input**; placeholder is not a label. `autocomplete`: `name`, `tel`,
  `email`, `username` (the sign-in identifier), `current-password`, `new-password`, and `off` on the
  CAPTCHA.
- **Errors announced**: `aria-live="polite"`, `aria-describedby`, `aria-invalid`. Focus to the first
  invalid field on failure; to the success heading on success.
- **Password requirements readable before typing**, linked by `aria-describedby`.
- **Show/hide password** is a real `<button>` whose accessible name reflects its state.
- **CAPTCHA**: `alt` states purpose (*"Security check: an image of 5 letters and numbers"*) and never
  the answer; visible help text names the **reception alternative** (WCAG SC 1.1.1, SRS Q-17); a new
  challenge is announced in a live region.
- **Idle lock is a modal dialog**: focus trapped inside, background `inert` and `aria-hidden`, the
  heading announced, `Escape` does **not** dismiss it.
- **Preference matrix** is a real `<table>` with row and column headers; locked cells are disabled
  checkboxes with visible text "Always on", never colour alone.
- **Submit is never disabled to express invalidity** — only while in flight, with its busy state
  announced.

---

## 9. ADRs to record during this build, and open questions

Record in the frontend decision log `web/docs/decisions.md` (create it if HO-CAT-002 has not).
`.ai/decisions/adr.md` is backend-only and forbidden scope. If HO-CAT-002 has already used
`ADR-FE-001…`, renumber these upward and note it in a §1 Amended row.

| ADR | Decision | Why |
|---|---|---|
| **ADR-FE-001** | Three route groups: `(auth)` public with a minimal shell; `(portal)` behind the customer session guard; `(staff)` behind the staff session guard and idle lock | HO-CAT-002 had two groups, both assuming an identity |
| **ADR-FE-002** | ~~Session transport is `HttpOnly` cookies over a same-origin proxy (`next.config.ts` rewrites)~~ — **corrected by the owner, 2026-09-26 (§1 Amended): direct cross-origin `fetch`, `credentials: 'include'`, separate subdomains in production, same scheme in development.** CSRF via `pc_csrf` → `X-CSRF-Token`. The browser never holds a token | Survives reload (an in-memory bearer token does not), and XSS cannot read it. Matches HO-ACC-001 ADR-031. Cross-origin needs same-**SITE**, not just CORS — the proxy proposal was never actually built (`lib/api/client.ts` kept HO-CAT-002's own `NEXT_PUBLIC_API_BASE_URL` design throughout), and the owner's fix is at the deployment/env layer, not the client |
| **ADR-FE-003** | Sessions are TanStack Query state (`['session', …]`), never a store; sign-out clears the whole cache | No new library; one source of truth |
| **ADR-FE-004** | `lib/identity.ts` is **deleted**. MSW mode supplies identities through mocked session endpoints, not a hardcoded module | A hardcoded identity that survives into the real build is an authentication bypass in waiting |
| **ADR-FE-005** | Password length checked client- and server-side; breach check server-side only | NFR-SEC-1 is a server obligation; a client list leaks and bloats |
| **ADR-FE-006** | The CAPTCHA is the backend's self-hosted image; the client never judges an answer and **fetches a new challenge after every failed submit** | Challenges are single-use server-side; skipping the refetch guarantees a second rejection |
| **ADR-FE-007** | **Non-disclosing screens.** One sign-in error for every failure; one client-owned reset confirmation rendered on every `200` (the response body is never rendered); the reset link is `/reset-password#token=…`, read once from the fragment, stripped from the address bar, page served `Referrer-Policy: no-referrer`; one "invalid or expired link" state | The server's non-disclosure is worthless if the screen varies |
| **ADR-FE-008** | The portal account area is a route-based tab shell; compliance-relevant settings (preferences, deletion) save **explicitly**, never on toggle | Deep-linkable; consent must be a deliberate act |
| **ADR-FE-009** | The role catalogue and capability matrix are rendered **from the API**; UI role-gating is presentation only; a server `403` renders `PermissionDeniedState` | FR-ADM-2, NFR-SEC-2 |
| **ADR-FE-010** | **The idle lock is an in-place overlay, not a route.** The page stays mounted, so every form's react-hook-form state survives re-authentication with no change to any form. Drafts live only in memory. After re-auth, the lock lifts **only** if `staffAccountId` matches; a different person discards the page | US-ACC-08 AC2 for every existing and future form; C-9 |
| **ADR-FE-011** | Merge integrity: no default survivor; one request to the merge endpoint, never client-orchestrated cat moves; the review is built from a **fresh read**, not the report row | UI-3, FR-ACC-10 |
| **ADR-FE-012** | Data the system does not record (orders, invoices, balances) is **stated as not recorded**, never shown as zero | A number is a claim |

### 9.1 Open questions

| # | Question | Owner | Default |
|---|---|---|---|
| OQ-1 | Mobile format hint text | Owner (HO-ACC-001 OQ-1) | "Bangladesh mobile, e.g. 01712 345678" |
| OQ-2 | Reception wording for the CAPTCHA alternative (hours, phone?) | Owner (SRS Q-17) | "Or register in person at the clinic reception." |
| OQ-3 | Where does a customer land after sign-in with no `?next=`? | Owner | `/portal/cats` |
| OQ-4 | Idle warning lead time | Owner | Warning 2 minutes before `idleExpiresOn` |
| OQ-5 | Walk-in completion wording to give the customer at the desk | Owner (SRS Q-16) | "Ask the customer to choose 'Forgot password' online with this phone number to set their password." |

---

## 10. Dependencies

**No new package.** Everything is on HO-CAT-002 §10's list: `next`, `react`, `@tanstack/react-query`,
`react-hook-form`, `zod`, `@hookform/resolvers`, shadcn/ui (`@radix-ui/*` — Dialog, Tabs, Checkbox,
Table, Alert, Badge), `lucide-react`, `date-fns`, `msw`, Vitest/Testing Library, Playwright, axe.

**Adding any of these is a §20.5 stop**: NextAuth/Auth.js or any auth library, a JWT decoder, a
CAPTCHA SDK, `zxcvbn`, `libphonenumber-js`, an idle-timer package, a state library.

---

## 11. Specified changes — components and screens

Every component: under ~150 lines (extract field rows), the three distinct loading / empty / error
states HO-CAT-002 §9 requires, and the §8.6 accessibility rules.

### 11.1 Contract types — `lib/api/types.ts` (extend)

Transcribe from HO-ACC-001 §12.3–§12.4, same names and nullability:
`CaptchaChallenge`, `RegisterCustomerRequest`, `SignInRequest`, `CustomerSession`, `StaffSessionStatus`,
`SessionError` (`error: 'session_invalid' | 'session_idle_timeout'`, `message`, `idleTimeoutMinutes?`),
`PasswordResetRequest`, `ResetPasswordRequest`, `CustomerProfile`, `UpdateCustomerProfileRequest`,
`DeliveryAddressBook`, `DeliveryAddress`, `DeliveryAddressRequest`, `NotificationPreferences`
(cells with `isGranted`, `isEditable`, `isAlwaysOn`), `NotificationPreferenceChange`,
`PersonalDataExport` (`schemaVersion: 1`), `DeleteAccountRequest`, `StaffRoleCatalogue`,
`StaffAccountSummary`, `StaffAccount` (`hasPassword`), `CreateStaffAccountRequest`,
`ChangeStaffRolesRequest`, `PagedResult<T>` `{ items, pageNumber, pageSize, totalCount }`,
`StaffCustomer` (`recordStatus: 'Complete' | 'Incomplete'`, `registrationChannel`),
`StaffCustomerSummary`, `DuplicateCustomerGroup`, `MergeCustomerRequest`, `MergeCustomerResult`.

A mismatch with HO-ACC-001 must be a **compile error** when the real API lands.

### 11.2 API client — `lib/api/client.ts` (HO-CAT-002's file, extended)

The one sanctioned change to HO-CAT-002's client:

- Base URL is **same-origin `/api`**; `NEXT_PUBLIC_API_BASE_URL` and the `lib/identity.ts` headers are
  removed.
- On `POST`/`PUT`/`DELETE`: read the `pc_csrf` cookie and send `X-CSRF-Token`.
- New typed errors: `401` → `ApiUnauthorizedError` carrying the `SessionError` body (or the sign-in
  `invalid_credentials` body); `403` → `ApiForbiddenError` (`ActionNotPermitted` or
  `csrf_token_invalid`); `429` → `ApiRateLimitedError` carrying `retryAfterSeconds` parsed from
  `Retry-After`.
- A registered `onSessionError` callback (set once by `QueryProvider`) implements §8.4's
  clear-and-route / idle-lock split, so no page handles session failure itself.

Existing behaviour for `400`, `404`, `409`, `5xx` is unchanged.

### 11.3 Shared auth pieces — `components/auth/`

| Component | Notes |
|---|---|
| `AuthCard` | `(auth)` card: heading receives focus on arrival |
| `PasswordField` | Show/hide; requirements text before typing (*"At least 10 characters. Common and previously-breached passwords are not accepted."*) when `purpose="new"`; no requirements when `purpose="current"` |
| `CaptchaField` | Fixed-size image, **"New code"** text button, input (`autoComplete="off"`, `autoCapitalize="characters"`, `spellCheck={false}`), help text with the reception alternative, live-region announcement on every new challenge, a "couldn't load the security check" state with retry |
| `RetryNotice` | Renders `429`: *"Too many attempts. Please wait {n} minutes and try again."* with `n = ceil(retryAfterSeconds / 60)`, minimum 1. Never raw seconds, never the header name. Submit stays enabled |
| `SignInForm` | `audience: 'customer' \| 'staff'`; identifier ("Mobile number or email"), password; one error region; links to the matching forgot-password route |
| `SignOutButton` | Calls the matching sign-out, then `queryClient.clear()`, then routes to sign-in |

### 11.4 Registration — US-ACC-01 · `components/auth/RegisterForm.tsx`, `RegistrationSuccess.tsx`

Fields, in order: **Full name**, **Mobile number**, **Email**, **Password**, **Security check**.
Mobile and email are each labelled optional *in visible text*, with a line above them: *"Give a
mobile number, an email, or both — you'll use one to sign in."*

- zod: name required; **at least one of mobile/email** (a form-level message linked to both fields);
  email shape when present; password ≥ 10 with the message naming the minimum and the actual length;
  CAPTCHA answer required. **No mobile regex until OQ-1 is confirmed** — shape only, the server
  normalises.
- Validate on submit, then on blur. Submit disabled only in flight; double-submit prevented.
- **After every failed submit** — any `400`, `429`, `5xx` or network error — clear the password and
  CAPTCHA answer and `refetch()` the challenge. A client-side validation failure does **not** spend the
  challenge.
- Error mapping (§11.14): `MobileNumberAlreadyRegistered` → mobile field, with **Sign in** and
  **Reset password** links; `EmailAddressAlreadyRegistered` → email field, same links; each message
  **one fixed string**, never naming the other identifier; `PasswordTooShort` and `BreachedPassword`
  → password field, **textually distinct**; `CaptchaInvalid` → CAPTCHA field, with the new image
  already on screen.
- Success: heading *"Your account is ready"*, receives focus; says the account can be used now;
  primary action **Sign in** (to `/sign-in`). **No mention of verification, codes or checking email.**
  Does not echo the password or the full mobile number.

### 11.5 Customer sign-in and guard — US-ACC-04

`/sign-in` renders `SignInForm audience="customer"`. On `401`: one message — *"That mobile number or
email and password don't match an account. Check them and try again, or reset your password."* —
identical for every failure (ADR-FE-007). The password is cleared; the identifier kept. `429` →
`RetryNotice`. Success → invalidate `['session','customer']`, then go to `?next=` (only a
same-origin path starting `/portal/`; anything else is ignored — an open-redirect guard) or OQ-3's
default.

`CustomerSessionGuard` (in `(portal)/layout.tsx`): pending → a full-height loading state (**never
flash portal content**); `null` → `redirect('/sign-in?next=' + encodeURIComponent(pathname))`;
present → render, with the customer's name and `SignOutButton` in the header.

### 11.6 Password reset — US-ACC-05 / US-ACC-14

`ForgotPasswordForm` (`audience` prop; `/forgot-password` and `/staff/forgot-password`): one
identifier field. On **any `200`** render the client-owned confirmation — *"If an account matches,
we've sent a link to reset the password. It expires in 15 minutes."* — **never the response body**.
`429` → `RetryNotice`.

`/reset-password` (`ResetPasswordForm`):
1. On mount read `token` from `location.hash`, keep it in component state, then
   `history.replaceState(null, '', '/reset-password')` to strip it. No token → the invalid-link state.
2. The page sets `export const metadata = { referrer: 'no-referrer' }`.
3. **No token pre-check** — the form renders from the link alone; validity is decided on submit.
4. Fields: new password (`PasswordField purpose="new"`). On `204` → *"Your password has been set.
   Sign in with it now."* with links to **both** sign-in pages (the link does not say which kind of
   account it was for, and must not).
5. `InvalidPasswordResetToken` → one state: *"This link is invalid or has expired."* with a link to
   request another. `BreachedPassword` / `PasswordTooShort` → field errors; the token stays in state
   so the user can try another password.

### 11.7 Account area — US-ACC-06 · `app/(portal)/portal/account/**`, `components/account/**`

`AccountShell` (layout): tab navigation as links (`aria-current="page"`), tabs **Profile · Addresses ·
Notifications · Your data**.

| Screen | Components | Behaviour |
|---|---|---|
| Profile | `ProfileForm` | Name; email (read-only with an explanation when `canEditEmail` is false — *"Your email is how you sign in. Contact the clinic to change it."*); primary mobile always read-only with the same kind of explanation; up to 3 additional numbers (add/remove rows). Explicit **Save**; `UnsavedChangesGuard` |
| Addresses | `AddressList`, `AddressCard`, `AddressForm` (dialog) | Label, lines, city, district, postal code, instructions. "Default" badge with text. **Set as default** on non-defaults. Removing the default while others exist shows the server's reason; removing any address confirms (UI-3) naming the label. Limit reached (10) → the add button explains why rather than disappearing |
| Notifications | ⭐ `NotificationPreferenceMatrix` | A `<table>`: channels as columns, categories as rows, **every cell from the API**; `isAlwaysOn` cells are disabled and read "Always on"; editable cells are checkboxes. Explicit **Save changes**, which sends only changed cells; a success status message. Never autosave (ADR-FE-008) |

`UnsavedChangesGuard`: `beforeunload` plus an in-app navigation confirm when the form is dirty.

### 11.8 Your data and deletion — US-ACC-13

- `/portal/account/data` — `PersonalDataExportCard`: explains what is included; **Download my data**
  → `POST` export → save the JSON as `purrcare-my-data.json` via a Blob URL revoked immediately
  after. The data is **not** put into the query cache. A link to **Delete my account**.
- `/portal/account/data/delete` — `DeleteAccountForm`: states **exactly** what happens (UI-3):
  personal details, contact details and addresses are removed; you will be signed out and cannot
  sign in again; clinical and billing records are kept for the legally required period with your name
  replaced by "Anonymised customer"; this cannot be undone. Offers the download first. Requires the
  **current password** and a checkbox *"I understand this cannot be undone"*. `ReauthenticationFailed`
  → field error; `429` → `RetryNotice`. Success → `queryClient.clear()` →
  `/account-deleted` (an `(auth)` page confirming it, with no identity).

### 11.9 Staff sign-in, guard and idle lock — US-ACC-14, US-ACC-08

- `/staff/sign-in` — `SignInForm audience="staff"`, the same single-error rule; on success go to
  `?next=` (only `/staff/…`) or `/staff/cats`. A link to `/staff/forgot-password` labelled **"Set or
  reset password"** — a new staff member's first password (US-ACC-14 AC3).
- `StaffSessionGuard` (in `(staff)/layout.tsx`): like the customer guard, redirecting to
  `/staff/sign-in?next=…`; header shows the staff member's name and `SignOutButton`. **This replaces
  `lib/identity.ts`** for every HO-CAT-002 staff page, unchanged otherwise.
- ⭐ `IdleLock` (wraps `{children}` inside the guard):
  - **Warning** at `idleExpiresOn − 2 min` (OQ-4), computed against `serverTime` from the status,
    never the client clock: a polite live-region message *"You'll be signed out for inactivity in 2
    minutes"* with a **Stay signed in** button that calls the activity endpoint.
  - **Activity**: real interaction (keydown, pointerdown, input) posts `/api/staff/session/activity`
    at most once per 60 s. **Never on a timer** — a timer would keep an abandoned terminal alive.
    Status is re-read with `GET /api/staff/session`, which never extends.
  - **Lock** on a `session_idle_timeout` from any call, or when the countdown reaches zero: render a
    modal dialog **above** the page (the page stays mounted; background `inert`), showing *"You were
    signed out after {n} minutes of inactivity. Sign in again to continue — your unsaved work is still
    here."* and an embedded staff `SignInForm`.
  - **Unlock**: after a successful re-sign-in, re-read the status; if `staffAccountId` equals the one
    held before the lock → remove the overlay; the user saves again **knowingly** (the refused request
    is **never replayed automatically**). If it differs → *"Signed in as a different person. The
    previous work on this screen has been discarded."* then `queryClient.clear()` and a full reload
    of the current route.
  - `beforeunload` warning while locked. Nothing about the draft is copied, serialised or stored.
- `IdleLockContext` (`StaffLockProvider`, wrapping `StaffSessionGuard` and `IdleLock` in
  `(staff)/layout.tsx` — a context `IdleLock` provided itself could never reach its own parent
  guard) exposes `{ locked, lock, unlock }`: `locked` lets `StaffSessionGuard` stop treating a
  `session_invalid`/`null` session read as "signed out" while the lock's own re-authentication is
  what actually resolves things (the real API ends the session on the first idle detection, so
  every later read naturally reports `session_invalid`, not `session_idle_timeout` again);
  `lock()`/`unlock()` are called from `IdleLock`'s own `client.ts` session-error handler and its
  `handleReSignInSuccess`. **ARCHITECTURE gate fourth pass** — beyond the boolean, `lock()`/
  `unlock()` also pause and resume TanStack's own global `focusManager`/`onlineManager` singletons
  (`setEventListener` swapped to a no-op while locked, restoring each manager's own default setup
  on unlock and on unmount while still locked), so a focus/reconnect event can never refetch an
  already-mounted page's own query while locked, independently of the session query — no server
  data is held either way; this is UI-only state plus a global, framework-level pause, not a cache
  of anything the API returned. See ADR-FE-010's fourth-pass amendment (`web/docs/decisions.md`)
  for the two rejected alternatives and why.

### 11.10 Staff accounts — US-ACC-07 · `components/admin/**`

| Screen | Components | Behaviour |
|---|---|---|
| List | `StaffAccountTable` | Paginated (UI-4), search by name/identifier, filter by role; columns name, identifier, roles, "Password set" yes/no (`hasPassword`) |
| Create | ⭐ `StaffAccountForm` | Name, mobile and/or email (at least one), ⭐ `RoleAssignmentField` (checkbox group from `GET /api/staff-roles`, skeleton while loading, at least one required), ⭐ `CapabilityMatrixPreview` showing the union of selected roles from the API data, captioned "Indicative — from SRS Appendix D". Success: *"Account created. Ask {name} to choose 'Set or reset password' on the staff sign-in page to set their password."* |
| Detail | `StaffAccountDetail`, `RoleAssignmentField` | Change roles — sends the **whole set**; removing a role confirms (UI-3) naming it and noting the member will be signed out; `LastAdministratorCannotBeRemoved` shown as the server's message |
| Any | `PermissionDeniedState` | Rendered on `403 ActionNotPermitted`: *"You don't have permission to do this. Ask an administrator if you need access."* — names no role |

Navigation to admin screens may be hidden for non-administrators **as presentation**; direct URL
access still renders `PermissionDeniedState` from the server's `403`.

### 11.11 Walk-in — US-ACC-10 · `components/customer/**`

- `/staff/customers/new` — `WalkInCustomerForm`: full name, mobile. On `201` → the customer's detail
  page. On `MobileNumberAlreadyRegistered` → call `GET /api/staff/customers?mobile=` and render
  `ExistingCustomerNotice` with a link to the record(s) found — staff are authorised, so this is a
  feature. The form keeps its values.
- `/staff/customers/[customerId]` — `CustomerDetail` with ⭐ `CustomerStatusBadge` (`Incomplete` shows
  text and an icon, never colour alone) and `IncompleteCustomerNotice` carrying OQ-5's wording **plus**
  a note while reset delivery is Development-only (HO-ACC-001 §4.3): *"Online password setup isn't
  available yet; the customer can be served in the clinic."* Merged records show "Merged into …" with a
  link. Cat count.

### 11.12 Duplicates report — US-ACC-11

`/staff/customers/duplicates` — `DuplicateGroupList`: paginated groups, each listing its records
(name, identifiers, registration channel, record status) and **their cats** (with a "deceased" badge).
Orders, invoices and balances are **not columns** — each group carries ⭐ `NotYetRecordedNotice`:
*"Orders, invoices and balances aren't recorded in the system yet, so they can't be shown or moved."*
(ADR-FE-012). **Review merge** per group → the review page with the group's ids in the URL.

### 11.13 Merge review — US-ACC-11 · `/staff/customers/duplicates/review`

⭐ `MergeReview`:
1. **Fresh read** of every record in the group and its cats on arrival (ADR-FE-011 (c)).
2. Radio group **"Keep which record?"** — **nothing pre-selected**, ever.
3. Reason (required).
4. A confirmation that names the survivor and the retired record by name and lists **every cat that
   will move**, deceased included, plus the not-recorded notice (UI-3).
5. **One** `POST …/merge`. `200` → result: survivor linked; `isReapplication: true` → *"This merge had
   already been applied; any remaining cats have now been moved."* A `5xx` → *"The merge may be
   partly complete. Run it again with the same choice to finish it."* with a **Run again** button that
   re-sends the identical request (the server's recovery path, HO-ACC-001 ADR-039).
6. `CustomersNotDuplicates` / `CustomerAlreadyMerged` → the server's message and a link back.

The client never calls a cat endpoint during a merge (ADR-FE-011 (b)).

### 11.14 Error mapping — `lib/api/errors.ts` (extend)

Append `propertyName → field` entries for every command in HO-ACC-001 §11.1 to the **one** mapping
table. Unmatched `propertyName` → a **form-level** message, never swallowed. **`errorCode` is never
rendered** (U-X2). `5xx` → our message with retry, never the server's text or a correlation id.

---

## 12. Specified changes — config and mocks

### 12.1 Scaffold

None created here. The changes to HO-CAT-002 files are **only** those named in §13.

### 12.2 Environment and proxy

> **Superseded — §1 Amended, 2026-09-26 (owner decision).** None of the three points below were
> built — `lib/api/client.ts` kept HO-CAT-002's own `NEXT_PUBLIC_API_BASE_URL`/direct-`fetch`
> design throughout this handover instead. The owner has since made that the permanent choice
> (separate subdomains in production, same scheme in development — `web/README.md`), so this
> section is kept only as a record of the original, not-built proposal.

- ~~`next.config.ts` — add `rewrites()`: `{ source: '/api/:path*', destination: \`${process.env.API_ORIGIN}/api/:path*\` }`.
  `API_ORIGIN` is **server-side** (no `NEXT_PUBLIC_` prefix). The API dev certificate must be trusted
  (`dotnet dev-certs https --trust`), or set `NODE_EXTRA_CA_CERTS` for the Next server.~~
- ~~`.env.local.example` — replace `NEXT_PUBLIC_API_BASE_URL` with `API_ORIGIN=https://localhost:7xxx`;
  keep `NEXT_PUBLIC_USE_MOCKS=true`.~~ **No secret, no CAPTCHA test answer in any `NEXT_PUBLIC_*`
  variable** — it would ship in the bundle — still applies regardless.
- ~~CORS is no longer involved in the browser path (same origin).~~ CORS **is** involved (real
  cross-origin calls); `Cors:AllowedOrigins` on the API must include the web app's own origin.

### 12.3 E2E against the real API

Start the API in `Development` with, in its git-ignored `appsettings.Development.json`:
`Captcha:TestAnswer`, `DevUser:Enabled: false`, `Account:WebBaseUrl: http://localhost:3000`. The
Playwright suite reads the CAPTCHA answer from `E2E_CAPTCHA_ANSWER` (test-runner env only) and the
reset link from the API's Development log. Seed a staff administrator through a Playwright global
setup that uses the API.

### 12.4 Mocks — `web/mocks/handlers/accounts.ts`, `staff-accounts.ts`, `staff-customers.ts`

Generated from HO-ACC-001 §12.4. Session endpoints read a mock cookie jar so sign-in/out and the
guards behave. Fixtures must include every branch a §15/§16 case takes:

- CAPTCHA: new id per call; the registration mock **enforces single use** (accepts the fixed answer
  only for the latest unconsumed id and consumes it on every call); a `500`; a 15-second expiry.
- Registration: success; duplicate mobile on an account **with** and **without** an email (U-B6 needs
  both); duplicate email; short; breached; `CaptchaInvalid`; `429` with `Retry-After: 540`; `500`.
- Sign-in (both kinds): success; `401 invalid_credentials` for unknown **and** wrong (identical);
  `429` twice with growing `Retry-After`.
- Sessions: valid; `401 session_invalid`; staff `401 session_idle_timeout`; status with `serverTime`.
- Reset: `200` neutral; confirm `204`, `InvalidPasswordResetToken`, `BreachedPassword`.
- Account area: profile with and without `canEditEmail`; empty and full (10) address books;
  `DefaultAddressCannotBeRemoved`; `409`; preferences with locked cells.
- Staff admin: catalogue; paged list; `hasPassword` false; `403`; `LastAdministratorCannotBeRemoved`.
- Walk-in: `201`; duplicate + lookup result. Duplicates: a group with a deceased cat; merge `200`,
  `isReapplication`, `5xx`, `CustomersNotDuplicates`.
- A `400` with an unmatched `propertyName`.

---

## 13. Allowed scope

```
web/app/(auth)/**
web/app/(portal)/layout.tsx                  ← guard + header only
web/app/(portal)/portal/account/**
web/app/(staff)/layout.tsx                   ← guard + idle lock + header only
web/app/(staff)/staff/admin/**
web/app/(staff)/staff/customers/**
web/components/auth/**
web/components/account/**
web/components/admin/**
web/components/customer/**
web/components/staff/**                      ← StaffSessionGuard, IdleLock
web/components/portal/CustomerSessionGuard.tsx
web/lib/api/{auth,account,staff-auth,staff-admin,staff-customers}.ts
web/lib/api/types.ts                         ← append
web/lib/api/errors.ts                        ← append mappings
web/lib/api/client.ts                        ← §11.2 only
web/lib/identity.ts                          ← DELETE (ADR-FE-004), and its imports
web/lib/hooks/**                             ← new hooks only
web/lib/validation/**
web/**/QueryProvider.tsx                     ← HO-CAT-002's provider, wherever it placed it: register onSessionError only
web/next.config.ts                           ← rewrites only
web/.env.local.example                       ← §12.2 only
web/mocks/handlers/{accounts,staff-accounts,staff-customers}.ts
web/mocks/handlers/index.ts                  ← register only
web/tests/components/{auth,account,admin,customer,staff}/**
web/tests/e2e/{auth,account,staff-auth,staff-admin,staff-customers}.spec.ts
web/tests/e2e/global-setup.ts
web/docs/decisions.md                        ← ADR-FE-001…012
docs/test-cases/TC-HO-ACC-002.md
docs/gates/HO-ACC-002-*.md
```

## 14. Forbidden scope

| Path | Why |
|---|---|
| `src/**`, `tests/**` | HO-ACC-001. A contract gap is §20.7 |
| `.ai/**`, `.claude/**` | Backend architecture; agent definitions |
| `docs/SRS.md`, `docs/USER_STORIES.md`, `docs/handover/**` | Inputs, not outputs |
| `web/app/(staff)/staff/cats/**`, `web/app/(staff)/staff/vaccines/**`, `web/app/(portal)/portal/cats/**` | HO-CAT-002's pages — they gain the guard through the layout only |
| `web/app/page.tsx`, `web/app/layout.tsx` | Landing route and root layout are HO-CAT-002's |
| `web/package.json`, `web/tsconfig.json`, `web/eslint.config.mjs` | No new dependency or config (§10) |

---

## 15. Component test cases

Vitest + Testing Library, querying by role and label.

| ID | Case | Expected |
|---|---|---|
| **Registration (US-ACC-01)** | | |
| U-B1 | Schema: name + mobile + 10-char password + answer | Passes; email optional |
| U-B2 | Schema: neither mobile nor email | Form-level "give at least one", linked to both fields |
| U-B3 | Schema: 9-char password | Message names 10 and the actual length |
| U-B4 | Valid submit | `register` called once with the values + current `captchaId`; success rendered, heading focused |
| U-B5 | Success text | Contains no "verif", "OTP", "code", "SMS", "inbox"; no password; no full mobile |
| U-B6 | Duplicate mobile, both fixtures (with/without email) | **Identical** message; both links present; no "email" in it |
| U-B7 | Duplicate email | Message on email; both links; no "mobile"/"phone" |
| U-B8 | Breached vs too-short from the server | Distinct texts on the password field |
| U-B9 | `CaptchaInvalid` | Message on the CAPTCHA; challenge refetched (mock called twice); answer and password cleared; other fields kept |
| U-B10 | Server `PasswordTooShort` then corrected resubmit | Second submit carries a **different** `captchaId` and succeeds against the single-use mock |
| U-B11 | `429` `Retry-After: 540` | "9 minutes"; no raw seconds |
| U-B12 | CAPTCHA a11y | `alt` states purpose, not the answer; help text with reception linked by `aria-describedby`; "New code" is a text button; new challenge announced |
| U-B13 | CAPTCHA endpoint `500` | "couldn't load" state with a working retry |
| U-B14 | Double click submit (slow mock) | One request; busy state announced |
| **Sign-in, reset, guards (US-ACC-04/05/14)** | | |
| U-C1 | Unknown vs wrong password | Identical message; password cleared; identifier kept |
| U-C2 | Success | Session key invalidated **before** navigation; `?next=/portal/cats/1` honoured; `?next=https://evil.example` ignored |
| U-C3 | `CustomerSessionGuard` pending / null / present | Loading (no children rendered) / redirect with `next` / children |
| U-C4 | Sign-out | Sign-out called; `queryClient.clear()`; routed to sign-in |
| U-C5 | Any `401 session_invalid` from a portal query | Cache cleared; routed to sign-in with `next` |
| U-D1 | Forgot password: `200` with an arbitrary body | The fixed client confirmation; the body text **not** rendered |
| U-D2 | Reset page with `#token=abc` | Token read; address bar becomes `/reset-password`; `no-referrer` metadata present |
| U-D3 | Reset page without a token | Invalid-link state |
| U-D4 | `InvalidPasswordResetToken` | Single invalid/expired state with "request another" |
| U-D5 | `BreachedPassword` on reset | Field error; a second attempt reuses the same token |
| U-D6 | After `204` | Links to **both** sign-in pages; no account-kind hint |
| **Account area (US-ACC-06/13)** | | |
| U-E1 | Profile with `canEditEmail: false` | Email read-only with the explanation; mobile always read-only |
| U-E2 | Four additional numbers | Add button explains the limit of 3 |
| U-E3 | Addresses: set default | One "Default" badge with text |
| U-E4 | Remove an address | Confirmation names the label; `DefaultAddressCannotBeRemoved` shows the server message |
| U-E5 | `409` on an address change | "Someone else changed this — reload" (HO-CAT-002 conflict pattern) |
| U-E6 | Preference matrix | Rendered from the API cells; locked cells disabled with "Always on" text; `<table>` headers present |
| U-E7 | Toggle then navigate away | Unsaved-changes confirm; nothing saved until **Save changes** |
| U-E8 | Save sends only changed cells | Request body has exactly the changed cells |
| U-J1 | Download | Blob saved as `purrcare-my-data.json`; URL revoked; nothing added to the query cache |
| U-J2 | Delete form | Lists every consequence (UI-3); requires password and the checkbox |
| U-J3 | `ReauthenticationFailed` | Field error; password cleared |
| U-J4 | Deletion success | Cache cleared; `/account-deleted` |
| **Staff (US-ACC-07/08/10/11/14)** | | |
| U-G1 | `StaffSessionGuard` null | Redirect to `/staff/sign-in?next=` |
| U-G2 | Idle warning timing | Appears at `idleExpiresOn − 2 min` computed from `serverTime` with a skewed client clock |
| U-G3 | Activity posting | Keydown twice within 60 s → one post; no posts with no interaction over 10 min |
| U-G4 | `session_idle_timeout` while a form has typed values | Overlay shown; background `inert`; focus in dialog; `Escape` does not close; **form values intact** after same-person re-sign-in; no automatic replay |
| U-G5 | Re-sign-in as a different `staffAccountId` | Draft discarded notice; cache cleared; route reloaded |
| U-G6 | `lib/identity.ts` | Does not exist; no import anywhere (a lint/grep test) |
| U-F1 | Role field | Options from the catalogue mock; skeleton while loading; ≥ 1 required |
| U-F2 | Capability preview | Union of selected roles from API data; captioned "Indicative" |
| U-F3 | Role removal | Confirm names the role and the sign-out consequence; `PUT` sends the whole set |
| U-F4 | `403` | `PermissionDeniedState`, no role named |
| U-H1 | Walk-in duplicate | Lookup called; `ExistingCustomerNotice` links the record; values kept |
| U-H2 | `CustomerStatusBadge` Incomplete | Text + icon, not colour alone |
| U-I1 | Duplicates group | Cats listed incl. deceased badge; `NotYetRecordedNotice`; no orders/balances columns |
| U-I2 | Merge review | Fresh fetch on mount; no radio pre-selected; confirmation lists every cat |
| U-I3 | Merge `5xx` | "may be partly complete" + **Run again** sends the identical body |
| U-I4 | Merge flow | No cat endpoint called |
| **Cross-cutting** | | |
| U-X1 | `client.ts` on `POST` | Sends `X-CSRF-Token` equal to the `pc_csrf` cookie; `GET` does not |
| U-X2 | Any `400` | `errorCode` appears nowhere in the DOM |
| U-X3 | Unmatched `propertyName` | Form-level message |
| U-X4 | Storage audit after every form test | `localStorage`, `sessionStorage` empty; no password/token/answer in the query cache |

## 16. End-to-end test cases

Playwright. **Run against MSW, then against the real API** (§12.3); the sheet records both.

| ID | Case | Expected |
|---|---|---|
| E-B1 | Register with mobile, then sign in | Success page; sign-in lands on the portal |
| E-B2 | Register with email only, sign in by email | Works |
| E-B3 | Wrong CAPTCHA, then right | Message + new image, fields kept; then success |
| E-B4 | Duplicate mobile / duplicate email | Specific messages, links, neither naming the other identifier |
| E-B5 | Short then breached password | Two distinct reasons |
| E-B6 | Trigger `429` | "wait N minutes" |
| E-B7 | Keyboard-only registration including "New code" | Completable; focus always visible |
| E-B8 | Timed unassisted registration | Well inside NFR-USA-1; record the time |
| E-C1 | Wrong password vs unknown account | Identical screens |
| E-C2 | Reload the portal after sign-in | Still signed in (cookie) |
| E-C3 | Sign out, press Back | Guard redirects; no cached portal data visible |
| E-C4 | Open `/portal/account/profile` signed out | Redirect to sign-in, then back after sign-in |
| E-D1 | Forgot → link (from the API Development log) → reset → sign in with the new password | Works; token absent from the address bar and history |
| E-D2 | Reuse the link | Invalid/expired state |
| E-E1 | Add three addresses, change default, remove one | One default throughout |
| E-E2 | Opt into SMS marketing, save, reload | Persisted; transactional cells locked |
| E-J1 | Download data, then delete account | JSON downloaded; `/account-deleted`; sign-in with the old credentials fails generically |
| E-G1 | New staff: admin creates account → member sets password via staff reset → signs in | Back office loads with their name |
| E-G2 | Type into a cat form, let the session go idle (short configured timeout), re-sign-in as the same person | Lock shown; typed values still there; save works |
| E-G3 | Same, but sign in as someone else | Draft discarded notice |
| E-F1 | A nurse opens `/staff/admin/staff-accounts` directly | `PermissionDeniedState` |
| E-H1 | Walk-in with a new number; with an existing number | Created *Incomplete*; existing record offered |
| E-I1 | Merge a duplicate pair with a deceased cat | Review lists both cats; after merge both are on the survivor |
| E-X1 | axe on every route in §8.3, clean and with errors shown, and with the idle lock open | Zero serious/critical violations |
| E-X2 | Every route at 360 / 768 / 1024 / 1440 | No horizontal scroll, no clipped control |
| E-X3 | After the whole suite | `localStorage`/`sessionStorage` empty; no `pc_session` readable from `document.cookie` |

---

## 17. Implementation order

1. Install Node 20 LTS; confirm HO-CAT-002's scaffold exists — else **stop** (§20.1).
2. `next.config.ts` rewrite, `.env.local.example`, `client.ts` changes, `onSessionError` wiring.
3. `lib/api/types.ts` from HO-ACC-001 §12, then the five API modules, then MSW handlers.
4. Auth pieces (§11.3) → registration → customer sign-in + guard → reset.
5. Staff sign-in + guard, **delete `lib/identity.ts`**, idle lock — then re-run HO-CAT-002's own
   component and E2E suites: they must still pass behind the new guards (with mocked sessions).
6. Account area → data/deletion.
7. Staff admin → walk-in → duplicates and merge.
8. U-series, then E-series against MSW, then E-series against the real API phase by phase.

```bash
cd web
npm run lint && npm run typecheck && npm run test && npm run test:e2e && npm run build
```

---

## 18. Acceptance criteria

| # | Story / Req | Criterion | Evidence |
|---|---|---|---|
| A1 | ACC-01 | Register with mobile and/or email, password and CAPTCHA; told the account is ready, with no verification wording | U-B4, U-B5, E-B1, E-B2 |
| A2 | ACC-01 | Neither identifier → told one is required | U-B2 |
| A3 | ACC-01 | Rejected CAPTCHA → specific message and a fresh challenge; refreshed after **every** failure | U-B9, U-B10, E-B3 |
| A4 | ACC-01/04/05 | `429` → "wait N minutes" | U-B11, E-B6 |
| A5 | ACC-01 | Duplicate mobile/email reported with sign-in and reset links, never naming the other identifier, identical across account states | U-B6, U-B7, E-B4 |
| A6 | ACC-01 | Short and breached passwords → distinct reasons | U-B3, U-B8, E-B5 |
| A7 | ACC-01 | CAPTCHA can be refreshed; the reception alternative is stated | U-B12, E-B7 |
| A8 | ACC-04 | Sign in by mobile or email; one generic failure; reset offered; session survives reload | U-C1, E-C1, E-C2 |
| A9 | ACC-04 | Sign-out clears everything | U-C4, E-C3 |
| A10 | ACC-05 | Neutral reset request; token handled from the fragment and never persisted | U-D1, U-D2, E-D1 |
| A11 | ACC-05 | Used/expired link → one state, can request another | U-D4, E-D2 |
| A12 | ACC-06 | Labelled addresses with one default | U-E3, U-E4, E-E1 |
| A13 | ACC-06 | Marketing consent per channel; transactional visibly always on; explicit save | U-E6, U-E7, U-E8, E-E2 |
| A14 | ACC-07 | Administrator creates staff with roles from the API catalogue | U-F1, U-F2, E-G1 |
| A15 | ACC-07 | A `403` renders the permission-denied state; UI gating is presentation only | U-F4, E-F1 |
| A16 | ACC-14 | Staff sign in; first password via "Set or reset password" | E-G1 |
| A17 | ACC-08 | Idle → re-authentication required, in place | U-G4, E-G2 |
| A18 | ACC-08 | Unsaved form data preserved for the same person only | U-G4, U-G5, E-G2, E-G3 |
| A19 | ACC-10 | Walk-in created and shown *Incomplete*; duplicate phone hands off to the existing record | U-H1, U-H2, E-H1 |
| A20 | ACC-11 | Duplicates listed with cats; orders/balances stated as not recorded | U-I1 |
| A21 | ACC-11 | Merge: explicit survivor, full impact stated, one request, re-run recovers | U-I2, U-I3, U-I4, E-I1 |
| A22 | ACC-13 | Data downloadable as JSON | U-J1, E-J1 |
| A23 | ACC-13 | Deletion states its consequences, needs the password, signs out | U-J2, U-J3, U-J4, E-J1 |
| A24 | UI-6 | Zero serious/critical axe violations on every screen, including the idle lock | E-X1 and every a11y row |
| A25 | UI-1 | Every screen at 360–1440 px | E-X2 |
| A26 | NFR-SEC-4 | No token, password, answer or draft in browser storage; CSRF header sent; `lib/identity.ts` gone | U-X1, U-X4, U-G6, E-X3 |
| A27 | NFR-USA-4 | No error code, id or stack trace in the DOM | U-X2, U-X3 |

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | §8 layering; the three route groups; same-origin proxy; no global store (the lock context holds no server data); no new dependency; HO-CAT-002 files changed only as §13 allows. **`.ai/` backend rules do not apply** |
| 2 | SECURITY | `security-reviewer` | ADR-FE-002/007: no token in JS, CSRF header, fragment-only reset token and `no-referrer`, identical sign-in and duplicate messages (both fixtures), open-redirect guard on `?next=`, no `NEXT_PUBLIC_*` secret or test answer, `lib/identity.ts` removed, cache cleared on sign-out and on person change |
| 3 | CODE_REVIEW | `code-reviewer` | Component size and composition; typing against HO-ACC-001; single mapping table; HO-CAT-002 §9 conventions |
| 4 | TEST | `test-engineer` | Writes `TC-HO-ACC-002.md` from §15/§16 first; records mock and real-API runs separately; re-runs HO-CAT-002's suites behind the new guards |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row against evidence; nothing withdrawn (§4.1) was built |

---

## 20. BLOCKED conditions — stop and report, do not work around

**20.1 The `web/` scaffold or Node is missing.** HO-CAT-002 §12.1 owns the scaffold. Do not create a
second `web/`, and do not `create-next-app` a partial one for these screens.

**20.2 A token or secret needs to live in JavaScript.** It never does — sessions are `HttpOnly`
cookies, reset tokens live in component state for one submit, CAPTCHA answers and passwords are
cleared on failure. If a design seems to need storage, it is wrong; stop.

**20.3 A screen varies with account state.** Any difference between the unknown-account and
wrong-password sign-in screens, between the two duplicate-mobile fixtures, or between reset-request
outcomes defeats the server's non-disclosure. Stop and fix; never "improve" the message.

**20.4 Links to things that don't work yet.** Reset delivery works only against a Development API
(HO-ACC-001 §4.3); the walk-in notice says so. Do not build a fake delivery or a "show me the link"
button in the product.

**20.5 A new dependency seems necessary.** §10 lists the tempting ones. Raise it; do not install.

**20.6 A HO-CAT-002 page needs changing to work behind the guard.** Its pages must work unchanged; if
one does not, report it against HO-CAT-002 rather than editing it.

**20.7 The contract disagrees with HO-ACC-001.** Field names, codes, statuses, cookie names or the
CSRF rule differ from HO-ACC-001 §12 → report it. Do not shim it on the client.

**20.8 An open question blocks wording.** Use the §9.1 default and flag it in the gate report; do not
invent a policy.
