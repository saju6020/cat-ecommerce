'use client';

import { DataTable } from '@/components/common/DataTable';
import { StorageChip } from '@/components/inventory/StorageChip';
import { formatDateOrDash, formatQuantity } from '@/lib/format/inventory';
import type { StockSummary } from '@/lib/api/inventory-stock';

/**
 * `components/stock/StockTable.tsx` — HO-IMP-002 §11.4, US-IMP-06/08. One row per item: on hand,
 * reserved, USABLE, SELLABLE, held, expired and the next expiry — each the server's figure, each with
 * the item's unit (UI-8). Filters (text, location, class, below reorder point) live in the URL above
 * this table; a row opens the item's batch × location matrix.
 */
export function StockTable({
  rows,
  totalCount,
  pageSize,
  isLoading,
  isError,
  onRetry,
}: {
  rows: StockSummary[] | undefined;
  totalCount: number;
  pageSize: number;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  return (
    <DataTable
      caption="Stock by item"
      rows={rows}
      getRowId={(row) => row.skuId}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      pagination={{ totalCount, pageSize }}
      rowHref={(row) => `/staff/inventory/stock/${row.skuId}`}
      emptyState={{ title: 'No stock matches', description: 'Receive goods, or clear the filters.' }}
      columns={[
        {
          id: 'item',
          header: 'Item',
          cell: (row) => (
            <span>
              {row.displayName} <span className="font-normal text-ink-muted">— {row.skuCode}</span>
            </span>
          ),
        },
        { id: 'storage', header: 'Storage', cell: (row) => <StorageChip storage={row.storage} /> },
        { id: 'onHand', header: 'On hand', cell: (row) => formatQuantity(row.onHand, row.stockUnit), cellClassName: 'tabular-nums' },
        { id: 'reserved', header: 'Reserved', cell: (row) => formatQuantity(row.reserved, row.stockUnit), cellClassName: 'tabular-nums' },
        { id: 'usable', header: 'Usable', cell: (row) => formatQuantity(row.usable, row.stockUnit), cellClassName: 'tabular-nums' },
        {
          id: 'sellable',
          header: 'Sellable',
          cell: (row) => <strong>{formatQuantity(row.sellable, row.stockUnit)}</strong>,
          cellClassName: 'tabular-nums',
        },
        { id: 'held', header: 'Held', cell: (row) => formatQuantity(row.heldQuantity, row.stockUnit), cellClassName: 'tabular-nums' },
        { id: 'expired', header: 'Expired', cell: (row) => formatQuantity(row.expiredQuantity, row.stockUnit), cellClassName: 'tabular-nums' },
        { id: 'next', header: 'Next expiry', cell: (row) => formatDateOrDash(row.nextExpiresOn, 'No expiry date') },
      ]}
    />
  );
}
