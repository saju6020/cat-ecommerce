## Second pass (delta)

GATE: ARCHITECTURE
HANDOVER: HO-CAT-002 (follow-up: `/cats/{id}` smart redirect, owner rule change of 2026-09-27, branch `ho/CAT-002-qr`)
COMMITS: e8a6d25 01309bc 9f94bee b543474 (reviewed as `git diff 1660019 HEAD`)
DATE: 2026-09-28
STATUS: PASS

This is a delta pass. It covers only what changed since the first pass below, which reviewed
`08e873e..6027ee8`. Nothing has been built, type-checked or run yet, so runtime claims are marked
for TEST. There are no BLOCKER or HIGH findings. By owner rule, the LOWs do not fail the gate. They
go to `docs/backlog/CAT.md`.

SPEC USED
- HO-CAT-002 §8.2 (layering rules 2 and 3), §8.4 (state and the query-key convention), §12.5
  (mocks), §13 and §14.
- The new HO-CAT-002 §1 Amended row (`docs/handover/HO-CAT-002.md:29`), which records the owner
  decision.
- HO-ACC-002 conventions: `lib/validation/nextPath.ts`, the `app/page.tsx` session-priority order,
  the fail-closed guards, and the mock test-hook precedent (`window.__forceStaffIdleExpired` in
  `accounts.ts`, `window.__forceCatQuerySessionInvalid` in `cat-profiles.ts`).

QUESTIONS ASKED
1. **Is changing `useCat`'s signature acceptable? Yes, and it conforms.**
   - `web/lib/hooks/useCat.ts:20` adds an optional second parameter, `options?: { enabled?: boolean }`.
     It defaults to `true`, so all 12 existing call sites (10 pages plus `CatProfileDataShell`)
     compile and behave exactly as before.
   - The query is still declared only in `lib/hooks/` (§8.2 rule 3). The page does not call
     `useQuery`.
   - The fetch still goes `getCat` → `apiFetch` (§8.2 rule 2). No endpoint function or contract
     type was added.
   - The key is still `['cat', catId]` (§8.4). Because the key is shared, the ownership check's
     result warms the cache that `/portal/cats/{id}` reads after `router.replace`. The owner path
     therefore does not add a duplicate fetch.
   - No new endpoint was added, and the call is an existing HO-CAT-001 read. There is no §20.2
     stop. This replaces first-pass Decision 2 because the owner changed the rule. It does not
     reverse an architecture ruling.
   - The gate uses `validId && isSessionResolved && !isStaffSignedIn && isCustomerSignedIn`
     (`page.tsx:89-90`). Staff and anonymous visitors therefore never issue the call.
   - `isOwnershipResolved` (`page.tsx:97`) short-circuits on `!shouldCheckOwnership`. This handles
     TanStack v5 reporting `isPending: true` for a disabled query with no data. The page cannot
     stall on a disabled query.
2. **Is the mock hook placed correctly, and is its departure from the precedent justified? Yes to
   both.**
   - Placement: `consumeForceCatQueryNotOwned()` is in `mocks/handlers/areas/cat-profiles.ts`,
     the module that owns `GET /api/cats/{catId}`. It is checked in that handler, after the
     existing 401 force and before the fixture lookup (`cat-profiles.ts:863`). It does not read
     `accounts.ts` state, so it respects the one-area-per-mock-file rule.
   - The mock cannot compute ownership. Modelling a non-owner as a forced `404` matches the real
     query-filter behaviour described in HO-CAT-001 §20.3. It is a stand-in, not a new contract.
   - Why it differs from the precedent: the existing hooks are functions that a test calls with
     `page.evaluate` on a page that is already loaded, before a refetch that happens in place. Here
     the check fires on first mount after a full `page.goto`, which discards the old window and its
     module instance. `addInitScript` is the only harness mechanism that puts a value on the new
     document before the app's scripts run. The reason is technical and documented where the hook
     is defined.
   - Repo precedent: `tests/e2e/account-area.spec.ts:160` already uses `addInitScript`, so this
     adds no new test mechanism.
   - The hook uses no web storage and no app state. It is dormant unless a test sets it, and the
     existing `catServerError` fixture is reused for the 5xx case rather than adding a second flag.
