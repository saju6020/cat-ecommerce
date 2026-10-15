'use client';

import { Suspense } from 'react';
import Link from 'next/link';
import { buttonVariants } from '@/components/ui/button';
import { LoadingState } from '@/components/common/LoadingState';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { FilterBar } from '@/components/inventory/FilterBar';
import { PageHeading } from '@/components/inventory/fields';
import { PO_RECEIPT_STATE_LABELS, PO_STATUS_LABELS } from '@/components/inventory/labels';
import { PurchaseOrderTable } from '@/components/procurement/PurchaseOrderTable';
import { ApiForbiddenError } from '@/lib/api/client';
import { useInventoryListState } from '@/lib/hooks/useInventoryListState';
import { usePurchaseOrders, useSuppliers } from '@/lib/hooks/useProcurement';

const FIELDS = ['status', 'supplierId', 'receiptState', 'from', 'to'] as const;

/** `/staff/procurement/purchase-orders` — US-IMP-02. */
function PurchaseOrdersContent() {
  const list = useInventoryListState(FIELDS);
  const query = usePurchaseOrders(list.params);
  const suppliers = useSuppliers({ pageSize: 100 });

  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;

  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="Purchase orders"
        description="Orders raised at this branch. Outstanding quantities stay visible after a partial receipt."
        actions={
          <Link href="/staff/procurement/purchase-orders/new" className={buttonVariants({ variant: 'primary' })}>
            New purchase order
          </Link>
        }
      />
      <FilterBar
        label="Filter purchase orders"
        values={list.values}
        onChange={list.setValue}
        onCommit={list.flush}
        fields={[
          {
            name: 'status',
            label: 'Status',
            type: 'select',
            options: [{ value: '', label: 'All' }, ...Object.entries(PO_STATUS_LABELS).map(([value, label]) => ({ value, label }))],
          },
          {
            name: 'supplierId',
            label: 'Supplier',
            type: 'select',
            options: [
              { value: '', label: 'All suppliers' },
              ...(suppliers.data?.items ?? []).map((s) => ({ value: s.id, label: s.name })),
            ],
          },
          {
            name: 'receiptState',
            label: 'Receipt',
            type: 'select',
            options: [
              { value: '', label: 'All' },
              ...Object.entries(PO_RECEIPT_STATE_LABELS).map(([value, label]) => ({ value, label })),
            ],
          },
          { name: 'from', label: 'Expected from', type: 'date' },
          { name: 'to', label: 'Expected to', type: 'date' },
        ]}
      />
      <PurchaseOrderTable
        rows={query.data?.items}
        totalCount={query.data?.totalCount ?? 0}
        pageSize={list.pageSize}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
      />
    </div>
  );
}

export default function PurchaseOrdersPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <PurchaseOrdersContent />
    </Suspense>
  );
}
