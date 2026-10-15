import { useActiveBranchId, useMyBranches } from '@/lib/hooks/useActiveBranch';
import { useAdminBranches } from '@/lib/hooks/useAdminBranches';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useActiveBranchZone } from '@/lib/hooks/billing/useActiveBranchZone';

/**
 * `lib/hooks/useBranchOptions.ts` — HO-RPT-002 §11.3/§11.5. The caller's own branches as picker
 * options (names from the branch list, the bare id until it loads), plus the active branch's IANA
 * zone for labelling date fields before a payload names its own zone. Composes the HO-ADM-002
 * hooks; nothing new is fetched.
 */
export function useBranchOptions() {
  const mine = useMyBranches();
  const all = useAdminBranches();
  const activeBranchId = useActiveBranchId();
  const { zone } = useActiveBranchZone();
  const primaryZone = useBranchTimeZone();
  const options = (mine.data?.branchIds ?? []).map((id) => ({
    id,
    name: all.data?.find((branch) => branch.id === id)?.name ?? id,
  }));
  /** The IANA zone for a branch id; '' (all branches) uses the primary branch's zone. */
  const zoneFor = (branchId: string) =>
    all.data?.find((branch) => branch.id === branchId)?.timeZoneId ?? primaryZone;
  return { options, activeBranchId, zone, zoneFor, branches: all.data };
}
