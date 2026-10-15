'use client';

import { DataTable } from '@/components/common/DataTable';
import { countryName } from '@/components/inventory/countries';
import { formatDateOrDash } from '@/lib/format/inventory';
import type { ConsignmentSummary } from '@/lib/api/procurement-consignments';
import { ConsignmentStatusBadge } from './StatusBadges';

/** `components/procurement/ConsignmentTable.tsx` — HO-IMP-002 §11.2: reference, origin, shipment
 *  reference, status, expected arrival and how many purchase orders it covers. */
export function ConsignmentTable({
  rows,
  totalCount,
  pageSize,
  isLoading,
  isError,
  onRetry,
}: {
  rows: ConsignmentSummary[] | undefined;
  totalCount: number;
  pageSize: number;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  return (
    <DataTable
      caption="Import consignments"
      rows={rows}
      getRowId={(row) => row.id}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      pagination={{ totalCount, pageSize }}
      emptyState={{ title: 'No consignments match', description: 'Plan a consignment, or clear the filters.' }}
      rowHref={(row) => `/staff/procurement/consignments/${row.id}`}
      columns={[
        { id: 'reference', header: 'Consignment', cell: (row) => row.reference },
        { id: 'origin', header: 'Origin', cell: (row) => countryName(row.originCountryCode) },
        { id: 'shipment', header: 'Shipment', cell: (row) => row.shipmentReference },
        { id: 'status', header: 'Status', cell: (row) => <ConsignmentStatusBadge status={row.status} /> },
        { id: 'arrival', header: 'Expected arrival', cell: (row) => formatDateOrDash(row.expectedArrivalOn) },
        {
          id: 'pos',
          header: 'Purchase orders',
          cell: (row) => `${row.purchaseOrderCount} ${row.purchaseOrderCount === 1 ? 'order' : 'orders'}`,
        },
      ]}
    />
  );
}
