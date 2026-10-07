'use client';

import type { ReactNode } from 'react';
import { EmptyState } from '@/components/common/EmptyState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { useAdminBranches } from '@/lib/hooks/useAdminBranches';
import { useActiveBranchId, useMyBranches } from '@/lib/hooks/useActiveBranch';

/**
 * `components/appointments/ActiveBranchZoneGate.tsx` — HO-APT-002 W16. The staff calendar and the
 * front desk read "today" and every time on screen in the ACTIVE branch's zone. Falling back to
 * the first branch's zone (or to UTC) while the branch list loads, or when no branch is active,
 * shows another site's day. So the children — which fetch and format — only mount once the zone
 * is known; until then the gate shows a loading state, an error with retry, or a prompt to
 * choose a branch (the picker lives in the staff header).
 */
export function ActiveBranchZoneGate({ children }: { children: (zone: string) => ReactNode }) {
  const mine = useMyBranches();
  const branches = useAdminBranches();
  const activeBranchId = useActiveBranchId();

  if (branches.isError) {
    return (
      <ErrorState title="We couldn’t load the branches." onRetry={() => void branches.refetch()} />
    );
  }
  // The server's own default is about to be written into the active-branch cache (the seed effect
  // in `useMyBranches`); that is "loading", not "no branch".
  const seedPending =
    activeBranchId === null &&
    (mine.isLoading || !!(mine.data?.activeBranchId ?? mine.data?.primaryBranchId));
  if (branches.isLoading || seedPending) {
    return <LoadingState variant="card" rows={4} label="Loading the branch…" />;
  }

  const branch = branches.data?.find((b) => b.id === activeBranchId);
  if (!branch) {
    return (
      <EmptyState
        title="Choose a branch to continue."
        description="Pick the branch you are working at from the branch menu at the top of the page. Times here are shown in that branch’s time zone."
      />
    );
  }
  return <>{children(branch.timeZoneId)}</>;
}
