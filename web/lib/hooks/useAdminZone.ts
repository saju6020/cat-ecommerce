'use client';

import { useMyBranches } from '@/lib/hooks/useActiveBranch';
import { useAdminBranches } from '@/lib/hooks/useAdminBranches';

/** The IANA zone of the person's primary branch, for admin screens whose times are shown in
 *  branch time (ADR-FE-014); `Asia/Dhaka` until the branches have loaded. Same rule the audit log
 *  screen uses. */
export function useAdminZone(): string {
  const mine = useMyBranches();
  const branches = useAdminBranches();
  return (
    branches.data?.find((b) => b.id === mine.data?.primaryBranchId)?.timeZoneId ?? 'Asia/Dhaka'
  );
}
