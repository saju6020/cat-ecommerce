'use client';

import { Suspense } from 'react';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { DataTable } from '@/components/common/DataTable';
import { LoadingState } from '@/components/common/LoadingState';
import { FilterBar } from '@/components/inventory/FilterBar';
import { PageHeading } from '@/components/inventory/fields';
import { BATCH_STATUS_LABELS } from '@/components/inventory/labels';
import { BatchStateBadge } from '@/components/stock/BatchStateBadge';
import { ExpiryBadge } from '@/components/stock/ExpiryBadge';
import { ApiForbiddenError } from '@/lib/api/client';
import { formatQuantity } from '@/lib/format/inventory';
import { useBatches } from '@/lib/hooks/useInventory';
import { useInventoryListState } from '@/lib/hooks/useInventoryListState';

const FIELDS = ['search', 'status', 'expiringWithinDays'] as const;

/** `/staff/inventory/batches` — US-IMP-06/09: batches with their state and expiry in words. */
function BatchesContent() {
  const list = useInventoryListState(FIELDS, { debounced: ['search'] });
  const query = useBatches(list.params);
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;
  return (
    <div className="flex flex-col gap-4">
      <PageHeading title="Batches" description="Every lot held at this branch. An unusable batch says why." />
      <FilterBar
        label="Filter batches"
        values={list.values}
        onChange={list.setValue}
        onCommit={list.flush}
        fields={[
          { name: 'search', label: 'Search by lot or item', type: 'text' },
          {
            name: 'status',
            label: 'Status',
            type: 'select',
            options: [{ value: '', label: 'All' }, ...Object.entries(BATCH_STATUS_LABELS).map(([value, label]) => ({ value, label }))],
          },
          { name: 'expiringWithinDays', label: 'Expiring within (days)', type: 'number', min: 0, max: 3650 },
        ]}
      />
      <DataTable
        caption="Batches"
        rows={query.data?.items}
        getRowId={(row) => row.id}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        pagination={{ totalCount: query.data?.totalCount ?? 0, pageSize: list.pageSize }}
        rowHref={(row) => `/staff/inventory/batches/${row.id}`}
        emptyState={{ title: 'No batches match', description: 'Receive goods, or clear the filters.' }}
        columns={[
          { id: 'lot', header: 'Lot', cell: (row) => row.lotNumber },
          { id: 'item', header: 'Item', cell: (row) => `${row.displayName} (${row.skuCode})` },
          { id: 'expiry', header: 'Expiry', cell: (row) => <ExpiryBadge expiresOn={row.expiresOn} isExpired={row.isExpired} daysToExpiry={row.daysToExpiry} /> },
          { id: 'state', header: 'State', cell: (row) => <BatchStateBadge status={row.status} isExpired={row.isExpired} /> },
          { id: 'onHand', header: 'On hand', cell: (row) => formatQuantity(row.onHand, row.stockUnit), cellClassName: 'tabular-nums' },
          { id: 'received', header: 'Received', cell: (row) => formatQuantity(row.receivedQuantity, row.stockUnit), cellClassName: 'tabular-nums' },
        ]}
      />
    </div>
  );
}

export default function BatchesPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <BatchesContent />
    </Suspense>
  );
}
