GATE: ARCHITECTURE
HANDOVER: HO-ACC-002
COMMITS: 7eea158 a0771b5 b20e01f 8b884e4 f9c326f 9e8d33a (delta 061aed3..9e8d33a)
DATE: 2026-09-27
STATUS: PASS

# Sixth pass (delta)

This pass covers branch `ho/ACC-002` at `9e8d33a`. It reviews only `061aed3..9e8d33a`: 12 files.

## Static checks

In `web/`:
- `npx tsc --noEmit` exited 0.
- `npm run lint` (`eslint . --max-warnings 0`) exited 0.
- `npx prettier --check .` reported clean.

Backend: no `dotnet build` was run. The only backend change is one `[InlineData("[::1]")]` string line in `CsrfCookieDomainTests.cs`, and the fifth pass's build was clean.

`git merge-tree --write-tree master ho/ACC-002` is clean (tree `a8d3a0b`). Master is an ancestor of the branch.

No tests were run. The runtime claims below are marked for TEST.

FINDINGS

- [LOW] `web/tests/e2e/staff-auth.spec.ts:275` (E-G3). The test cannot tell "no refetch was attempted" apart from "a refetch happened and the fallback absorbed it". Its only failure signal is the ErrorState text "couldn't load this cat's profile". The cat query already has data at that point, so a refetch that hit the armed 401 would give `isError && data`. `CatProfileDataShell`'s `isError && !data` guard would then hide the ErrorState anyway. E-G3 therefore passes with or without the pause, which is the same weakness it was written to fix in E-G2.
  - The pause itself is now proven by the unit tests in `idle-lock-context.test.tsx`. They dispatch on `window` and have unlocked positive controls, so this is not load-bearing.
  - Fix direction: assert that the one-shot `__forceCatQuerySessionInvalid` hook is still armed, or count cat-query requests.
  - **TEST.**

## Verification of fifth-pass findings

| Finding | Result |
|---|---|
| MEDIUM: offline-at-lock deadlock | **Closed** |
| MEDIUM: unrecorded `run-real-api-e2e.mjs` and `package.json` command | **Closed** |
| LOW: HO-ACC-002 fourth-pass row did not name the two HO-CAT-002 files | **Closed** |
| LOW: vacuous focus-pause unit test | **Closed** |
| CODE_REVIEW fifth-pass MEDIUM: `UnsavedChangesGuard` `history.state` marker | **Layering fine** |
| CODE_REVIEW fifth-pass LOW: `[::1]` case | **No concern** |

- **Offline-at-lock deadlock (MEDIUM): closed.** Two changes in `7eea158`:
  - `useSignInStaff()` (`web/lib/hooks/useStaffAuthMutations.ts`) now sets `networkMode: 'always'`.
  - A new `restoreManagers()` helper (`IdleLockContext.tsx`) is used by both `unlock()` and the unmount-while-locked cleanup. It reinstalls both default listeners, then calls `onlineManager.setOnline(navigator.onLine)`, with a guard for a missing `navigator`.
  - The pause on focus and reconnect still applies to every other query. Only the one mutation that must always be reachable is exempt.
  - The side effect on the plain staff sign-in page is documented and harmless: it now fails fast instead of pausing.
  - Layering: putting `networkMode` on a mutation hook in `lib/hooks` is the right place. It is a TanStack option on the hook that owns the mutation, and no component reaches into `onlineManager` to work around it. `useStaffAuthMutations.ts` was new on this branch (`454b756`), so it is inside §13 `web/lib/hooks/** (new hooks only)`.
  - The new test in `idle-lock.test.tsx` calls `setOnline(false)` before the lock, completes re-sign-in, and asserts `isOnline()` is `true` after unlock. **TEST** should also run the real-browser variant from the fifth pass (`context.setOffline(true)` before the lock, `false` after).
- **Unrecorded script (MEDIUM): closed.** HO-ACC-002 §1 Amended now has a 2026-09-27 row naming `web/scripts/run-real-api-e2e.mjs` and the changed `"test:e2e:real-api": "node scripts/run-real-api-e2e.mjs"` command. The row states that it supersedes the third-pass inline command.
  - The script now spawns Playwright's resolved `bin` through `process.execPath` with `shell: false`. It uses only `node:child_process`, `node:module` and `node:path`, so it adds no dependency.
- **LOW, HO-ACC-002 fourth-pass row: closed.** `9e8d33a` appends a correction to that row naming `app/_components/cat/CatProfileDataShell.tsx` and `mocks/handlers/areas/cat-profiles.ts`.
- **LOW, vacuous focus test: closed.** `fireVisibilityChange` now dispatches on `window`, and unlocked positive controls were added for both focus and online. E-G3 was also added (see the LOW above).
- **CODE_REVIEW fifth-pass MEDIUM, `UnsavedChangesGuard` (`8b884e4`): layering is fine.**
  - The "is this popstate ours" signal moved from `history.state` markers into the existing component-local `sentinelActiveRef`. `useId()` and the mount-time `replaceState` were removed.
  - It is still component-local, with no global store, and the sentinel push still spreads existing `history.state`.
  - ADR-FE-013 in `web/docs/decisions.md` was amended.
  - Correctness, including the documented limitation where an external `pushState` lands on top of the sentinel, belongs to CODE_REVIEW and TEST.
- **CODE_REVIEW fifth-pass LOW, `[::1]` (`f9c326f`): no concern.** It is a test-data line, recorded in HO-ACC-001 §1 Amended.
- Each of HO-ACC-001 and HO-ACC-002 still has exactly one `*(append further rows here)*` marker.

SCOPE CHECK
- files in allowed scope: 12/12.
  - In §13:
    - `web/components/account/UnsavedChangesGuard.tsx`
    - `web/components/staff/IdleLockContext.tsx`
    - `web/lib/hooks/useStaffAuthMutations.ts`
    - `web/docs/decisions.md`
    - the three `web/tests/components/{account,staff}/**` files
    - `web/tests/e2e/staff-auth.spec.ts`
  - Recorded exceptions:
    - `web/scripts/run-real-api-e2e.mjs`: HO-ACC-002 §1 Amended, 2026-09-27.
    - `tests/.../CsrfCookieDomainTests.cs`: HO-ACC-001 §1 Amended, 2026-09-27.
    - `docs/handover/HO-ACC-001.md` and `HO-ACC-002.md`: gate-requested records.
- files outside allowed scope: none

---

GATE: ARCHITECTURE
HANDOVER: HO-ACC-002
COMMITS: 9953495 36f380b fefb86d 761ed52 84f50e0 f26b51c 615c95e 775b27a (merge of master eef1621; only its 3 conflict resolutions reviewed)
DATE: 2026-09-27
STATUS: FAIL

# Fifth pass (delta)

This pass covers branch `ho/ACC-002` at `775b27a`. It reviews ACC-002's own commits since `427e375`, listed above. The Cat Profiles commits that arrived through the master merge were already gated on master, so they were not re-reviewed.

## Static checks

In `web/`:
- `npx tsc --noEmit` exited 0.
- `npm run lint` (`eslint . --max-warnings 0`) exited 0.
- `npx prettier --check .` reported clean.

Backend: `dotnet build CatEcommerce.sln` was run from a fresh `git archive 775b27a` in the new scratch folder `acc2-arch5`. Result: build succeeded with 0 errors and 0 warnings. That folder alone was deleted afterwards.

