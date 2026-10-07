'use client';

import { use } from 'react';
import { useStaffAccount, useStaffRoleCatalogue } from '@/lib/hooks/useStaffAdmin';
import { useChangeStaffRoles } from '@/lib/hooks/useStaffAdminMutations';
import { StaffAccountDetail } from '@/components/admin/StaffAccountDetail';
import { StaffBranchAssignmentPanel } from '@/components/branches/StaffBranchAssignmentPanel';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import {
  useAdminBranches,
  useStaffBranchAssignment,
  useAssignStaffBranches,
} from '@/lib/hooks/useAdminBranches';

// US-ACC-07.
// ARCH gate HIGH 3 (first pass) — this page wires
// useStaffRoleCatalogue()/useChangeStaffRoles(staffAccountId) (the route param, available before
// `account` itself loads, so both hooks can be called unconditionally ahead of the early
// returns below); StaffAccountDetail itself takes the results as props.
export default function StaffAccountDetailPage({
  params,
}: {
  params: Promise<{ staffAccountId: string }>;
}) {
  const { staffAccountId } = use(params);
  const { data: account, isLoading, isError, error, refetch } = useStaffAccount(staffAccountId);
  const catalogue = useStaffRoleCatalogue();
  const changeRolesMutation = useChangeStaffRoles(staffAccountId);
  const branches = useAdminBranches();
  const assignment = useStaffBranchAssignment(staffAccountId);
  const assignMutation = useAssignStaffBranches(staffAccountId);

  if (isLoading) return <LoadingState variant="card" rows={5} label="Loading staff account…" />;
  if (error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (error instanceof ApiNotFoundError) {
    return (
      <ErrorState
        title="Staff account not found"
        description="This account may have been removed."
      />
    );
  }
  // ARCHITECTURE gate third pass, HIGH — `isError` alone used to swap this whole page out for
  // ErrorState, discarding `StaffAccountDetail`'s unsaved `selectedRoleIds` draft, even when TanStack
  // Query still holds perfectly good, if stale, `account` data (its own documented behaviour on a
  // failed refetch — it keeps the last-known-good data, only flipping `isError` alongside it). The
  // realistic trigger this closes: `IdleLock` locks on a focus event, and a concurrent refetch of
  // THIS SAME query (also triggered by that focus event, independently of the session query) gets
  // its own `401` before `StaffLockProvider`'s query-refetch pause (`IdleLockContext.tsx`) has a
  // chance to cancel it. Only actually show the error screen when there is nothing else to show.
  if (isError && !account) {
    return (
      <ErrorState description="We couldn't load this account just now." onRetry={() => refetch()} />
    );
  }
  if (!account) return null;

  return (
    <div className="flex flex-col gap-8">
      <StaffAccountDetail
        account={account}
        catalogue={catalogue}
        onChangeRoles={(roleIds) => changeRolesMutation.mutateAsync(roleIds)}
        isPending={changeRolesMutation.isPending}
      />
      {/* HO-ADM-002 §11.2/§13 — the one scoped edit to this HO-ACC-002 page: adds the branch
          assignment panel below the existing roles editor. */}
      <StaffBranchAssignmentPanel
        branches={{ ...branches, refetch: () => void branches.refetch() }}
        assignment={{ ...assignment, refetch: () => void assignment.refetch() }}
        onSave={(request) => assignMutation.mutateAsync(request)}
        isPending={assignMutation.isPending}
      />
    </div>
  );
}
