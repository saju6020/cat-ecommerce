'use client';

import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { PendingDiagnosticsTable } from '@/components/visit/PendingDiagnosticsTable';
import { ApiForbiddenError } from '@/lib/api/client';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { usePendingDiagnostics } from '@/lib/hooks/useDiagnostics';

/** Container for `/staff/diagnostics` (HO-EMR-002 §11.9). The 30 s poll only swaps `data`. */
export function PendingDiagnosticsView() {
  const orders = usePendingDiagnostics();
  const zone = useBranchTimeZone();
  if (orders.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-2xl font-semibold text-ink">Pending diagnostics</h1>
      <p className="text-sm text-ink-muted">
        Tests ordered at this branch that are still waiting for results. The list refreshes every 30
        seconds.
      </p>
      <PendingDiagnosticsTable
        rows={orders.data}
        zone={zone}
        isLoading={orders.isLoading}
        isError={orders.isError}
        onRetry={() => void orders.refetch()}
      />
    </div>
  );
}
