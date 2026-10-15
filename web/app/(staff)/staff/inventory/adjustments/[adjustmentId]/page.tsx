'use client';

import { use } from 'react';
import { AdjustmentDetail } from '@/components/inventory/AdjustmentDetail';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useAdjustment, useAdjustmentActions } from '@/lib/hooks/useInventory';

/** `/staff/inventory/adjustments/[adjustmentId]` — US-IMP-13 AC2, ADR-FE-048. */
export default function AdjustmentPage({ params }: { params: Promise<{ adjustmentId: string }> }) {
  const { adjustmentId } = use(params);
  const query = useAdjustment(adjustmentId);
  const actions = useAdjustmentActions(adjustmentId);
  const zone = useBranchTimeZone();
  return (
    <QueryBoundary query={query} notFoundTitle="This adjustment could not be found" notFoundDescription="It may belong to another branch.">
      {(adjustment) => (
        <AdjustmentDetail
          adjustment={adjustment}
          timeZoneId={zone}
          onApprove={(note) => actions.approve.mutateAsync(note)}
          onReject={(note) => actions.reject.mutateAsync(note)}
          onWithdraw={() => actions.withdraw.mutateAsync()}
        />
      )}
    </QueryBoundary>
  );
}
