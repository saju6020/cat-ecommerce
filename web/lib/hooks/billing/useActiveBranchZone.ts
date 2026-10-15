import { DEFAULT_BRANCH_TIME_ZONE } from '@/lib/hooks/useBranchTimeZone';
import { useActiveBranchId, useMyBranches } from '@/lib/hooks/useActiveBranch';
import { useAdminBranches } from '@/lib/hooks/useAdminBranches';

/**
 * `lib/hooks/billing/useActiveBranchZone.ts` — the IANA zone of the ACTIVE branch (not the primary
 * one that `useBranchTimeZone` returns), so "today" on a form or a cash-up is the branch's day
 * (HO-BIL-002 §11 preamble). `ready` is false until the branch list and the active branch are
 * known; until then `zone` is the platform default and callers should not submit a date.
 */
export function useActiveBranchZone(): { zone: string; ready: boolean } {
  const mine = useMyBranches();
  const branches = useAdminBranches();
  const activeId = useActiveBranchId();
  const branch = branches.data?.find((b) => b.id === activeId);
  const ready = !!branch || branches.isError || (mine.isSuccess && !mine.data.primaryBranchId && !activeId);
  return { zone: branch?.timeZoneId ?? DEFAULT_BRANCH_TIME_ZONE, ready };
}
