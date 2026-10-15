'use client';

import { Suspense } from 'react';
import Link from 'next/link';
import { buttonVariants } from '@/components/ui/button';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { DataTable } from '@/components/common/DataTable';
import { LoadingState } from '@/components/common/LoadingState';
import { FilterBar } from '@/components/inventory/FilterBar';
import { PageHeading } from '@/components/inventory/fields';
import { ApiForbiddenError } from '@/lib/api/client';
import { formatInZone } from '@/lib/format/time';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useGoodsReceipts } from '@/lib/hooks/useInventory';
import { useInventoryListState } from '@/lib/hooks/useInventoryListState';

const FIELDS = ['from', 'to'] as const;

/** `/staff/inventory/receipts` — US-IMP-04: posted goods receipts. */
function ReceiptsContent() {
  const list = useInventoryListState(FIELDS);
  const query = useGoodsReceipts(list.params);
  const zone = useBranchTimeZone();
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;
  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="Goods receipts"
        description="Every delivery received at this branch. A posted receipt is permanent."
        actions={
          <Link href="/staff/inventory/receipts/new" className={buttonVariants({ variant: 'primary' })}>
            Receive goods
          </Link>
        }
      />
      <FilterBar
        label="Filter receipts"
        values={list.values}
        onChange={list.setValue}
        fields={[
          { name: 'from', label: 'Received from', type: 'date' },
          { name: 'to', label: 'Received to', type: 'date' },
        ]}
      />
      <DataTable
        caption="Goods receipts"
        rows={query.data?.items}
        getRowId={(row) => row.id}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        pagination={{ totalCount: query.data?.totalCount ?? 0, pageSize: list.pageSize }}
        rowHref={(row) => `/staff/inventory/receipts/${row.id}`}
        emptyState={{ title: 'No receipts match', description: 'Receive goods to see them here.' }}
        columns={[
          { id: 'number', header: 'Receipt', cell: (row) => row.number },
          { id: 'received', header: 'Received', cell: (row) => formatInZone(row.receivedOn, zone) },
          { id: 'supplier', header: 'Supplier', cell: (row) => row.supplierName ?? '—' },
          { id: 'po', header: 'Purchase order', cell: (row) => row.purchaseOrderNumber ?? 'Direct' },
          { id: 'ref', header: 'Delivery ref', cell: (row) => row.deliveryReference ?? '—' },
          { id: 'lines', header: 'Lines', cell: (row) => `${row.lineCount} ${row.lineCount === 1 ? 'line' : 'lines'}` },
          { id: 'batches', header: 'Batches', cell: (row) => `${row.batchCount} ${row.batchCount === 1 ? 'batch' : 'batches'}` },
        ]}
      />
    </div>
  );
}

export default function ReceiptsPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <ReceiptsContent />
    </Suspense>
  );
}
