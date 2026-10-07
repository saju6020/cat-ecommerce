## Second pass (delta)

GATE: CODE_REVIEW
HANDOVER: HO-CAT-002 (owner rule change, 2026-09-27: `/cats/{id}` now sends anonymous visitors and
non-owner customers to `/staff/sign-in?next=/staff/cats/{id}`, with an ownership check for a
signed-in customer)
COMMITS: e8a6d25, 01309bc, 9f94bee, b543474 (diff 1660019..HEAD, branch ho/CAT-002-qr)
DATE: 2026-09-28
STATUS: PASS

This is a delta pass over the first pass below, scoped to `git diff 1660019 HEAD`: `web/app/cats/[id]/page.tsx`,
`web/lib/hooks/useCat.ts`, `web/mocks/handlers/areas/cat-profiles.ts`, `web/tests/e2e/cat-a11y.spec.ts`,
`web/tests/e2e/cat-smart-redirect.spec.ts`, `web/tests/unit/cat/cat-smart-redirect-page.test.tsx`,
`docs/handover/HO-CAT-002.md`.

FINDINGS
- [LOW] `web/app/cats/[id]/page.tsx:93` — `isOwner = Boolean(ownershipQuery.data)` counts a cached
  success too, not only a response fetched for this check. `useCat` inherits the global
  `staleTime: 30_000` (`app/providers.tsx:29`); it does not set its own. In TanStack Query v5, a
  query already holding `data` reports `isPending: false` immediately on mount/re-enable when the
  cached entry is not yet stale by that window, so `router.replace` can fire off a value already
  sitting in the shared `['cat', catId]` cache rather than a fresh answer to "does the CURRENT
  visitor own this". Not reachable from an actual QR scan (a full page load starts with an empty
  `QueryClient`, since `Providers` builds it via `useState(() => new QueryClient(...))` and a hard
  navigation remounts the whole tree). Sign-in, sign-out and account deletion all call
  `queryClient.clear()` (`SignOutButton.tsx:26`, `(auth)/sign-in/page.tsx:43`,
  `(auth)/staff/sign-in/page.tsx:38`, `portal/account/data/delete/page.tsx:21`), so a genuine
  cross-identity cache leak on a shared device requires an identity switch that skipped one of
  those paths, which is not a reachable flow. The only realistic case is a client-side
  navigation, same customer, same `catId`, inside the 30s window — an already-true "owns" answer
  reused within its own staleness budget, the same tolerance every other `useCat` call site (the
  destination page included) already accepts. This is the same defect *shape* as the ACC-002
  `IdleLock` bug (an ad hoc/implicit reliance on the global `staleTime` where a check implies
  "fresh"), but not the same severity: the ACC-002 case let a bystander keep trusting a session
  that had just ended with **no network call at all**; here the worst case is a wrong destination
  choice, not a wrong authorization decision — `/portal/cats/{id}` and the API still enforce
  ownership independently (§20.3's query filter), so nothing is exposed that a direct
  `GET /api/cats/{id}` wouldn't already tell the same caller. Matches SECURITY's own second-pass
  LOW on the same line (`docs/gates/HO-CAT-002-QR-SECURITY.md`) — recording it here too rather than
  silently deferring to that gate, since it is also a code-quality/correctness point on this file.
  Fix, if picked up: give the ownership check its own `staleTime: 0`, or gate `isOwner` on
  `ownershipQuery.isSuccess && !ownershipQuery.isFetching && ownershipQuery.isFetchedAfterMount`
  rather than bare `Boolean(data)`. Per the owner's gate-blocking rule, LOW does not block; goes to
  `docs/backlog/CAT.md` if not already filed there from the SECURITY pass.

No MEDIUM, HIGH or BLOCKER findings.

VERIFICATION

Redirect state machine, with the added async ownership check (`web/app/cats/[id]/page.tsx`):
- **Waits for the cat query; never redirects while it is pending.** The customer branch of the
  effect (`if (isCustomerSignedIn) { if (!isOwnershipResolved) return; ... }`, lines 116-120)
  returns before calling `router.replace` unless `isOwnershipResolved` is true.
  `isOwnershipResolved = !shouldCheckOwnership || !ownershipQuery.isPending` (line 97), so once the
  check is enabled it genuinely gates on TanStack Query's own `isPending`, not a locally-invented
  proxy. Confirmed by the "owner" and "non-owner"/"5xx" unit tests, none of which see a
  `router.replace` call before `getCatMock` has resolved or rejected.
- **No loop, no double `replace`.** Every path through the effect calls `router.replace` at most
  once and returns immediately after (lines 111-122); none of the three destinations
  (`/staff/cats/{id}`, `/portal/cats/{id}`, `/staff/sign-in?next=...`) route back through
  `/cats/{id}`, so no cycle is reachable. A successful `replace` unmounts this page's tree before
  any further effect run could fire a second navigation. Re-derived the dependency array
  (lines 123-133): every value it lists is exactly what the body reads, so no stale-closure replace
  and no missing-dependency re-fire is possible either.
- **Retry behaviour is correctly waited out.** `useCat`'s retry function (`lib/hooks/useCat.ts:25-29`)
  returns `false` immediately for `ApiNotFoundError` (no retry) and retries once
  (`failureCount < 1`) for anything else. During a retry, TanStack Query keeps the query's `status`
  at `'pending'` until the retry is exhausted or a value lands, so `ownershipQuery.isPending` stays
  `true` through the whole retry window — the effect keeps returning early and does not fire a
  premature "non-owner" redirect mid-retry. The new 5xx unit test proves this by hand: it asserts
  the eventual `staffSignInWithNext` replace only inside a `waitFor(..., { timeout: 3000 })`,
  explicitly because of TanStack Query's default backoff delay on the one retry — a real, not
  simulated, wait, so the test would fail if the effect fired early.
- **Depends on the global `staleTime`** — see the LOW finding above. This is the one point flagged
  as non-blocking, not a correctness gap in the state machine itself: the wait-for-pending logic is
  sound; the input it waits for can, in one narrow and largely unreachable case, be a cache hit
  instead of a fresh network answer.
- **Staff still wins outright, with no ownership call.** `shouldCheckOwnership` requires
  `!isStaffSignedIn` (line 91); staff idle-timeout still counts as staff (`isStaffSignedIn` includes
  `isStaffIdleTimeout`), so neither a plain staff session nor an idle one ever enables the query.
  Confirmed by three unit tests (`staff`, `staff idle-timeout`, `both sessions present`) each
  asserting `getCatMock` was never called, and by the doc comment's own claim matching the code.
- **Anonymous visitors trigger no cat call**, by construction (`shouldCheckOwnership` requires
  `isCustomerSignedIn`) — confirmed by the anonymous unit test's `expect(getCatMock).not.toHaveBeenCalled()`.

`useCat`'s `{ enabled }` addition (`web/lib/hooks/useCat.ts:20,24`):
- Purely additive: a new optional second parameter defaulting `enabled` to `true` when omitted, so
  every other call site — grepped directly: `CatProfileDataShell.tsx`, `portal/cats/[catId]/page.tsx`,
  and the seven `(staff)/staff/cats/[catId]/*` pages — compiles and behaves exactly as before
  (none of them pass a second argument). The query key, `queryFn`, and retry policy are unchanged
  for every caller. Verified against the diff directly, not merely by reading the comment's claim.

Query-key sharing (`['cat', catId]`, `catQueryKey`) is unchanged and deliberate — this is what lets
the ownership check's successful fetch warm the cache the destination `/portal/cats/{id}` page then
reads, per the file's own doc comment and the handover row; it is also the mechanism behind the LOW
finding above, since the same key is shared with every other reader of a given cat, not scoped by
caller identity.

Test determinism:
- Unit tests mock `@/lib/api/cats`'s `getCat` at the same layer as the two session endpoints
  (`vi.mock`), so no reliance on real timers for the pending/resolved transitions except the one
  deliberate real-retry-backoff case, which is called out with its own comment and a widened
  `waitFor` timeout rather than a fixed `sleep`.
  `beforeEach` resets `getCatMock` alongside the two session mocks, so no cross-test bleed.
- E2E: the new `__forceCatQueryNotOwned` hook is set via `page.addInitScript`, not `page.evaluate`,
  specifically because the ownership check fires immediately after a full navigation that would
  tear down anything set on the pre-navigation page first — the right mechanism for a one-shot flag
  that must exist before the destination document's own scripts run, avoiding the race the file's
  own comment identifies. The 5xx e2e case reuses the pre-existing `catServerError` fixture (always
  500 on GET) instead of inventing a second force-flag, keeping the mock surface smaller.
  `resetCatProfilesMockData` is a full per-test reset for the module's own tracked state
  (`forceNextCatQuery401`); the new flag has no module-level state to reset since it lives on
  `window` and is one-shot-consumed on read (`consumeForceCatQueryNotOwned`).

Convention checks:
- `.ai/` backend conventions do not apply — this diff is entirely `web/**` (a client page, a hook,
  MSW mocks, and tests) plus one handover documentation row.
- No superseded-architecture symbol (`IDispatcher`, `IBusMessageDispatcher`, MassTransit, MongoDB,
  etc.) appears anywhere in the diff.
- No secrets, no `float`/`double` money/timestamp handling in scope.
- `docs/handover/HO-CAT-002.md`'s new row was read in full against the actual shipped code: its
  description of the two rule changes, the ownership-check mechanism, the mock hook's
  `addInitScript` reasoning, and its own corrections to the previous row's overstatements (session
  queries still fire for an invalid id; `cat-responsive.spec.ts` needed no change; the row itself
  lives under `docs/`, not `web/**`) all match what the diff actually does. No drift found.

