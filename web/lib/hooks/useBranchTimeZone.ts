import { useMyBranches } from './useActiveBranch';
import { useAdminBranches } from './useAdminBranches';

/** The fallback while branches load (or when the caller cannot read them): the clinic's own zone. */
export const DEFAULT_BRANCH_TIME_ZONE = 'Asia/Dhaka';

/**
 * HO-NTF-002 §11.5 — the IANA zone of the signed-in staff member's primary branch, used to show a
 * quiet-hours release time ("Held until 08:00") in BRANCH time, not the browser's (ADR-FE-015).
 * Same lookup the audit-log page does inline; read from the cached branch queries, no new request.
 */
export function useBranchTimeZone(): string {
  const myBranches = useMyBranches();
  const branches = useAdminBranches();
  return (
    branches.data?.find((branch) => branch.id === myBranches.data?.primaryBranchId)?.timeZoneId ??
    DEFAULT_BRANCH_TIME_ZONE
  );
}
