'use client';

import { use } from 'react';
import {
  useAdminBranch,
  useBranchClosures,
  useCreateBranchClosure,
  useCancelBranchClosure,
} from '@/lib/hooks/useAdminBranches';
import { ClosureForm } from '@/components/branches/ClosureForm';
import { ClosureList } from '@/components/branches/ClosureList';
import { LoadingState } from '@/components/common/LoadingState';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { ApiForbiddenError } from '@/lib/api/client';

/** `/staff/admin/branches/[branchId]/closures` — Closures tab. */
export default function BranchClosuresPage({ params }: { params: Promise<{ branchId: string }> }) {
  const { branchId } = use(params);
  const { data: branch, isLoading: isBranchLoading } = useAdminBranch(branchId);
  const closures = useBranchClosures(branchId);
  const createMutation = useCreateBranchClosure(branchId);
  const cancelMutation = useCancelBranchClosure(branchId);

  if (closures.error instanceof ApiForbiddenError)
    return <AccessRefusedState error={closures.error} />;
  if (isBranchLoading || !branch) return <LoadingState rows={5} />;

  return (
    <div className="flex flex-col gap-6">
      <h2 className="font-display text-lg font-semibold text-ink">Closures</h2>
      <ClosureForm
        timeZoneId={branch.timeZoneId}
        isPending={createMutation.isPending}
        onCreate={(request) => createMutation.mutateAsync(request)}
      />
      <ClosureList
        timeZoneId={branch.timeZoneId}
        closures={closures.data}
        isLoading={closures.isLoading}
        isError={closures.isError}
        onRetry={() => closures.refetch()}
        onCancel={(closureId) => cancelMutation.mutateAsync(closureId)}
      />
    </div>
  );
}