3. **Does the new §1 Amended row describe the shipped code, and does it correct the three
   first-pass LOWs? Yes, with one inexactness (LOW 1 below).**
   - The row matches the code:
     - The destinations match `page.tsx:109-122`.
     - `sanitizeNextPath(…, '/staff/cats', '/staff/cats')` is used, and the return trip is honoured
       by `(auth)/staff/sign-in/page.tsx:39` (prefix `/staff/`).
     - Staff are checked first and never trigger a cat call.
     - A 404, a 403, a 5xx or a network error all count as "not owner".
     - The files-touched list matches the diff exactly, 7 files.
     - The `cat-responsive.spec.ts` cases are unchanged: not in the diff, and lines 21 and 27 still
       cover both destinations that did not change.
     - `cat-a11y.spec.ts` now has four `/cats/{id}` cases (lines 63, 72, 81 and 98).
     - The cited commits `071cbaf`, `d6ce94f` and `1660019` are the three earlier gate records.
   - First-pass LOW 1 (sessions still issued for an invalid id): corrected in correction (a) of the
     row, and in the page doc comment at `page.tsx:67-72`.
   - First-pass LOW 2 (the responsive-loop claim and the `web/**` claim): corrected in corrections
     (b) and (c) of the row. Both now match the code.
   - First-pass LOW 3 (a non-owner customer went to the portal): superseded. The owner decision now
     sends that customer to staff sign-in, and the row records it as the owner's decision.
     ACCEPTANCE still needs to confirm the owner instruction; that is a spec question, not an
     architecture one.

CHECKLIST (delta only)
- **One fetch site, hooks only in `lib/hooks`, no web storage, `router.replace` only, the shared
  `sanitizeNextPath`: all comply.**
  - Every branch uses `replace`.
  - The staff `next` is built through the shared helper, with no hand-rolled check.
- **Session-priority and fail-closed conventions: comply.**
  - Staff still wins first, and `StaffSessionIdleTimeoutError` still counts as staff.
  - A failed ownership check fails closed, like the guards do.
- **Dependencies: none added.** `package.json` and the lockfile are not in the diff.

FINDINGS
- [LOW] `docs/handover/HO-CAT-002.md:29` and `web/mocks/handlers/areas/cat-profiles.ts:516-518,563-573`:
  the hook is described as "one-shot", for "exactly one upcoming request", evaluated in "the NEXT
  document".
  - Playwright's `page.addInitScript` runs again on **every** later navigation in that page, not
    only the next one. Each new document re-arms `__forceCatQueryNotOwned = true`, and the
    clear-on-read resets it only within the current document.
  - The current tests are not affected: after the redirect they land on `/staff/sign-in`, which
    makes no cat call.
  - A future test that signs in as staff after the forced redirect, and then loads a cat page,
    would get an unexpected `404`.
  - The fix is to the wording only. Alternatively, the test could clear the flag before any further
    navigation.
- [LOW] `web/mocks/handlers/areas/cat-profiles.ts:570`: the comment `// Vitest's jsdom — never forced`
  is on the `typeof window === 'undefined'` branch.
  - Under jsdom, `window` is defined, so that branch runs only in a pure Node environment.
  - Under jsdom the function still returns `false`, because no unit test sets the flag. The
    behaviour is harmless and the comment is the only error.

