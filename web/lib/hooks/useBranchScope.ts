import { useActiveBranchId, useMyBranches } from './useActiveBranch';

/**
 * `lib/hooks/useBranchScope.ts` — HO-APT-002 §8.3: the staff Appointments queries all include the
 * active branch in their key so a branch switch refetches them. `ready` is false until the active
 * branch is known — `GET /api/staff/branches/mine` seeds it (`useMyBranches`) — so no request is
 * sent without the `X-Branch-Id` it should carry. A staff member with nothing to seed (or whose
 * branch call failed) still becomes ready and gets the API's own answer.
 */
export function useBranchScope(): { branchId: string | null; ready: boolean } {
  const branchId = useActiveBranchId();
  const mine = useMyBranches();
  const seedable = mine.data ? (mine.data.activeBranchId ?? mine.data.primaryBranchId) : null;
  const ready = branchId !== null || (mine.isSuccess && seedable === null) || mine.isError;
  return { branchId, ready };
}
