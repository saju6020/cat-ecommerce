'use client';

import { Suspense } from 'react';
import { Badge } from '@/components/ui/badge';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { DataTable } from '@/components/common/DataTable';
import { LoadingState } from '@/components/common/LoadingState';
import { FilterBar } from '@/components/inventory/FilterBar';
import { PageHeading } from '@/components/inventory/fields';
import { ApiForbiddenError } from '@/lib/api/client';
import { formatQuantity } from '@/lib/format/inventory';
import { formatInZone } from '@/lib/format/time';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useUnitBySku } from '@/lib/hooks/useInventory';
import { useInventoryListState } from '@/lib/hooks/useInventoryListState';
import { useRecalls } from '@/lib/hooks/useInventoryRecalls';

const FIELDS = ['status'] as const;

/** `/staff/inventory/recalls` — US-IMP-11: active, closed and withdrawn recalls. A recall is declared
 *  from a batch's own page. */
function RecallsContent() {
  const list = useInventoryListState(FIELDS);
  const query = useRecalls(list.params);
  const zone = useBranchTimeZone();
  const units = useUnitBySku();
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;
  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="Recalls"
        description="A recall holds every remaining unit of a lot, at every location. To declare one, open the batch and choose Recall."
      />
      <FilterBar
        label="Filter recalls"
        values={list.values}
        onChange={list.setValue}
        fields={[
          {
            name: 'status',
            label: 'Status',
            type: 'select',
            options: [
              { value: '', label: 'All' },
              { value: 'Active', label: 'Active' },
              { value: 'Closed', label: 'Closed' },
              { value: 'Withdrawn', label: 'Withdrawn' },
            ],
          },
        ]}
      />
      <DataTable
        caption="Recalls"
        rows={query.data?.items}
        getRowId={(row) => row.batchId}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        pagination={{ totalCount: query.data?.totalCount ?? 0, pageSize: list.pageSize }}
        rowHref={(row) => `/staff/inventory/recalls/${row.batchId}`}
        emptyState={{ title: 'No recalls', description: 'Nothing has been recalled.' }}
        columns={[
          { id: 'number', header: 'Recall', cell: (row) => row.recallNumber },
          { id: 'lot', header: 'Lot', cell: (row) => row.lotNumber },
          { id: 'item', header: 'Item', cell: (row) => `${row.displayName} (${row.skuCode})` },
          { id: 'declared', header: 'Declared', cell: (row) => formatInZone(row.declaredOn, zone) },
          {
            id: 'status',
            header: 'Status',
            cell: (row) => <Badge variant={row.status === 'Active' ? 'danger' : 'neutral'}>{row.status}</Badge>,
          },
          { id: 'notified', header: 'Customers messaged', cell: (row) => `${row.notifiedRecipientCount} in ${row.notificationRuns} ${row.notificationRuns === 1 ? 'run' : 'runs'}` },
          {
            id: 'held',
            header: 'Still held',
            cell: (row) => (units[row.skuId] ? formatQuantity(row.heldQuantity, units[row.skuId]!) : '—'),
            cellClassName: 'tabular-nums',
          },
        ]}
      />
    </div>
  );
}

export default function RecallsPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <RecallsContent />
    </Suspense>
  );
}