NOTES FOR LATER GATES (not findings)
- **Stale cache (the first pass's note, now applied to ownership).** `isOwner = Boolean(ownershipQuery.data)`
  (`page.tsx:93`) treats any cached `['cat', id]` data as ownership, even while a refetch is running
  or has failed.
  - On a full page load from a QR scan the cache is empty, so the check always really runs.
  - After a client-side navigation with a warm cache, a stale "owner" result would send the visitor
    to `/portal/cats/{id}`. That page still enforces ownership on the server, so this is not a
    boundary breach.
  - This is the same pattern the first pass accepted for `app/page.tsx`.
- **ACCEPTANCE (UX consequence of the owner rule):**
  - A **signed-out owner** who scans their own cat's card now lands on staff sign-in with
    `next=/staff/cats/{id}`, and has no direct path back to customer sign-in.
  - A signed-in non-owner is shown staff sign-in.
  - Both follow the owner rule as recorded. The owner should see these consequences.
- **SECURITY:** a signed-in customer's call is the only one the page makes, and only for a valid
  GUID. Anonymous visitors still trigger no cat call (unit tests at
  `cat-smart-redirect-page.test.tsx:115,139,151,208,220` assert `getCatMock` was not called).
- **TEST:**
  - Type-check `useCat`'s new signature across all 12 call sites.
  - Confirm that `vi.mock('@/lib/api/cats', () => ({ getCat }))`, which exports only `getCat`,
    does not break any other import `useCat.ts` resolves from that module.
  - Run `cat-smart-redirect-page.test.tsx`, `cat-smart-redirect.spec.ts` and `cat-a11y.spec.ts` in
    both Playwright projects.
  - Confirm that `addInitScript` sets the flag before the MSW handler module first reads it.

SCOPE CHECK
- files in allowed scope: 7/7.
  - 6 are under `web/**` (§13):
    - `app/cats/[id]/page.tsx`
    - `lib/hooks/useCat.ts`
    - `mocks/handlers/areas/cat-profiles.ts`
    - `tests/e2e/cat-a11y.spec.ts`
    - `tests/e2e/cat-smart-redirect.spec.ts`
    - `tests/unit/cat/cat-smart-redirect-page.test.tsx`
  - `docs/handover/HO-CAT-002.md` gets one appended §1 Amended row, the handover's own record, per
    precedent. The `*(append further rows here)*` marker is still present.
- files outside allowed scope: none. Nothing touches §14: no `src/**`, `tests/**`, `.ai/**`,
  `.claude/**`, `docs/handover/HO-CAT-001.md`, or .NET build files.

---

## First pass

GATE: ARCHITECTURE
HANDOVER: HO-CAT-002 (follow-up: the `/cats/{id}` smart-redirect page, branch `ho/CAT-002-qr`)
COMMITS: 8738d2e 5b7447a 9558066 6027ee8 (reviewed as `git diff 08e873e HEAD`, on master 08e873e)
DATE: 2026-09-27
STATUS: PASS

This gate ran first. Nothing has built, type-checked or run the tests yet, so every runtime claim
below is marked for the TEST gate. There are no BLOCKER or HIGH findings. By owner rule, the LOW
findings do not fail the gate. They go to `docs/backlog/CAT.md`.

SPEC USED
- HO-CAT-002 §1 Amended, 2026-09-27 row "The cage-card QR now opens…".
- HO-CAT-001 §1 Amended, 2026-09-27, item (a): staff go to `/staff/cats/{id}`, the owner goes to
  `/portal/cats/{id}`, and anyone else goes to sign-in.
- HO-CAT-002 §6, §8 (§8.2 layering, §8.3 route map, §8.4 state), §13 and §14.
- The web conventions HO-ACC-002 set up: `app/page.tsx`, `lib/hooks/useSession.ts`,
  `lib/validation/nextPath.ts`, `CustomerSessionGuard` and `StaffSessionGuard`.
- Per HO-CAT-002 §6, `.ai/` covers the backend only. Its CQRS and aggregate rules do not apply to
  this React-only change, and they were not applied.

CHECKLIST
- **One fetch site (§8.2 rule 2): complies.**
  - `app/cats/[id]/page.tsx` and `lib/validation/catId.ts` do not call `fetch`.
  - The only network access goes through the existing hooks, which call `lib/api/auth.ts` and
    `lib/api/staff-auth.ts`, which call `client.ts`.
  - No new endpoint function or contract type was added.
- **Hooks only in `lib/hooks` (§8.2 rule 3): complies.**
  - There is no `useQuery` or `useMutation` in the page.
  - The page uses `useStaffSession()` and `useCustomerSession()` unchanged.
  - No hook is added or modified.
- **No web storage (§8.4): complies.**
  - Nothing uses `localStorage`, `sessionStorage`, a store or Context-as-store.
  - Session state is read only from the TanStack Query hooks (HO-ACC-002 §8.4).
- **`router.replace`: complies.** All three branches (page.tsx:76, :81, :92) use `replace`, and
  the page never uses `push`. The QR landing page therefore leaves no history entry to go back to,
  which matches `app/page.tsx`.
- **`sanitizeNextPath`: complies.**
  - page.tsx:87-92 uses the shared helper from `lib/validation/nextPath.ts` and adds no second
    open-redirect check.
  - The prefix is `/portal/cats` and the fallback is `/portal/cats`. That is a subset of what
    `(auth)/sign-in` accepts on the way back in (`['/portal/', '/book']`), so the round trip
    survives.
  - The `next` value is URL-encoded once, and the id has already been checked as a GUID.
- **Session-guard conventions: complies.**
  - page.tsx:62-71 is the same derivation `app/page.tsx` uses, line for line:
    - staff are checked first;
    - `StaffSessionIdleTimeoutError` counts as signed-in staff, so the IdleLock overlay handles it
      on `/staff/**`;
    - the page does nothing until both queries have settled.
- **Layering and composition: complies.**
  - The page is a thin `app/` composition. It renders only the existing `LoadingState` and
    `ErrorState` from `components/common`.
  - The `"use client"` boundary is the page itself, not a layout (§8.2 rule 5).
  - Placing it outside the `(staff)` and `(portal)` route groups is right. It must not inherit
    either shell's session guard, and HO-CAT-001 (a) authorises the route. The §8.3 route map
    predates that route. The §1 Amended row is the record, so there is no finding.
- **Dependencies: none added.** No `package.json` or lockfile is in the diff.

DECISIONS ASKED FOR A VIEW
1. **A network error or 5xx on the session check fails closed to `/sign-in`: approved as
   conforming.**
   - This is the established HO-ACC-002 convention: `app/page.tsx` and both session guards treat
     `isError` as not signed in.
   - `useSession.ts` already retries a network-level failure (`status === 0`) twice before
     settling, so a transient blip does not bounce the user.
   - A separate "couldn't check your session" screen would be a second convention for the same
     question.
   - TEST should confirm the 5xx branch in `cat-smart-redirect-page.test.tsx`.
2. **A non-owner customer goes to `/portal/cats/{id}`, with no new backend call: approved as
   architecturally conforming.**
   - Checking ownership here would mean either a second fetch of the cat on this page or a new
     "is owner" endpoint. HO-CAT-001 specifies no such endpoint, and inventing one is a §20.2 stop.
   - The portal profile already gets a not-found result from the server
     (`CatProfileDataShell` → `ApiNotFoundError`, query filter per HO-CAT-001 §20.3). Ownership is
     therefore enforced where it already lives.
   - Taken literally, the spec's "anyone else → sign-in" would put a signed-in non-owner in the
     "anyone else" bucket. The §1 Amended row puts this behaviour down to the owner's instruction.
     That is a spec question, not an architecture one (see LOW 3). ACCEPTANCE should confirm the
     owner's instruction.

FINDINGS
- [LOW] `docs/handover/HO-CAT-002.md:28`: the record says a non-GUID id "never reaches a session
  round trip". That is not accurate.
  - page.tsx:62-63 calls both session hooks unconditionally, as the rules of hooks require, so
    both session GETs are still sent for an invalid id. Only their results are ignored
    (page.tsx:74, `if (!validId || !isResolved) return;`).
  - The page's own doc comment gets this right ("rejected before any session query result is
    used"). The handover row overstates it.
  - This is harmless. The fix is to the record only.
- [LOW] `docs/handover/HO-CAT-002.md:28`: two statements in the record do not match the diff.
  - It says `/cats/{id}` "could not join either file's existing generic per-screen loop". In fact
    `tests/e2e/cat-responsive.spec.ts:21,27` adds it to the `STAFF_SCREENS` and `PORTAL_SCREENS`
    loops as `{ path, expectFinalUrl }` entries. Only `cat-a11y.spec.ts` gets a dedicated case.
  - It says "Every file this row touches is under `web/**`", but the row itself is in
    `docs/handover/`. By this repo's gate precedent, that edit is an allowed §1 Amended record, so
    it is not a scope failure. The sentence is still inexact.
- [LOW] `web/app/cats/[id]/page.tsx:80-82`: a signed-in non-owner customer goes to
  `/portal/cats/{id}`, where the spec text says "anyone else → sign-in".
  - Architecturally this is the right shape (see Decision 2).
  - It is logged so that ACCEPTANCE confirms the owner's instruction the row relies on. Only
    `docs/handover/HO-CAT-002.md:28` records that instruction.

NOTES FOR LATER GATES (not findings)
- **Signed-out staff.** A signed-out staff member who scans a cage card lands on customer
  `/sign-in?next=/portal/cats/{id}`, not on staff sign-in.
  - This matches the spec ("anyone else → sign-in") and the HO-ACC-002 owner decision behind
    `app/page.tsx`: anonymous visitors never see staff sign-in by default.
  - Cage cards are used mostly by clinic staff, so ACCEPTANCE may want the owner to see this UX
    consequence.
- **Stale cached data.** Like `app/page.tsx`, this page treats `Boolean(data)` as signed in even
  if a refetch has errored and cached data is still present.
  - The guards use `!isError && session !== null`.
  - On a full page load from a QR scan there is no cache, so this does not matter there. On
    client-side navigation, the destination's guard still fails closed.
  - This mirrors the approved `app/page.tsx` exactly, so it is not a finding here.
- **For TEST:**
  - type-check and lint the new page, including the `use(params)` Promise-typed params on this
    Next.js version;
  - run `cat-smart-redirect-page.test.tsx`, `cat-id.test.ts` and `cat-smart-redirect.spec.ts` in
    both Playwright projects;
  - rerun the changed `cat-a11y` and `cat-responsive` specs.

SCOPE CHECK
- files in allowed scope: 8/8.
  - 7 are under `web/**` (§13):
    - `web/app/cats/[id]/page.tsx`
    - `web/lib/validation/catId.ts`
    - `web/tests/e2e/cat-a11y.spec.ts`
    - `web/tests/e2e/cat-responsive.spec.ts`
    - `web/tests/e2e/cat-smart-redirect.spec.ts`
    - `web/tests/unit/cat/cat-smart-redirect-page.test.tsx`
    - `web/tests/unit/lib/cat-id.test.ts`
  - `docs/handover/HO-CAT-002.md` gets a single appended §1 Amended row. That is the
    handover's own record, per the precedent in HO-CAT-002-ARCHITECTURE.md and
    HO-ACC-002-ARCHITECTURE.md. A single `*(append further rows here)*` marker remains.
- files outside allowed scope: none. Nothing touches §14: no `src/**`, `tests/**`, `.ai/**`,
  `.claude/**`, `docs/handover/HO-CAT-001.md`, or .NET build files.
