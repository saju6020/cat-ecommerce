## Second pass (delta)

GATE: SECURITY
HANDOVER: HO-CAT-002 (§1 Amended 2026-09-27, owner rule change on `/cats/{id}`: staff sign-in default + ownership gate)
COMMITS: e8a6d25, 01309bc, 9f94bee, b543474 (diff 1660019..b543474, branch ho/CAT-002-qr)
DATE: 2026-09-28
STATUS: PASS

FINDINGS
- [LOW] web/app/cats/[id]/page.tsx:92 — `isOwner = Boolean(ownershipQuery.data)` also counts a
  success that is already in the React Query cache under `['cat', id]` (staleTime 30s,
  `app/providers.tsx:29`). In TanStack Query v5, a cached success keeps `isPending` false, and the
  last good `data` survives a later 404 or 5xx refetch. So the page can redirect on data it already
  holds without waiting for the server's answer. This is not reachable from a QR scan, which is a
  full page load with an empty cache. Sign-in, sign-out and account deletion also call
  `queryClient.clear()`. The worst case is a client-side navigation on a shared device whose cache
  was filled by a different principal, and the only effect is a wrong destination: the
  `/portal/cats/{id}` page and the API still enforce authorization, so nothing new is exposed.
  Fix: count ownership only from a response fetched for this check, e.g.
  `ownershipQuery.isSuccess && ownershipQuery.isFetchedAfterMount && !ownershipQuery.isFetching`,
  or give the check its own `staleTime: 0` / `refetchOnMount: 'always'` and require `!isError`.
  Goes to docs/backlog/CAT.md under the owner rule and does not block.

VERIFIED
- No cat call for anonymous visitors or staff: `shouldCheckOwnership` (`page.tsx:88-89`) requires
  `validId && isSessionResolved && !isStaffSignedIn && isCustomerSignedIn`. `useCat` passes
  `enabled` straight through to `useQuery` (`lib/hooks/useCat.ts:24`). The default stays `true`,
  so existing call sites are unchanged. A staff idle timeout counts as staff, so it also makes no
  call. `isSessionResolved` waits for both session queries, so no call goes out while staff status
  is still pending. Unit tests cover no `getCat` call for invalid id, staff, idle-timeout, both
  sessions, and anonymous (`tests/unit/cat/cat-smart-redirect-page.test.tsx:115,139,151,208,220`).
- No existence oracle: the backend `RequireActionFilter` returns 404 for both a missing cat and
  someone else's cat, never 403 (`src/Api/CatEcommerce.Api/Authorization/RequireActionFilter.cs:84-86`),
  and it does so before the handler runs, so the work done is the same in both cases. On the
  client, 404 is not retried (`useCat.ts:25-28`) and 403, 404, 401, 5xx and network errors all go
  to the same `/staff/sign-in?next=/staff/cats/{id}`. Destination and timing do not differ between
  "missing" and "not yours". Only a 5xx takes longer, because of one retry, and that does not
  depend on existence. Anonymous visitors make no call at all. A signed-in customer learns nothing
  here that a direct `GET /api/cats/{id}` would not already tell them.
- `next` safety: it is built only from a GUID-validated id (`isValidCatId`), `encodeURIComponent`-ed,
  run through `sanitizeNextPath(..., '/staff/cats', ...)`, then URL-encoded into the query
  (`page.tsx:99-106`). No attacker-supplied `next` is read. The consumer re-sanitises it with
  `sanitizeNextPath(searchParams.get('next'), '/staff/', '/staff/cats')`
  (`app/(auth)/staff/sign-in/page.tsx:39`). That rejects `//` and matches the prefix only at a
  `/` boundary (`lib/validation/nextPath.ts`), so it cannot redirect off-site or outside `/staff/`.