`git merge-tree --write-tree master ho/ACC-002` is clean (tree `a054bb7`), and master is an ancestor of the branch.

No tests were run. The runtime claims below are marked for TEST.

FINDINGS

- [MEDIUM] `web/components/staff/IdleLockContext.tsx:151-165`. The pause freezes `onlineManager`'s *state* as well as its listener. Nothing resyncs that state on unlock, so the lock can deadlock itself.
  - `onlineManager` keeps `#online` and never reads `navigator.onLine` (`@tanstack/query-core` 5.103.2 `onlineManager.js`). `unlock()` only reinstalls the listener.
  - The failing sequence:
    1. The terminal goes `offline` before or at lock. This is realistic: the lid closes, Wi-Fi drops, the `offline` event fires while the real listener is still attached, and `#online` becomes `false`.
    2. The lock takes hold and detaches the listener.
    3. The `online` event on reconnect is dropped.
    4. The re-sign-in `useSignInStaff()` mutation (`lib/hooks/useStaffAuthMutations.ts:13`) uses the default `networkMode: 'online'`, so it never starts: `canFetch` is false and it pauses, waiting for an `onlineManager` notification that cannot arrive.
    5. Only `unlock()` would reattach the listener, and `unlock()` runs only after that mutation succeeds.
  - Result: the dialog's Sign in never completes. The only way out is a reload, which discards the draft US-ACC-08 AC2 exists to protect.
  - This is the same failure the ADR-FE-010 amendment itself names ("permanently stuck behind the very lock it exists to lift"), reached through frozen state rather than a forced `setOnline(false)`.
  - The reverse case (offline during the lock) leaves `#online` stale at `true`, which is harmless.
  - Fix direction (implementer's call), any of:
    - pause only `focusManager` and leave `onlineManager` alone. A reconnect refetch that 401s is already absorbed by the `isError && !data` guards and by `lockedRef`;
    - make the locked `onlineManager` setup still forward `online` events;
    - give the re-sign-in mutation `networkMode: 'always'`.
  - Whichever is chosen, resync with `onlineManager.setOnline(navigator.onLine)` on unlock.
  - **TEST:** lock while offline (`context.setOffline(true)` before lock, `false` after), then re-sign-in must complete.

- [MEDIUM] `web/scripts/run-real-api-e2e.mjs` (new) and `web/package.json:18`. **Unrecorded out-of-§13 file.**
  - The script is outside §13. `package.json` is §14 forbidden scope, admitted only through its §1 Amended record. That record still describes the command as `E2E_REAL_API=1 playwright test`.
  - Neither HO-ACC-002 §1 Amended nor any other handover names `run-real-api-e2e.mjs` or the changed script command.
  - Placing it under `web/scripts/` is fine. No convention in `.ai/`, `web/README.md` or §10 governs it, it adds no dependency (`node:child_process` only), and it is the fix this gate's fourth-pass LOW asked for.
  - Only the record is missing: one sentence in §1 Amended naming the file and the new `test:e2e:real-api` command would close this.
  - Until that record exists, this file counts as outside allowed scope (SCOPE CHECK below), which fails the pass on its own.
  - For CODE_REVIEW, not architecture: `spawnSync(..., { shell: true })` passes the forwarded CLI args through a shell unquoted.

- [LOW] `docs/handover/HO-ACC-002.md:18` (§1 Amended). The new 2026-09-27 ARCHITECTURE fourth-pass row says the edit is "integrator-authorised ... (recorded here and in HO-CAT-002 §1)". The row describes only `IdleLockContext.tsx`, though. It never names `app/_components/cat/CatProfileDataShell.tsx` or `mocks/handlers/areas/cat-profiles.ts`.
  - HO-CAT-002 §1 Amended does record both in full, so the authorisation is traceable. HO-ACC-002's own record is incomplete, however.
  - Add the two paths to that row.

- [LOW] `web/tests/components/staff/idle-lock-context.test.tsx:68-71,106-130`. The behavioural focus test is vacuous.
  - `fireVisibilityChange` dispatches `new Event('visibilitychange')` on `document` with `bubbles: false`. TanStack's listener sits on `window`, so the event never reaches it, whether the terminal is locked or not.
  - There is no unlocked positive control, so `fetchCount === 1` would pass with no pause at all.
  - The mechanism itself is still proven by the add/removeEventListener spy tests at `:74-104`, `:155-215`.
  - The online test at `:132` dispatches on `window` and is valid, but it also has no unlocked control.
  - E-G2 (below) triggers refetch through `__invalidateQueries`, which bypasses `focusManager`. It therefore proves the `isError && !data` layer, not the pause.
  - Fix: `{ bubbles: true }`, plus an unlocked control.
  - **TEST.**

- Note, not a finding for this branch: `docs/handover/HO-CAT-002.md:21-26`. Master (`eef1621`) already has blank lines inside the §1 table, before its last row. A blank line ends a Markdown table, so the rows after it render outside the table. The merge resolution placed ACC-002's row before them and kept all 9 Amended rows (8 from master + 1 from ACC-002), so it did not create this problem. It is a master-side issue for HO-CAT-002's owner.

## Verification of the caller's items

| Item | Result |
|---|---|
| HIGH: lock pause reaching mounted queries | **Closed** (details below) |
| MEDIUM: idempotency, unmount restore, ADR-FE-010, §11.9 | **Closed** (details below) |
| LOW: Windows npm script | **Closed in code.** Record missing (MEDIUM above) |
| LOW: stale `playwright.config.ts` comment | **Closed** |
| LOW: `HO-ACC-001.md` marker and row (24) | **Closed** |
| CODE_REVIEW fourth-pass MEDIUM: `UnsavedChangesGuard` `history.state` marking | **Layering: fine** |
| CODE_REVIEW fourth-pass LOW: IPv4 forms in `CsrfCookieDomain` | **No layering concern** |
| Merge `775b27a`: three conflict resolutions | **Correct** |

- **HIGH (fourth pass), lock pause reaching mounted queries: closed.**
  - `lock()` swaps `focusManager` and `onlineManager` to a no-op `setEventListener`. That detaches the real `window` `visibilitychange`, `online` and `offline` listeners. Because the managers themselves dispatch focus and reconnect to every `QueryObserver`, the pause now reaches mounted observers regardless of their cached options.
  - The reproduced default setups match `query-core` 5.103.2 `focusManager.js` and `onlineManager.js` exactly (verified in `node_modules`).
  - Restoration happens on `unlock()` and in the unmount cleanup, both guarded by `pausedRef`.
  - `cancelQueries` was removed; `revert: false` no longer appears anywhere.
  - `CatProfileDataShell.tsx:61` now reads `catQuery.isError && !catQuery.data`, and a `!data -> null` fallthrough was added. It is recorded in HO-CAT-002 §1 Amended (row 21) and referenced from HO-ACC-002 (see the LOW above).
  - E-G2 on `/staff/cats/{id}/edit` exists (`tests/e2e/staff-auth.spec.ts`, `36f380b`). It uses a one-shot MSW hook, and the `cat-profiles.ts` mock edit is recorded in HO-CAT-002.
  - The listener-freeze side effect is the MEDIUM above.
- **MEDIUM (fourth pass), idempotency, unmount restore, ADR-FE-010 and §11.9: closed.**
  - A second `lock()` is a no-op beyond `setLocked(true)`.
  - The unmount cleanup restores.
  - `web/docs/decisions.md` ADR-FE-010 has a fourth-pass amendment.
  - §11.9 now describes `{ locked, lock, unlock }` and the global pause.
- **LOW, Windows npm script: closed in code.** It uses a Node wrapper with no new dependency. Its record is missing (MEDIUM above).
- **LOW, stale `playwright.config.ts` comment: closed.**
- **LOW, `HO-ACC-001.md` marker and row (24): closed.**
  - A single `*(append further rows here)*` marker remains, at the end of the cell.
  - Row (24) carries a correction stating the 13 invalid cases.
  - Row (25) records `84f50e0`: last-label all-digits or `0x` rejection, 19 invalid and 5 valid cases.
  - `AccountOptions.cs` stays under the existing CsrfCookieDomain integrator authorisation.
- **CODE_REVIEW fourth-pass MEDIUM, `UnsavedChangesGuard` `history.state` marking: layering is fine.**
  - It is component-local: `useId()` plus refs, with no global store or context.
  - It spreads existing `history.state` rather than replacing it, so Next's own keys survive.
  - It has no interaction with the staff lock.
  - ADR-FE-013 was amended.
  - Correctness belongs to CODE_REVIEW and TEST, for example whether Next's router later drops the marker through its own `replaceState`.
- **CODE_REVIEW fourth-pass LOW, IPv4 forms in `CsrfCookieDomain`:** no layering concern. It is Infrastructure options with `ValidateOnStart`.
- **Merge `775b27a`, conflict resolutions: all three are correct.**
  - `web/.env.local.example`: `NEXT_PUBLIC_API_BASE_URL=http://localhost:5005`, per the owner's decision. Master's valid-URL rationale is retained as a comment.
  - `web/playwright.config.ts`: keeps both `PORT` and ACC-002's `E2E_REAL_API` gating.
  - `docs/handover/HO-CAT-002.md`: the Amended rows are combined, 8 from master plus 1 from ACC-002, with a single end marker.

SCOPE CHECK
- files in allowed scope: 16/17. That counts §13 files plus integrator-authorised, recorded exceptions:
  - `CatProfileDataShell.tsx` and `mocks/handlers/areas/cat-profiles.ts`: recorded in HO-CAT-002;
  - `AccountOptions.cs` and `CsrfCookieDomainTests.cs`: HO-ACC-001 (25);
  - `docs/handover/HO-ACC-001.md`, `HO-ACC-002.md` and `HO-CAT-002.md`: gate-requested records;
  - `web/package.json` and `web/playwright.config.ts`: existing §1 Amended record;
  - `web/.env.local.example`: §12.2, owner decision.
- files outside allowed scope: `web/scripts/run-real-api-e2e.mjs`. It is new, outside §13 and has no §1 Amended record. Recording it closes this.

# Fourth pass (delta)

This pass covers branch `ho/ACC-002` at `e3a3f88`. It reviews the fix round `2b0f9fa..e3a3f88`: 20 files of the branch's own changes, plus the CODE_REVIEW record `f380f3f`. The caller's range `f380f3f..e3a3f88` would miss `d693b2b` and `6544b01`, because both sit before `f380f3f`, so the range was widened to include them.

## Static checks

In `web/`:
- `npx tsc --noEmit` exited 0.
- `npm run lint` exited 0.
- `npx prettier --check .` reported clean.
- `npx playwright test --list` lists only `chromium` and `mobile-360`, 119 tests each.
- `E2E_REAL_API=1 ... --project=real-api --list` lists 3 real-api tests.

Backend: `dotnet build CatEcommerce.sln` was run from a fresh `git archive e3a3f88` in the new scratch folder `acc2-arch4`. Result: 0 errors. All 20 warnings are NU1900, meaning the private feed was unreachable. That folder alone was deleted afterwards.

`git merge-tree --write-tree master ho/ACC-002` is clean (tree `3185617`), and master `eae3d7b` is an ancestor.

No tests were run.

FINDINGS

- [HIGH] `web/components/staff/IdleLockContext.tsx:69-97`, together with `web/app/_components/cat/CatProfileDataShell.tsx:51`. **US-ACC-08 AC2 / ADR-FE-010 / §8.4 is still breakable. The breaking path is the handover's own acceptance scenario E-G2: "type into a cat form, let the session go idle, re-sign-in".**
  - **Why the root-level layer does nothing for mounted queries.** `setDefaultOptions` only changes `QueryClient.#defaultOptions`. A mounted observer's options are computed at render and pushed in an effect:
    - `@tanstack/react-query` `useBaseQuery.js:17` calls `client.defaultQueryOptions(options)`.
    - `useBaseQuery.js:36` calls `observer.setOptions(...)` inside a `useEffect`.
    - Focus refetch reads the observer's stored copy (`query-core` `queryObserver.js:80`, `this.options.refetchOnWindowFocus`; `query.js:222`).
    - So the pause only reaches components that re-render after `lock()`, which here means the ones consuming `useStaffLockState` (`StaffSessionGuard`, `IdleLock`). Page and layout queries whose components do not re-render (the `children` element identity is stable) keep refetching on focus.
    - The unit test (`idle-lock-context.test.tsx`) only asserts `getDefaultOptions()`. It never mounts a query and fires focus, so it cannot catch this.
  - **The draft is still lost on the cat pages.** `CatProfileDataShell` is the `(staff)/staff/cats/[catId]/layout.tsx` wrapper around every cat form (edit, weights, vaccinations and the rest).
    - It still branches on `catQuery.isError || !catQuery.data` and swaps `{children}` out for `ErrorState`.
    - After lock, a return to the tab refetches the stale cat query (staleTime 30 s), which gets `401` against the real API. TanStack keeps `data` but sets `isError`, and the layout unmounts the edit form, losing its react-hook-form draft.
  - **`cancelQueries(undefined, { revert: false })` at `:84` makes this worse.** With `revert: false`, `query.js:396-405` does not take the silent or revert branch. It dispatches `{type:'error', error: CancelledError}`. So every query in flight at the moment of lock becomes `isError`, including first-load queries with no data. On this layout, that alone swaps the form out.
    - The comment at `:76-83` has the semantics backwards. `revert: true` restores the pre-fetch state, and for a background refetch that state includes the data.
  - **Correction to the third pass.** It called HO-CAT-002's `cats/[catId]/edit/page.tsx` "not affected". The page is fine, but the layout above it is not. The third pass missed that.
  - **The three ACC-002 sites are fixed.** The page-level `isError && !data` guards on the staff-account page, the staff-customer page and `MergeReview` are correct.
  - **Other `isError`-first sites under `app/(staff)/**` and `components/**`.** `DataTable` (lists), `DuplicateGroupList` and `vaccination-status/page.tsx` are read-only, with no draft. `CatProfileDataShell` is the one draft-bearing site left.
  - **Fix direction** (implementer's call). Either:
    - make the lock's pause actually reach mounted observers, for example by gating TanStack's `focusManager`/`onlineManager` while locked, or by walking the cache's observers; or
    - get an integrator-authorised, recorded `isError && !data` edit to `CatProfileDataShell.tsx`, which is outside §13 and belongs to HO-CAT-002. Only this option also covers any future layout with its own query.
  - Either way, reconsider `revert: false`.
  - **TEST:** E-G2 on `/staff/cats/{id}/edit`, against both MSW and the real API. Also a unit test that mounts a `useQuery`, locks, fires `focusManager.setFocused(true)`, and asserts no fetch.

- [MEDIUM] `web/components/staff/IdleLockContext.tsx:69-75`, `web/components/staff/IdleLock.tsx:92-97`. Restoring the defaults on unlock is not exact.
  - **`lock()` is not idempotent.** `lockNow` has three callers: the countdown timer (`:160`), the `session_idle_timeout` handler (`:120`) and the `sessionError` effect (`:141`). None of them checks whether the terminal is already locked.
  - The realistic order: the timer locks while the tab is hidden. On return, the first request reaches the server, which reports `session_idle_timeout` because this is its first detection, and that calls `lock()` again.
  - The second call captures `{refetchOnWindowFocus:false, refetchOnReconnect:false}` as the "originals". `unlock()` then restores `false`, so focus and reconnect refetch stay off for the whole app until a reload.
  - **No unmount cleanup either.** If `StaffLockProvider` unmounts while locked, for example on a client Back to a route outside `(staff)`, the defaults stay `false`.
  - **Per-query overrides are preserved.** The only one is `useCaptcha`'s `refetchOnWindowFocus:false`. But a per-query `true` would bypass the pause entirely.
  - **This global policy change is not recorded.** ADR-FE-010 in `web/docs/decisions.md` does not mention it, and §11.9 still says the context "exposes only `lock()`".
  - Fix: guard `lock()` on the ref already being set, restore in an unmount cleanup, and record the change in ADR-FE-010.
  - CODE_REVIEW/TEST.

- [LOW] `web/package.json:18`: `"E2E_REAL_API=1 playwright test ..."` is POSIX-only syntax.
  - On this repo's own Windows dev hosts, npm runs scripts through `cmd.exe` (`npm config get script-shell` returns `null`), so the script fails there.
  - It is a manual-only script (§5.1).
  - Fix: a cross-platform form, or document running it from bash.

- [LOW] `web/playwright.config.ts:16-22`: the older comment still says `npx playwright test --project=real-api` runs the file, and calls it "the only project this repo's own npm scripts never pass". Both are now false: the env var is required, and `test:e2e:real-api` passes the project.

- [LOW] `docs/handover/HO-ACC-001.md:16` has two issues:
  - It now carries two `*(append further rows here)*` markers, at char 2633 and at the end of the cell. The old one should go.
  - Row (24) still says "7 invalid-value cases" and "no scheme, port, path or whitespace". Since 37b34bd, IP literals are rejected too and there are 13 invalid cases. `AccountOptions.cs` changed under the existing integrator authorisation, but its record is stale.

## Third-pass findings: disposition

| Third-pass finding | Status now |
|---|---|
| HIGH locked page's own query | **Open.** Closed on all three ACC-002 sites. Still reachable through `CatProfileDataShell`, and the root layer is inert (HIGH above). |
| BLOCKER `web/package.json` unrecorded | **Closed.** |
| MEDIUM unrecorded `playwright.config.ts` / `pagination-caps.test.ts`; `real-api` running by default | **Closed.** |
| MEDIUM `HO-ACC-001.md` heading and numbering | **Closed.** |
| LOW `UnsavedChangesGuard` | **Closed** for layering. (d) is still **TEST**. |
| LOW `lockedRef` race | **Closed.** |
| LOW `eslint.config.mjs` comment | **Closed.** |

Detail for each:

- **BLOCKER `web/package.json` unrecorded: closed.** Recorded in HO-ACC-002 §1 Amended (2026-09-27), together with `playwright.config.ts` and `pagination-caps.test.ts`.
- **MEDIUM unrecorded files and the default `real-api` run: closed.** The `real-api` project now exists only when `E2E_REAL_API=1`, which was verified with `--list`.
- **MEDIUM `HO-ACC-001.md` heading and numbering: closed.**
  - The ACC-002 heading is restored, and the ACC-002 contribution adds a balanced 6 `**`. Master's own odd count is pre-existing.
  - Items are renumbered (22)–(24), with the internal "((22) above)" updated.
  - **The removed ~21K block was verified by three-way reconstruction.** Base `7abc9e4` was taken, master's two inserts and ACC-002's one insert were applied, and the result was compared with `e3a3f88`. The only differences are:
    - ACC-002's three rows moved from the mid-cell marker to the end of the cell, after master's (21);
    - the renumbering;
    - the added end marker (LOW above).
  - **Nothing legitimate was lost.** The removed block was a verbatim second copy of items (10)–(20).
  - The third pass said HO-ACC-002 §1 Amended cites "(21)/(22)". No such citation exists, either at `2b0f9fa` or now. The only external citation is HO-ADM-001 → HO-ACC-001 (21), which is still master's NUL-fix row, so it is correct.
- **LOW `UnsavedChangesGuard`: closed for layering.** It remains component-local refs, with no global store. It is used only by `ProfileForm` and `NotificationPreferenceMatrix`, and has no interaction with the staff lock. The `history.length > 1` check, `sentinelActiveRef`, the pop on clean save and the 500 ms safety timeout are correctness matters for CODE_REVIEW. (d) is still **TEST**.
- **LOW `lockedRef` race: closed.** `lockedRef` is now set synchronously in `lockNow` and `handleReSignInSuccess`.
- **LOW `eslint.config.mjs` comment: closed.** The comment was corrected.
- **Customer portal is not idle-locked.** That is per the handover:
  - US-ACC-08 is `(staff)` layout only;
  - ADR-FE-001 puts the idle lock on `(staff)` only;
  - NFR-USA-5 uses `UnsavedChangesGuard` for account forms and the idle lock for staff forms.
- **CODE_REVIEW third pass:**
  - The MEDIUM (stray sentinel) and the LOW (IP literals) raise no layering concern.
  - Adding IP-literal rejection to `AccountOptions` (Infrastructure options, `ValidateOnStart`) is within the existing authorisation, apart from the stale record noted above.

SCOPE CHECK
- files in allowed scope: 21/21. That counts §13 files plus integrator-authorised, recorded exceptions:
  - `docs/handover/HO-ACC-001.md` and `HO-ACC-002.md`: gate-requested records;
  - `web/package.json`, `web/playwright.config.ts` and `web/eslint.config.mjs` (comment only): recorded in §1 Amended;
  - `AccountOptions.cs` and `CsrfCookieDomainTests.cs`: under the existing CsrfCookieDomain authorisation, recorded in HO-ACC-001 (24);
  - `tests/e2e/account-area.spec.ts`: recorded in the second pass.
- files outside allowed scope: none. The remaining HIGH's second fix option needs `web/app/_components/cat/CatProfileDataShell.tsx`, which is outside §13 and needs integrator authorisation if chosen.

# Third pass

This pass covers branch `ho/ACC-002` at `2e20e73`. It reviews the fix round `49b4d50..2e20e73`, which is 26 files of the branch's own changes. For the merge `108e0db`, only its two conflict resolutions were checked.

Integrator authorisations for this round were taken as given and are **not** findings:
- the `eslint.config.mjs` rule, recorded now;
- the `components/ui/dialog.tsx` `overlayClassName` prop;
- the dev-only `window.__invalidateQueries` / `__apiBaseUrl` hooks. Both are gated on `NODE_ENV !== 'production'`, which was verified;
- the `AccountOptions.CsrfCookieDomain` startup validation.

## Static checks

In `web/`:
- `npx tsc --noEmit` exited 0.
- `npm run lint` (`eslint . --max-warnings 0`) exited 0.
- `npx prettier --check .` reported clean.

Backend: `dotnet build CatEcommerce.sln` was run from a fresh `git archive 2e20e73` in a new scratch folder. Result: 0 errors. All 20 warnings are NU1900, meaning the private feed was unreachable. The folder was deleted afterwards.

`git merge-tree --write-tree master ho/ACC-002` is clean (tree `7459823`), and master `eae3d7b` is already an ancestor.

No tests were run. Runtime claims below are marked for TEST.

## ESLint probes

Each probe was run through `eslint --stdin --stdin-filename`. Nothing was written to the tree.

| Probe | Result |
|---|---|
| `../../lib/hooks/useCaptcha` in `components/account` | error, exit 1 |
| `../../../lib/hooks/...` in `components/admin/sub` | error, exit 1 |
| `../../../lib/hooks` (bare folder) | error, exit 1 |
| `@/lib/hooks/useSession` in `components/customer` | error, exit 1 |
| `useQuery` from `@tanstack/react-query` in `components/auth` | error, exit 1 |
| `useQuery` in `SignOutButton.tsx` | error, exit 1 (the ban now stays in force there) |
| `@/lib/hooks/*` in `SignOutButton.tsx` | clean (the only thing lifted) |
| `@/lib/hooks/*` in `components/staff` | clean (exempt) |
| `@/lib/api/*` in `components/account` | clean |

## Merge 108e0db: conflict resolutions

- **`DuplicateCustomerGroupsEndpointTests.cs`** is correct.
  - It takes master's content: the ADR-043 Reception-client `RegisterCatAsync`, the paging-cap tests and the ORDER BY test.
  - It keeps ACC-002's record-status/registration-channel test.
  - The net difference against master is one comment phrase.
- **`HO-ACC-001.md` Amended cell** keeps the text of both sides, which was verified segment by segment. The join is damaged, though. See the MEDIUM finding below.

FINDINGS

- [HIGH] `web/app/(staff)/staff/admin/staff-accounts/[staffAccountId]/page.tsx:37-41` (together with `web/lib/hooks/useStaffAdmin.ts` `useStaffAccount`) — **US-ACC-08 AC2 / ADR-FE-010 / §8.4 is still breakable. The mechanism is new: the page's own error branch unmounts the form.**
  - **What is fixed.** The layout-level part of the second-pass HIGH is closed (see Disposition).
  - **The remaining path.** This is second-pass path (b), "a page query refetched on the same focus event", now ending in the page rather than a redirect:
    1. The terminal locks. Normally the local timer does this while the tab is hidden.
    2. On return, `refetchOnWindowFocus` refetches every stale active query. Provider `staleTime` is 30 s, and any real idle period is far longer, so this includes `['staff-admin','staff-account',id]`.
    3. Against the real API (the session was ended on the first idle detection), that refetch gets `401` twice: `noRetryOn403` allows one retry for a 401.
    4. TanStack v5 keeps `data` but sets `isError: true`.
    5. The page's `if (isError || !account) return <ErrorState …/>` swaps `StaffAccountDetail` out, and its unsaved `selectedRoleIds` draft is lost.
    6. `IdleLock` stays mounted, but its child page has already discarded the form. After unlock, the user sees "We couldn't load this account just now".
  - **Why the tests miss it.** The new unit test and e2e test (`staff-auth.spec.ts:78`) run on `/staff/customers/new`, which has no page query. Step 3 of that e2e dispatches `input` to fire a heartbeat, but the heartbeat listener is disabled while locked (`IdleLock.tsx:165`), so that step exercises nothing.
  - **Same pattern, no draft today.** `staff/customers/[customerId]/page.tsx:30` is read-only, so nothing is lost there now. `MergeReview.tsx:75` keeps its own `useState` but blanks its content until a manual retry. The pattern will be copied onto future staff edit pages.
  - **Not affected.** HO-CAT-002's `cats/[catId]/edit/page.tsx:21` renders whenever `data` exists.
  - **Fix direction** (implementer's call). Either render cached data when present (`isError && !data`), or stop focus refetch and surfacing of staff queries while locked (for example, focus-manager or `enabled` gating from `useStaffLockState`).
  - **TEST:** add "lock, then return to the tab, while on a staff-account detail page with a changed role selection". Run it against the corrected mock and the real API.

- [BLOCKER] `web/package.json:17-18` is §14 forbidden scope ("No new dependency or config"). `test:e2e` changed to `--project=chromium --project=mobile-360`, and a new `test:e2e:real-api` script was added (ed412e9).
  - HO-ACC-002 §1 Amended records only the `lint --max-warnings 0` change, and only as an effect of the ESLint row. It never names `web/package.json` and never mentions the e2e scripts.
  - These scripts are not in the integrator's list of authorisations for this round.
  - The change is sound and adds no dependency. Same remedy as the second-pass ESLint BLOCKER: record it in §1 Amended, or revert it.
  - The HO-ACC-002 SECURITY record's statement that `package.json` is unchanged is now stale.

- [MEDIUM] Out-of-§13 files changed in this round and not recorded:
  - `web/playwright.config.ts:16-53`: the `real-api` project, and `testIgnore` on the two default projects. §1 Amended records only the earlier `PORT` change.
  - `web/tests/unit/lib/pagination-caps.test.ts`: a new file; `tests/unit/**` is not in §13.
  - Also, the comment at `playwright.config.ts:46-48` says the real-api project is "not run at all unless `--project=real-api` is passed". A bare `npx playwright test` runs all three projects, including `real-api`, which then fails against MSW. Only the npm script is structural.

- [MEDIUM] `docs/handover/HO-ACC-001.md:16`, the Amended cell as resolved in 108e0db. The text from both sides is present, but:
  - **(a)** The heading of ACC-002's own row, `**2026-09-26 — `, was dropped at the join. The cell now reads "…See HO-ADM-001 §1 Amended (50). HO-ACC-002 first-pass gate fix round, … edit).**", which leaves an unbalanced `**` (the cell has an odd count). It also runs master's integrator row into ACC-002's row.
  - **(b)** The item numbers now collide. Master's integrator row is item **(21)** (the PUT `/api/customers/me` NUL fix), and ACC-002's rows are also **(21)**, **(22)** and **(23)**.
    - HO-ACC-002 §1 Amended cites "HO-ACC-001 §1 Amended (21)/(22)".
    - Item (23) itself says "((21) above)".
    - Both references are now ambiguous.
  - **Fix.** Restore the header and renumber ACC-002's items to (22)–(24), including their cross-references.
  - The second pass predicted no collision. That held for the ADM-001 merge, but not for the later integrator-host row.

- [LOW] `web/components/account/UnsavedChangesGuard.tsx:112-160`, the sentinel-history technique (ADR-FE-013). On layering it is fine:
  - The state is component-local, with no global store.
  - It is used only by `ProfileForm` and `NotificationPreferenceMatrix` in the customer portal.
  - It has no interaction with `IdleLock` or `StaffSessionGuard`, which are staff-only.
  - `CustomerSessionGuard`'s `router.replace` is neither a click nor a `popstate`, so a session expiry is not intercepted, which is correct per §8.4.
  - The residual gaps below are for CODE_REVIEW/TEST, beyond the rapid double Back that ADR-FE-013 already accepts:
    - **(a)** When the guarded page is the tab's first entry, `history.go(-2)` is a silent no-op. It leaves `isProgrammaticNavRef` set, so the next real Back is swallowed unguarded.
    - **(b)** When the previous entry is another document, the confirmed Leave fires the native `beforeunload` prompt too, because `when` is still true. The user gets a double prompt.
    - **(c)** Each dirty, clean, dirty cycle without leaving pushes another sentinel, so extra Back presses do nothing.
    - **(d)** Next 15.5 patches `history.pushState`. The runtime interplay with the App Router's own `popstate` handling is **TEST**.

- [LOW] `web/components/staff/IdleLock.tsx:75-77,115` and `StaffSessionGuard.tsx:39-40`: a residual ordering race.
  - `lockedRef` is updated in a passive effect after the lock commits. The guard also sees `locked` only after that render.
  - If the lock is triggered by a server response rather than the local timer, a `session_invalid` processed before that commit still clears the cache and redirects. Example: a `session_invalid` from a concurrent request, or a session GET resolving `null`, arriving ahead of the `session_idle_timeout` response.
  - This is low probability, because the local timer normally locks first. **TEST** against the real API.

- [LOW] `web/eslint.config.mjs:64-68`: the comment claims `**/lib/hooks/*` does not match "an unrelated package". A probe with `some-pkg/lib/hooks/y` is rejected. No such package exists, so this is cosmetic.

## Second-pass findings: disposition

| Second-pass finding | Status now |
|---|---|
| HIGH idle lock vs real API | **Layout-level part closed; page-level residue open.** See the HIGH above. |
| BLOCKER `eslint.config.mjs` unrecorded | **Closed.** |
| MEDIUM unrecorded out-of-§13 files | **Closed** for every file the second pass named. New ones are listed above. |
| LOW ESLint rule | **Closed.** |
| LOW `__apiBaseUrl` in production | **Closed.** |
| LOW E-X3 | **Closed.** |
| LOW README:176 | **Closed.** |
| LOW component size | **Carried**, for CODE_REVIEW. |

Detail for each:

- **HIGH idle lock vs real API: layout-level part closed; page-level residue open.**
  - `StaffLockProvider` is above both `StaffSessionGuard` and `IdleLock` (`(staff)/layout.tsx`).
  - While locked, the guard counts a `null` session as authenticated and never redirects. The client handler returns early on `session_invalid` (`lockedRef`), so there is no `clear()` and no redirect.
  - **Unlock:** a re-sign-in as the same staff member calls `unlock()`.
  - **Different person:** the only way out is "discard", which does `clear()` and `location.reload()`, resetting the provider.
  - **Genuine sign-out from another tab while locked:** the lock stays up, and a re-sign-in creates a new session and unlocks. Discard is always available. `/staff/sign-in` is in `(auth)`, outside the provider, so no stale `locked` state is carried there.
  - **Revoked or deactivated account while locked:** re-authentication fails, and discard is the exit.
  - **No lock that can never be released:** confirmed.
  - **Non-idle `session_invalid` while not locked** (a role change or admin revocation): the guard and handler still clear and redirect as before, and are not misread as locked.
  - **Mock:** it now ends the session and expires `pc_staff_session` on the first idle detection (`endStaffSessionForIdleTimeout`, at all three call sites), matching `SessionAuthenticationMiddleware`.
  - `staff-shell.tsx` is null-safe on `session`.
- **BLOCKER `eslint.config.mjs` unrecorded: closed.** It is recorded in HO-ACC-002 §1 Amended (2026-09-26, ARCHITECTURE second pass).
- **MEDIUM unrecorded out-of-§13 files: closed for every file the second pass named.**
  - Recorded: `dialog.tsx`, the 12 `cat-*` specs, the 7 `staff-shell-*` specs, `home.spec.ts`, the four ACC e2e specs and `next-path.test.ts`.
  - The staff-shell test path is corrected.
  - The new files from this round are listed in the MEDIUM above.
- **LOW ESLint: closed.**
  - The rule is `error`.
  - It catches relative imports.
  - The `SignOutButton` override lifts only the hooks-folder ban.
  - `npm run lint` uses `--max-warnings 0`.
- **LOW `__apiBaseUrl` in production: closed.** It is gated on `NODE_ENV`.
- **LOW E-X3: closed.** The deviation is recorded in §1 Amended.
- **LOW README:176: closed.**
- **LOW component size: carried**, for CODE_REVIEW.

The backend change in 3cd8c67 (`AccountOptions.CsrfCookieDomain` `[RegularExpression]` with `ValidateOnStart`, plus its tests) is integrator-authorised and recorded as HO-ACC-001 Amended (23). It has no layering impact.

SCOPE CHECK
- **Files in allowed scope or recorded: 23/26.**
  - This counts §13 files plus files recorded in §1 Amended of HO-ACC-002 or HO-ACC-001.
  - The handover docs themselves count as Amended-row edits.
  - `DuplicateCustomerGroupsEndpointTests.cs` is recorded as HO-ACC-001 (22). This round's net change to it, after the merge, is one comment phrase.
- **Files outside allowed scope: 3.**
  - `web/package.json`: §14 forbidden scope, e2e scripts unrecorded (**BLOCKER**).
  - `web/playwright.config.ts`: the real-api project split is unrecorded (MEDIUM).
  - `web/tests/unit/lib/pagination-caps.test.ts`: unrecorded (MEDIUM).

---

# Second pass (ac8c519, 2026-09-26, FAIL)

This pass covers branch `ho/ACC-002` at `ac8c519`, reviewed as `git diff 7abc9e4...ac8c519` (166 files). It focuses on the fix round `f90d465..ac8c519`.

## Governing documents

- HO-ACC-002 §3, §8, §9, §11–§14 and its §1 Amended rows.
- HO-CAT-002 §8.2.
- HO-ACC-001 §12 and its §1 Amended (21)/(22).
- For the integrator-authorised backend additions: `.ai/architecture.md`, `.ai/read-side-pattern.md` and `.ai/persistence-ef-core.md`.

## Static checks

In `web/`:
- `npx tsc --noEmit` exited 0.
- `npx eslint .` exited 0.
- `npx prettier --check .` exited 0.

Backend: `dotnet build CatEcommerce.sln` was run from a clean `git archive ac8c519` in a new scratch folder with no git-ignored appsettings. Result: 0 errors. The 20 warnings are all NU1900, meaning the private feed was unreachable for vulnerability data. The scratch folder was deleted afterwards.

No tests or dev servers were run. Runtime claims below are marked for TEST.

## Merge checks

**`git merge-tree --write-tree master ho/ACC-002`** is clean (tree `bef7c4c`).

**`git merge-tree --write-tree ho/ADM-001 ho/ACC-002`** has 1 conflict:
- **`docs/handover/HO-ACC-001.md`**: the single-line `| Amended |` table cell. ADM-001 inserts its "2026-09-26 — by HO-ADM-001, gate fix" row (its own item (10), ADR-043), and ACC-002 appends items (21)/(22) in the same cell. Resolve by keeping both texts; the item numbers do not collide.
- **`appsettings.json`** auto-merges cleanly. ADM-001 changes other sections; `Account:CsrfCookieDomain` survives.
- **`DuplicateCustomerGroupsEndpointTests.cs`** auto-merges cleanly. ADM-001 moves `RegisterCatAsync` onto a Reception client (ADR-043). ACC-002's two new tests register only customers, which is still anonymous-allowed. ACC-002's new `WalkInEndpointTests` case already registers its cat through a Reception client, so it is compatible with ADR-043.
- No ADM-001 file constructs `SessionCookieWriter`, `StaffCustomerViewModel` or `DuplicateCustomerMemberViewModel` positionally, so the new constructor parameters do not break it.
- Whether the merged tree compiles and its tests pass was not verified here. **TEST/integrator.**

FINDINGS
- [HIGH] `web/components/staff/IdleLock.tsx:96-106`, `web/components/staff/StaffSessionGuard.tsx:25-27`, `web/mocks/handlers/areas/accounts.ts:727-729,1450` — **first-pass HIGH 1 is only half closed.** This still breaks §8.4 ("`401 session_idle_timeout` → do not clear and do not navigate — the idle lock takes over") and ADR-FE-010.
  - **What is fixed.** The first detection now locks in place. `getStaffSessionStatus` throws `StaffSessionIdleTimeoutError`, the guard keeps `children` mounted, and `IdleLock` locks.
  - **What the real API does next.** On that same detection it ends the session (`EndForIdleTimeout` via C14) and expires `pc_staff_session`. See `SessionAuthenticationMiddleware.cs:233-257` (passive) and `:280-287` (non-passive). From then on, every call returns `401 session_invalid`: `StaffSessionController.cs:37` when there is no cookie, `TryResolveStaffAsync:207` for an ended row, and `RequireActionFilter` after ADM-001.
  - **Neither client path checks `locked`:**
    - (a) The next `['session','staff']` refetch resolves `null`. Examples: the next `visibilitychange`, since the query still holds its old `dataUpdatedAt` and so is stale (query-core `isStaleByTime`/`shouldFetchOn` verified), or any remount. The guard's `isIdleTimeout` is then false, `unauthenticated` is true, and it calls `router.replace('/staff/sign-in')`.
    - (b) Any other staff call hits `IdleLock`'s own handler with `session_invalid`, which runs `queryClient.clear()` and then `router.replace('/staff/sign-in')`. That call can be a page query refetched on the same focus event (TanStack refetches all stale active queries concurrently, so the session GET and a page GET race on the server) or a submit attempted just before the lock.
  - **Effect.** Either path unmounts `IdleLock` and the page with its unsaved react-hook-form state. This is the US-ACC-08 AC2 loss. Realistic trigger: a staff member comes back to the tab, sees the lock, switches to a password manager and returns.
  - **Why the tests pass anyway.** The MSW mock never ends the session: `forcedIdleExpired` makes every later read return `session_idle_timeout` again. So the mock diverges from the real API, and the new unit test and the e2e test (8aa2eeb) both pass.
  - **Fix direction** (implementer's call). While `locked`, treat `session_invalid` and a `null` session as "still locked" rather than signed out. That applies to both the guard and the handler. Alternatively, pin the lock state above the guard's redirect decision. Also make the mock end the session on first idle detection.
  - **TEST:** add the case "lock, then a second focus refetch, then a page query". Run it against the MSW mock once the mock is corrected, and against the real API.
- [BLOCKER] `web/eslint.config.mjs` — this is HO-ACC-002 §14 forbidden scope ("No new dependency or config (§10)").
  - It was edited in 20fad47, which added a per-folder `no-restricted-imports` block and a `SignOutButton` override.
  - The edit has no §1 Amended row and was not in the integrator's list of authorisations for this round.
  - The change itself is additive and sound, and it adds no dependency. If the integrator did authorise it, recording a §1 Amended row closes this finding. Otherwise it must be reverted. A §14 file changed without a record is a BLOCKER under this gate's rules.
- [MEDIUM] Out-of-§13 files in the fix round are not recorded in §1 Amended, despite the round's claim that they are.
  - `web/components/ui/dialog.tsx`: the additive `overlayClassName` prop. It is called integrator-authorised only in the 7865ee5 commit message and in a code comment.
  - 12 HO-CAT-002 specs `web/tests/e2e/cat-*.spec.ts`, 7 `staff-shell-*.spec.ts`, and `home.spec.ts`. These are consequential edits: the shared fixture no longer signs in automatically.
  - `web/tests/e2e/{account-area,accounts-auth,accounts-a11y-responsive,real-api-verification}.spec.ts`. These were already flagged in the first pass.
  - `web/tests/unit/lib/next-path.test.ts`.
  - The Amended row names `web/tests/components/staff/staff-shell.test.tsx`, but the file actually edited is `web/tests/unit/staff-shell.test.tsx`.
  - All of these are low-risk, and each needs a row.
- [LOW] `web/eslint.config.mjs:57-85` — verified with deliberate violations via `eslint --stdin --stdin-filename` (nothing was written to the tree).
  - **What works.** The rule fires on `@/lib/hooks`, `@/lib/hooks/*` and nested `@/lib/hooks/a/b` in all four folders, including subfolders. It is silent in `components/{staff,cat}` and on `SignOutButton.tsx`, which are the specified exemptions. No component in the four folders imports `lib/hooks` today, so the HIGH 3 substance is closed.
  - **Gap: relative imports.** A relative import (`../../lib/hooks/useCaptcha`) is **not** caught.
  - **Gap: severity.** The rule is `warn`, and `npm run lint` is `eslint .` with no `--max-warnings 0`, so a violation never fails the lint step. The HO-CAT-002 base rule is `warn` too, so this is consistent with it, but weaker than the round's "enforce" claim.
  - **Gap: override too broad.** The `SignOutButton` override turns the whole rule off, including the `useQuery`/`useMutation` ban.
- [LOW] `web/app/providers.tsx:44-50` — HO-ACC-002 §13 limits QueryProvider edits to "register onSessionError only". This round removes that registration, which is correct because it moved to the guards. It also adds a `window.__apiBaseUrl` global in every environment, including production, purely for the Playwright fixture. The value is already public (`NEXT_PUBLIC_*`), so there is no exposure. It is still test plumbing in app code; gate it the way `__mswReady` is gated, or record it.
- [LOW] `web/tests/e2e/accounts-a11y-responsive.spec.ts:82-104` — the E-X3 deviation (MSW's own `localStorage` entry and the mock DB in `sessionStorage`) is explained only in a test comment. The first pass asked for it to be recorded in the handover or the TC document. The plaintext-password part is fixed in 3dd9114 and is now asserted.
- [LOW] `web/README.md:176` — still says "`DataTable` is built on this" (the `Table` family). 5e1f120 made that untrue. The other two stale lines from the first pass are fixed.
- [LOW] Component size is for CODE_REVIEW, and it is carried over from the first pass. Several components still exceed §11's ~150 lines: `IdleLock` 265, `MergeReview` 224, `RegisterForm` 203, `ProfileForm` 198, `StaffAccountForm` 186, `NotificationPreferenceMatrix` 179. 55a4223 acknowledges this.

## First-pass findings: disposition

| First-pass finding | Status now |
|---|---|
| HIGH 1 — idle timeout redirects instead of locking | **Partially closed.** See the HIGH above. |
| HIGH 2 — audience guessed from `/api/staff` prefix | **Closed.** |
| HIGH 3 — components fetch and mutate their own data | **Closed.** |
| MEDIUM `staffIdleLockBridge.ts` | **Closed.** |
| MEDIUM `web/docs/decisions.md` | **Closed.** |
| MEDIUM `UnsavedChangesGuard` | **Closed.** |
| MEDIUM MSW `sessionStorage` | **Mostly closed.** |
| MEDIUM shared fixture | **Closed.** |
| LOW `PagedResult<T>` | **Closed.** |
| LOW `?? 30` fallback | **Closed.** |
| LOW README | **Partially closed.** |
| LOW `playwright.config.ts` PORT | **Closed.** |
| LOW component size | **Carried.** |
| LOW §13 paths | **Closed.** |

Detail for each:

- **HIGH 2 — closed.**
  - `client.ts` no longer infers the audience. `CustomerSessionGuard` registers the customer handler. `IdleLock`, which is mounted only inside a resolved `StaffSessionGuard`, registers the staff handler.
  - Checked for windows where no handler, or the wrong one, is registered:
    - A staff page cannot fire a call before the handler exists. The guard renders nothing while pending. Page effects run child-first but in the same commit as `IdleLock`'s effect, and every response is asynchronous.
    - Only one of `(portal)` and `(staff)` is ever mounted.
    - On a cross-group navigation, React runs every passive unmount (`setSessionErrorHandler(null)`) before any passive mount.
  - Residual, not scored: a staff request still in flight across a staff→portal client navigation would reach the customer handler. This is an unrealistic flow.
- **HIGH 3 — closed.**
  - Every listed component now takes props and callbacks, and the pages wire `lib/hooks`.
  - Grep finds no hook calls in `components/{account,admin,auth,customer}`, only names in comments. `SignOutButton` is the specified exemption.
  - Rule gaps are in the LOW above.
- **MEDIUM `staffIdleLockBridge.ts` — closed.** The file is deleted, has no remaining reference, and is recorded in §1 Amended.
- **MEDIUM `web/docs/decisions.md` — closed.** It exists and holds ADR-FE-001…012 plus a new ADR-FE-013 (`UnsavedChangesGuard`). ADR-FE-002 carries the owner's 2026-09-26 correction.
- **MEDIUM `UnsavedChangesGuard` — closed.** It uses the shared `ConfirmDialog`; recorded as ADR-FE-013.
- **MEDIUM MSW `sessionStorage` — mostly closed.** No plaintext password is stored any more. The recording is still open (LOW above).
- **MEDIUM shared fixture — closed.** It uses one audience per test, an absolute URL and a loud failure. Real-API credential seeding is for TEST.
- **LOW `PagedResult<T>` — closed.** It lives in `lib/api/types.ts`.
- **LOW `?? 30` fallback — closed.** The server's `idleTimeoutMinutes` from the 401 is preferred.
- **LOW README — partially closed.** One stale line remains.
- **LOW `playwright.config.ts` PORT — closed.** Recorded in §1 Amended.
- **LOW component size — carried.**
- **LOW §13 paths — closed.** Recorded in §1 Amended (2).

## Integrator-authorised items this round: design review, no finding

**`pc_csrf` Domain.**
- `AccountOptions.CsrfCookieDomain`: empty means host-only.
- It applies only to `WriteCsrf` and `ExpireCsrf`. The session cookies stay host-only and HttpOnly; `Expire` passes `domain: null` for both.
- `CustomerProfileController.DeleteAccount` now expires cookies through `SessionCookieWriter`, so the Domain matches. Injecting `SessionCookieWriter` there is the same class of exception as HO-ACC-001 Amended (13), and it is recorded in (21).
- `SessionCookieWriter` is Scoped, over `IOptions` (a Singleton), so it is lifetime-safe.
- `CsrfCookieDomainTests` covers both configurations.

**`app/page.tsx`.**
- It asks the API client-side through the same session hooks and fails closed to `/sign-in`.
- Idle-timeout routes to `/staff/cats`, where the HIGH above applies.
- It no longer reads cookies.

**CR MEDIUM 2, read side only.**
- `StaffCustomerViewModel` gains `Cats`, reusing `DuplicateCustomerCatViewModel`. `DuplicateCustomerMemberViewModel` gains `RecordStatus` and `RegistrationChannel`.
- Both query handlers stay in `Read`. They use `AsNoTracking()`, project with `Select` into view models, write nothing and rehydrate no aggregate.
- The derivation uses the existing `CustomerRecordStatusRules.Derive` and the Enumeration `DisplayName`, the same way Q-H1 already did.
- The per-customer cat list is a bounded read, not an aggregate collection.
- There is no Domain, Application, EF mapping or migration change. No `IsMarkedToDelete` filter was added, which matches the existing Read handlers.
- The frontend types (`cats`, `recordStatus`, `registrationChannel`) and the MSW bodies match the view models.

SCOPE CHECK
- files in allowed scope: 109/166 by §13's literal globs, including the three `docs/gates/HO-ACC-002-*.md` files.
- outside §13, but authorised, recorded or in scope by §13's own wording, so not scored: 30.
  - `docs/handover/HO-ACC-00{1,2}.md`.
  - 13 backend files: `src/**` ×8 and `tests/**` ×5, integrator-authorised this round and recorded in HO-ACC-001 Amended (21)/(22).
  - `web/.env.local.example` and `web/app/providers.tsx` (§13).
  - `web/README.md` and `web/app/page.tsx` (owner decisions).
  - `web/components/common/{DataTable,portal-header,staff-shell}.tsx` and `web/components/cat/WeightTrendChart.tsx`.
  - `web/lib/identity.ts` (deleted per §13).
  - `web/mocks/handlers/areas/accounts.ts`, `web/nav/areas/accounts.ts`, `web/playwright.config.ts`, `web/tests/e2e/fixtures.ts`, `web/tests/components/cross-cutting.test.tsx` and `web/tests/unit/staff-shell.test.tsx` (recorded, though under the wrong path).
- files outside allowed scope, unrecorded: 27.
  - `web/eslint.config.mjs` (**§14 forbidden**).
  - `web/components/ui/dialog.tsx`.
  - `web/tests/e2e/cat-*.spec.ts` ×12, `staff-shell-*.spec.ts` ×7 and `home.spec.ts`.
  - `web/tests/e2e/{account-area,accounts-auth,accounts-a11y-responsive,real-api-verification}.spec.ts`.
  - `web/tests/unit/lib/next-path.test.ts`.

---

# First pass (condensed) — 5e1f120, 2026-09-26, FAIL

- **Reviewed:** `git diff 68451a8...5e1f120`, 116 files, 13 commits. tsc and eslint were clean.
- **HIGH 1:** the idle-timeout `401` on a focus refetch mapped to `null`, so the guard redirected and unmounted `IdleLock` and the page.
- **HIGH 2:** the session-error audience was guessed from the `/api/staff` path prefix.
- **HIGH 3:** `components/{account,admin,auth,customer}` fetched and mutated their own data, against HO-CAT-002 §8.2 rule 4.
- **MEDIUMs:**
  - `staffIdleLockBridge.ts` was out of scope.
  - `web/docs/decisions.md` was missing.
  - `window.confirm` was used in `UnsavedChangesGuard`.
  - The MSW `sessionStorage` held plaintext passwords, with E-X3 relaxed.
  - The shared fixture signed in both audiences with a relative fetch.
- **LOWs:**
  - `PagedResult` was placed in an area module.
  - `IdleLock` had a `?? 30` fallback.
  - `README` had stale lines.
  - The `PORT` change was unrecorded.
  - Several components were oversized.
  - §13 had stale paths.
- **Scope:** 101/116 in scope, 6 authorised, 9 unauthorised.
