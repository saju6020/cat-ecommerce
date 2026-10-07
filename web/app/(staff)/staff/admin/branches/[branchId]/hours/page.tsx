'use client';

import { use } from 'react';
import { useAdminBranch, useSetBranchWeeklyHours } from '@/lib/hooks/useAdminBranches';
import { WeeklyHoursEditor } from '@/components/branches/WeeklyHoursEditor';
import { LoadingState } from '@/components/common/LoadingState';

/** `/staff/admin/branches/[branchId]/hours` — Opening hours tab. */
export default function BranchHoursPage({ params }: { params: Promise<{ branchId: string }> }) {
  const { branchId } = use(params);
  const { data: branch, isLoading } = useAdminBranch(branchId);
  const mutation = useSetBranchWeeklyHours(branchId);

  if (isLoading || !branch) return <LoadingState rows={7} />;

  return (
    <div className="flex flex-col gap-4">
      <h2 className="font-display text-lg font-semibold text-ink">Opening hours</h2>
      <WeeklyHoursEditor
        timeZoneId={branch.timeZoneId}
        weeklyHours={branch.weeklyHours}
        isPending={mutation.isPending}
        onSave={(intervals) => mutation.mutateAsync(intervals)}
      />
    </div>
  );
}
