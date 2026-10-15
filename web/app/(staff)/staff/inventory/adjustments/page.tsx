'use client';

import { Suspense } from 'react';
import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { buttonVariants } from '@/components/ui/button';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { DataTable } from '@/components/common/DataTable';
import { LoadingState } from '@/components/common/LoadingState';
import { ApprovalQueue } from '@/components/inventory/ApprovalQueue';
import { FilterBar } from '@/components/inventory/FilterBar';
import { PageHeading } from '@/components/inventory/fields';
import { ADJUSTMENT_STATUS_LABELS, adjustmentReasonLabel } from '@/components/inventory/labels';
import { ApiForbiddenError } from '@/lib/api/client';
import { formatMoney } from '@/lib/format/money';
import { formatInZone } from '@/lib/format/time';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useAdjustments, useCounts } from '@/lib/hooks/useInventory';
import { useInventoryListState } from '@/lib/hooks/useInventoryListState';

const FIELDS = ['status'] as const;

/** `/staff/inventory/adjustments` — US-IMP-13 AC2: the approval queue (shared with counts) and the
 *  list of adjustments. */
function AdjustmentsContent() {
  const list = useInventoryListState(FIELDS);
  const query = useAdjustments(list.params);
  const pending = useAdjustments({ status: 'PendingApproval', pageSize: 50 });
  const awaitingCounts = useCounts({ status: 'AwaitingApproval', pageSize: 50 });
  const zone = useBranchTimeZone();
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;
  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="Adjustments"
        description="Every change to stock outside a receipt, transfer or count needs a reason and a note. Above the approval limit, a different person must approve."
        actions={
          <Link href="/staff/inventory/adjustments/new" className={buttonVariants({ variant: 'primary' })}>
            New adjustment
          </Link>
        }
      />
      <ApprovalQueue adjustments={pending.data?.items ?? []} counts={awaitingCounts.data?.items ?? []} timeZoneId={zone} />
      <FilterBar
        label="Filter adjustments"
        values={list.values}
        onChange={list.setValue}
        fields={[
          {
            name: 'status',
            label: 'Status',
            type: 'select',
            options: [{ value: '', label: 'All' }, ...Object.entries(ADJUSTMENT_STATUS_LABELS).map(([value, label]) => ({ value, label }))],
          },
        ]}
      />
      <DataTable
        caption="Stock adjustments"
        rows={query.data?.items}
        getRowId={(row) => row.id}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        pagination={{ totalCount: query.data?.totalCount ?? 0, pageSize: list.pageSize }}
        rowHref={(row) => `/staff/inventory/adjustments/${row.id}`}
        emptyState={{ title: 'No adjustments', description: 'Raise one when stock needs correcting.' }}
        columns={[
          { id: 'number', header: 'Adjustment', cell: (row) => row.number },
          { id: 'reason', header: 'Reason', cell: (row) => adjustmentReasonLabel(row.reason) },
          { id: 'location', header: 'Location', cell: (row) => row.locationCode ?? '—' },
          {
            id: 'status',
            header: 'Status',
            cell: (row) => (
              <Badge variant={row.status === 'PendingApproval' ? 'warning' : row.status === 'Posted' ? 'success' : 'neutral'}>
                {ADJUSTMENT_STATUS_LABELS[row.status]}
              </Badge>
            ),
          },
          { id: 'value', header: 'Value', cell: (row) => formatMoney(row.valueBase), cellClassName: 'tabular-nums' },
          { id: 'when', header: 'Requested', cell: (row) => formatInZone(row.requestedOn, zone) },
        ]}
      />
    </div>
  );
}

export default function AdjustmentsPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <AdjustmentsContent />
    </Suspense>
  );
}
