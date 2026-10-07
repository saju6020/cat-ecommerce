import type { QueryClient } from '@tanstack/react-query';

/**
 * The active branch id for `X-Branch-Id` (HO-ADM-001 §8.3, ADR-046), applied by
 * `lib/api/client.ts` to every request — reads included, not only unsafe methods (a branch is a
 * read-scoping concern as much as a write one).
 *
 * HO-ADM-002 §8.3, ADR-FE-016: the active branch is TanStack Query state under the key
 * `['staff', 'active-branch']` — seeded from `GET /api/staff/branches/mine`'s primary branch
 * (`useMyBranches`, `lib/hooks/useActiveBranch.ts`) and written by `ActiveBranchPicker` via
 * `queryClient.setQueryData`. The query cache is the only source of truth; this file holds no
 * state of its own, only a reference to the one real store. **Not persisted anywhere** — a reload
 * always re-seeds from the primary branch.
 *
 * `apiFetch` calls `getActiveBranchId()` synchronously and cannot itself use `useQueryClient()`
 * (not a hook), so `registerQueryClient()` gives it a way to reach the app's one `QueryClient`
 * instance without going through React. `app/providers.tsx` calls it once, with the same instance
 * it passes to `QueryClientProvider`.
 *
 * ARCH gate HIGH fix (HO-ADM-002-ARCHITECTURE, owner decision 2026-09-28) — registration flows
 * `app/providers.tsx` → this module, not the reverse (this module importing a getter FROM
 * `app/providers.tsx`), because that is the correct dependency direction per HO-CAT-002 §8.2
 * (`app/` depends on `lib/`, never the reverse); it also keeps this file free of any import beyond
 * the type used for annotation. **`app/providers.tsx` calls `registerQueryClient()` from a
 * `useEffect`, never from its own `useState` lazy initialiser** — verified against a real `next
 * dev` build, not merely reasoned about: Next's dev build runs React Strict Mode
 * (`next.config.ts`), which deliberately calls a lazy `useState` initialiser twice on mount and
 * keeps only one call's result as the real state. Calling `registerQueryClient()` *inside* that
 * initialiser runs it for both calls, and the later call can register the discarded `QueryClient`,
 * permanently orphaning the one React actually renders with — `getActiveBranchId()` would then read
 * a client with nothing ever written to it (reproduced: `ActiveBranchPicker`'s own context-based
 * read kept working throughout, while `apiFetch` silently sent no `X-Branch-Id` at all). An effect
 * body only ever runs against the already-settled `queryClient` value, so it cannot register the
 * wrong one.
 *
 * Kept in its own module (rather than a local function inside client.ts) so it can be mocked in
 * isolation — `vi.mock('@/lib/branch', ...)` — without mocking `lib/api/client.ts` itself, whose
 * `apiFetch` a real request test still needs to exercise unmocked.
 */
let queryClientRef: QueryClient | undefined;
const ACTIVE_BRANCH_QUERY_KEY = ['staff', 'active-branch'] as const;

/** Called once, by `app/providers.tsx`, with the same `QueryClient` instance it renders
 *  `QueryClientProvider` with. Holds a reference to that one real store — not a second copy of the
 *  active-branch value — so `getActiveBranchId()` has something to call `getQueryData` through. */
export function registerQueryClient(client: QueryClient): void {
  queryClientRef = client;
}

export function getActiveBranchId(): string | null {
  return queryClientRef?.getQueryData<string | null>(ACTIVE_BRANCH_QUERY_KEY) ?? null;
}

/**
 * ADM backlog 12 — `queryClient.clear()` empties the whole cache, `['staff', 'active-branch']`
 * included, so a staff member whose session ended mid-work (idle-lock expiry, revoked session) and
 * who signs straight back in lost their branch.
 *
 * The branch is preserved ONLY for that same person: `clearQueryCacheKeepingBranchFor` clears the
 * cache completely (nothing stale is sent as `X-Branch-Id` meanwhile) and remembers
 * `{ userId, branchId }` outside the cache; `restorePreservedBranch` puts it back on the next
 * staff sign-in only if the signed-in user id matches. An explicit sign-out calls
 * `forgetPreservedBranch`, and a different person never inherits it.
 */
let preserved: { userId: string; branchId: string } | null = null;

export function clearQueryCacheKeepingBranchFor(
  client: QueryClient,
  userId: string | null | undefined,
): void {
  const active = client.getQueryData<string | null>(ACTIVE_BRANCH_QUERY_KEY);
  client.clear();
  preserved = userId && active ? { userId, branchId: active } : null;
}

export function restorePreservedBranch(client: QueryClient, userId: string): void {
  const kept = preserved;
  preserved = null;
  if (kept && kept.userId === userId) client.setQueryData(ACTIVE_BRANCH_QUERY_KEY, kept.branchId);
}

/** Explicit sign-out / person change: clear everything and remember nothing. */
export function forgetPreservedBranch(client: QueryClient): void {
  preserved = null;
  client.clear();
}
