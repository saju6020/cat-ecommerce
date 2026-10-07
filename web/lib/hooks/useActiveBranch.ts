import { useEffect } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { getMyBranches, type MyBranches } from '@/lib/api/admin-branches';
import { ApiForbiddenError } from '@/lib/api/client';

/**
 * `lib/hooks/useActiveBranch.ts` — HO-ADM-002 §8.3, ADR-FE-016.
 *
 * The active branch id lives entirely in the TanStack Query cache, under `ACTIVE_BRANCH_KEY`
 * (`['staff', 'active-branch']`) — there is no store of our own alongside it. `useMyBranches()`
 * fetches `GET /api/staff/branches/mine` (every staff role, §12.3) and, the first time it resolves
 * with no active branch already cached, seeds that key from the server's own
 * `activeBranchId`/`primaryBranchId` via `queryClient.setQueryData` — never from a client-side
 * guess. `useActiveBranchId()` is the read side, a plain `useQuery` against the same key with
 * fetching disabled, so a component re-renders whenever `ActiveBranchPicker` (or this seed effect)
 * writes to it — the same reactivity every other cached resource in this app gets, nothing bespoke.
 *
 * `useSetActiveBranch()` is what `ActiveBranchPicker` calls on a selection: it writes the cache
 * value AND resets every branch-scoped query (§8.3, "changing it resets branch-scoped queries") by
 * invalidating the shared `['admin', ...]` query-key prefix every admin resource hook in this area
 * uses — one convention, one place that knows it, rather than each resource re-registering itself.
 *
 * `lib/branch.ts`'s `getActiveBranchId()` reads this same cache entry directly (via the app's one
 * `QueryClient` instance, `app/providers.tsx`) so that `apiFetch`, which cannot use
 * `useQueryClient()`, sends it as `X-Branch-Id` on every request.
 */
export const ACTIVE_BRANCH_KEY = ['staff', 'active-branch'] as const;

export function useMyBranches() {
  const queryClient = useQueryClient();
  const query = useQuery<MyBranches>({
    queryKey: ['staff', 'branches', 'mine'],
    queryFn: getMyBranches,
    staleTime: 60_000,
    retry: (failureCount, error) => !(error instanceof ApiForbiddenError) && failureCount < 1,
  });

  useEffect(() => {
    if (!query.data) return;
    // already seeded or user-picked this session — unless it is a branch this person is not
    // assigned to (kept across a sign-out/sign-in by clearQueryCachePreservingBranch)
    const cached = queryClient.getQueryData<string | null>(ACTIVE_BRANCH_KEY);
    if (cached != null && query.data.branchIds.includes(cached)) return;
    const seed = query.data.activeBranchId ?? query.data.primaryBranchId;
    if (seed) queryClient.setQueryData(ACTIVE_BRANCH_KEY, seed);
  }, [query.data, queryClient]);

  return query;
}

export function useActiveBranchId(): string | null {
  const { data } = useQuery<string | null>({
    queryKey: ACTIVE_BRANCH_KEY,
    queryFn: () => null,
    initialData: null,
    enabled: false, // written only via setQueryData (the seed effect above, ActiveBranchPicker)
  });
  return data ?? null;
}

export function useSetActiveBranch() {
  const queryClient = useQueryClient();
  return (branchId: string) => {
    queryClient.setQueryData(ACTIVE_BRANCH_KEY, branchId);
    void queryClient.invalidateQueries({ queryKey: ['admin'] });
  };
}
