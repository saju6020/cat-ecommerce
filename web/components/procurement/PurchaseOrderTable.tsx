'use client';

import { DataTable } from '@/components/common/DataTable';
import { formatMoney } from '@/lib/format/money';
import { formatDateOnly } from '@/lib/format/time';
import type { PurchaseOrderSummary } from '@/lib/api/procurement-orders';
import { PurchaseOrderStatusBadge, ReceiptStateBadge } from './StatusBadges';

/** `components/procurement/PurchaseOrderTable.tsx` — HO-IMP-002 §11.2: number, supplier, status,
 *  receipt state, expected delivery, total. Status and receipt state are two badges (icon + word). */
export function PurchaseOrderTable({
  rows,
  totalCount,
  pageSize,
  isLoading,
  isError,
  onRetry,
}: {
  rows: PurchaseOrderSummary[] | undefined;
  totalCount: number;
  pageSize: number;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  return (
    <DataTable
      caption="Purchase orders"
      rows={rows}
      getRowId={(row) => row.id}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      pagination={{ totalCount, pageSize }}
      emptyState={{ title: 'No purchase orders match', description: 'Raise a new order, or clear the filters.' }}
      rowHref={(row) => `/staff/procurement/purchase-orders/${row.id}`}
      columns={[
        { id: 'number', header: 'Order', cell: (row) => row.number },
        { id: 'supplier', header: 'Supplier', cell: (row) => row.supplierName },
        { id: 'status', header: 'Status', cell: (row) => <PurchaseOrderStatusBadge status={row.status} /> },
        { id: 'receipt', header: 'Receipt', cell: (row) => <ReceiptStateBadge state={row.receiptState} /> },
        { id: 'expected', header: 'Expected', cell: (row) => formatDateOnly(row.expectedDeliveryOn) },
        { id: 'total', header: 'Total', cell: (row) => formatMoney(row.totalAmount), cellClassName: 'tabular-nums' },
      ]}
    />
  );
}