- MSW hook confined to mocks: `__forceCatQueryNotOwned` appears only in
  `web/mocks/handlers/areas/cat-profiles.ts:566-575,863` and the two e2e specs (set through
  `page.addInitScript`). Nothing outside tests or mocks references it. `@/mocks` is imported in
  exactly one place, as a dynamic `import('@/mocks/browser')` guarded by
  `process.env.NODE_ENV === 'development' && NEXT_PUBLIC_USE_MOCKS === 'true'`
  (`app/providers.tsx:90-91`). Next inlines `NODE_ENV` at build time, so a production bundle does
  not contain it. The hook only turns a mock 200 into a 404, which lowers access, and it is one-shot.
- Fail closed: every non-2xx or network error leaves `data` undefined, so the visitor goes to
  staff sign-in. The effect waits for `isOwnershipResolved` before it decides (`page.tsx:117`), so
  no redirect fires while the check is still in flight. `/cats/{id}` sits outside the
  `(portal)`/`(staff)` layouts, so no global session-error handler is registered there, and a 401
  from the check is just another "not owner". Covered by unit tests: 404 → staff sign-in, 5xx →
  staff sign-in after a retry, session 5xx → staff sign-in. As noted in the first pass, the
  redirect only chooses a destination. Authorization stays with the destination guards and the API.

SCOPE CHECK
- files in allowed scope: 7/7 (web/** per §13, plus the HO-CAT-002 §1 Amended row)
- files outside allowed scope: none

---

## First pass

GATE: SECURITY
HANDOVER: HO-CAT-002 (§1 Amended 2026-09-27, `/cats/{id}` smart-redirect page)
COMMITS: 8738d2e, 5b7447a, 9558066, 6027ee8 (diff 08e873e..6027ee8, branch ho/CAT-002-qr)
DATE: 2026-09-27
STATUS: PASS

FINDINGS
- none (no BLOCKER, HIGH, MEDIUM or LOW)

VERIFIED
- Open redirect / injection via id: `web/app/cats/[id]/page.tsx:65,73,95` — `isValidCatId` (anchored
  GUID regex, `web/lib/validation/catId.ts:15-16`) gates every redirect; the effect returns before
  any `router.replace` when the id is invalid. All three targets are fixed relative paths with the id
  additionally `encodeURIComponent`-ed. `next` is a constant prefix + GUID, run through
  `sanitizeNextPath` and URL-encoded; the consumer (`app/(auth)/sign-in/page.tsx:46`) re-sanitises
  against `['/portal/', '/book']`. No attacker-controlled `next` is read by this page at all.
- XSS: the id is never rendered. The invalid-id `ErrorState` uses static strings only.
- Existence/data leak: the page imports only the session hooks, `sanitizeNextPath` and
  `isValidCatId`. No `/api/cats` call, no fetch. A real id, a missing id and a well-formed
  nonexistent id behave identically for anonymous users (sign-in) and customers
  (`/portal/cats/{id}`, which owns its own not-found/forbidden handling). The response of the
  page does not depend on whether the cat exists.
- Fail-closed: a session query error other than `StaffSessionIdleTimeoutError` leaves `data`
  undefined, so the user is treated as signed out and sent to sign-in. This is covered by a unit test
  with 500 on both endpoints. A staff idle timeout is routed to `/staff/cats/{id}`, where
  `StaffSessionGuard` enforces re-auth, so there is no privilege gain. The redirect only picks a
  destination. Authorization stays with the destination pages' guards and the API.
- Staff-wins precedence: staff is checked first (`page.tsx:75`), with unit and e2e coverage.
- Credentials in tests: the e2e spec uses `accountsFixtureIds.customerEmail/customerPassword` from
  the pre-existing MSW mock fixture (`web/mocks/handlers/areas/accounts.ts:45`). This is not new and
  not a real credential. The unit test uses only synthetic session objects.
- `web/.env.local`: exists locally and is ignored by `.gitignore:34` (`web/.env*.local`). It is not
  in `git ls-files` and appears in no commit on any branch. Its contents are only
  `NEXT_PUBLIC_API_BASE_URL` / `NEXT_PUBLIC_USE_MOCKS`, which are public build variables, not secrets.

SCOPE CHECK
- files in allowed scope: 8/8 (web/** per §13, plus the HO-CAT-002 §1 Amended row)
- files outside allowed scope: none