Static checks (`web/`), all clean against this diff:
- `tsc --noEmit`: no errors.
- `eslint . --max-warnings 0`: no errors or warnings.
- `prettier --check .`: only flags `next-env.d.ts`, an untouched, auto-generated file (absent from
  this diff's file list) — not a finding against this change.
- `vitest run`: 62 files / 257 tests, all passing (up from 255 at the first pass — the two new
  unit cases: non-owner customer, ownership-check 5xx).

No servers were started; e2e specs were read and statically reviewed, not executed.

SCOPE CHECK
- files in allowed scope: 7/7 (`web/**` per HO-CAT-002 §13, plus the HO-CAT-002 §1 Amended
  documentation row)
- files outside allowed scope: none

### Verdict

**PASS.** The redirect state machine correctly waits for the ownership check (including through its
one retry) before deciding a signed-in customer's destination, has no loop or double-`replace` path,
and the `useCat` signature change is additive and leaves all ten-plus other call sites unaffected
(verified directly against each, not only against the doc comment's claim). One LOW finding — the
ownership check inherits the global 30s `staleTime` rather than forcing a fresh fetch — is the same
defect shape as the earlier ACC-002 `IdleLock` bug this brief asked to check for, but not the same
severity: it is unreachable from an actual QR scan, self-heals through the existing sign-in/out
`queryClient.clear()` calls, and even in its narrowest reachable case only changes which screen a
visitor lands on, not what the API/destination guards will actually allow. Matches SECURITY's own
independent second-pass finding on the same line. Non-blocking per the owner's gate rule; goes to
`docs/backlog/CAT.md`. Tests are deterministic. All static checks pass.

---

GATE: CODE_REVIEW
HANDOVER: HO-CAT-002 (§1 Amended 2026-09-27, `/cats/{id}` smart-redirect page)
COMMITS: 8738d2e, 5b7447a, 9558066, 6027ee8 (diff 08e873e..6027ee8, branch ho/CAT-002-qr)
DATE: 2026-09-27
STATUS: PASS

FINDINGS
- [LOW] web/app/cats/[id]/page.tsx:60-68 — `useStaffSession()`/`useCustomerSession()` fire
  unconditionally, before `isValidCatId(id)` is checked, so an obviously-invalid id (e.g.
  `not-a-guid`) still issues two session network calls that are then discarded. Not a security
  issue (the session endpoints don't take the id and the result is provably never used, per the
  page's own doc comment and confirmed by reading the effect body), and hooks can't be called
  conditionally anyway — but the id-shape check could run before the hooks are invoked with no
  behavioural change (React allows computing `isValidCatId(id)` first; only the *use* of the hook
  results needs the guard, and this is already correctly deferred to `useEffect`). Optional:
  short-circuit is nice-to-have, not required.
- [LOW] web/app/cats/[id]/page.tsx:73,86 — `encodeURIComponent(id)` on a value already constrained
  to `/^[0-9a-fA-F]{8}-...{12}$/` by `isValidCatId` is a no-op (no character in that charset is
  ever percent-encoded). Harmless defence-in-depth, consistent with the file's own stated
  philosophy of not trusting a single check, but worth knowing it buys nothing today since the two
  checks aren't independent (the regex already forecloses everything encoding would catch).

No MEDIUM, HIGH or BLOCKER findings.

VERIFICATION

Redirect state machine (web/app/cats/[id]/page.tsx):
- Mirrors the already-reviewed `app/page.tsx` pattern line for line (`isStaffIdleTimeout`,
  `isStaffSignedIn`, `isCustomerSignedIn`, `isResolved`), rather than inventing a second
  convention for "who is signed in" — matches the handover's own stated intent.
- No double-redirect / loop: the effect's first branch (`if (!validId || !isResolved) return`)
  makes every redirect call idempotent against re-renders driven by the same resolved query data;
  none of the three destinations (`/staff/cats/{id}`, `/portal/cats/{id}`, `/sign-in?next=...`)
  route back through `/cats/{id}`, so no cycle is reachable.
- Cannot fire before both sessions resolve: `isResolved` requires both
  `!staffSession.isPending` (or a thrown `StaffSessionIdleTimeoutError`, which also signals
  "resolved, and it's staff") and `!customerSession.isPending`. `retryOnlyNetworkError` keeps
  `isPending` true through the retry window for a transport-level failure, so no premature
  redirect during retries; verified by the "loading" unit test (`neverResolves()` on both, no
  `router.replace` call).
- Stale cache: `staleTime` (10s staff / 30s customer) governs re-fetch-on-mount, not correctness —
  a stale-but-cached "signed in" is only reachable if the underlying session is actually still
  valid or a genuine sign-out failed to clear the query cache, which is an existing, previously
  accepted risk shared with `app/page.tsx` (sign-in flows call `queryClient.clear()`), not
  something this diff introduces or changes.
- Staff precedence when both sessions are present: checked first, unconditionally, with both a
  unit test and an e2e test (`signInAsStaff` + `signInAsCustomer` on the same page/browser
  context) asserting `/staff/cats/{id}` and NOT `/portal/cats/{id}`.
- Invalid id short-circuits correctly: `isValidCatId` is evaluated unconditionally (hooks rule
  compliance) but the effect returns before touching either session result, and the component
  renders `ErrorState` instead of ever reaching the loading/redirect branches — covered by unit
  and e2e tests that also assert `routerReplace` was never called.

Test quality:
- Unit tests (`cat-smart-redirect-page.test.tsx`) mock only the network edge
  (`getCustomerSession`/`getStaffSessionStatus`), leaving the real hooks, `sanitizeNextPath` and
  `isValidCatId` in the loop — each of the 8 cases (staff, staff idle-timeout, owner, staff-wins,
  anonymous, loading, session-check failure, invalid id) asserts either the exact `router.replace`
  argument (a real final URL string) or its absence via `waitFor`, not just "was called". `neverResolves()`
  gives deterministic control over the pending state instead of racing real timers.
- `cat-id.test.ts` covers case-insensitivity, malformed segments, path-traversal payloads and
  whitespace — appropriate for a value that gates both a redirect destination and (indirectly) a
  sign-in `next=` value.
- E2E (`cat-smart-redirect.spec.ts`) drives the real cookie-driven MSW sign-in flow rather than
  re-mocking, and every case chains `expect(page).toHaveURL(...)` (final URL) with a
  `getByRole('heading', ...)` assertion for the destination screen, not merely the URL — this
  satisfies "assert the intended screen actually rendered," including proving the sign-in round
  trip via `?next=` actually returns the visitor to the scanned cat.
- `cat-a11y.spec.ts` additions: each case navigates, asserts the final URL, only then runs
  `AxeBuilder`, so axe is checked against the destination screen, not the transient loading state.
- `cat-responsive.spec.ts`: `assertNoHorizontalScroll` was generalised to accept
  `{ path, expectFinalUrl }` and asserts `toHaveURL(expectFinalUrl)` before measuring
  `scrollWidth`/`clientWidth`, so the overflow measurement is against the real destination screen
  in both the staff and portal loops, not the redirect page's own brief loading skeleton.

Convention checks:
- `.ai/` backend conventions (CQRS, aggregates, DI, EF Core) don't apply to this diff — it is a
  pure Next.js client page plus a validation helper and tests; no backend layer, controller,
  handler or persistence code is touched.
- No `IDispatcher`, `IBusMessageDispatcher`, MassTransit, MongoDB or any other superseded-arch
  symbol appears anywhere in the diff.
- No secrets, hardcoded credentials, or `float`/`double` money/timestamp handling in scope.

Local checks (web/), all clean against this diff:
- `tsc --noEmit`: no errors.
- `eslint . --max-warnings 0`: no errors or warnings.
- `prettier --check .`: only flags `next-env.d.ts`, an untracked, auto-generated file
  (confirmed absent from `git ls-files` and not present at the base commit `08e873e`) — not part
  of this diff, not a finding against it.
- `vitest run`: 62 files / 255 tests, all passing, including the new
  `cat-smart-redirect-page.test.tsx` (8 tests) and `cat-id.test.ts` (8 tests).

No servers were started; e2e specs were read and statically reviewed, not executed (`--noEmit`/
`vitest`/`eslint`/`prettier` only, per instructions).

SCOPE CHECK
- files in allowed scope: 8/8 (web/** per HO-CAT-002 §13, plus the HO-CAT-002 §1 Amended
  documentation row)
- files outside allowed scope: none
