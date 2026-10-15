'use client';

import { Badge } from '@/components/ui/badge';
import { DataTable } from '@/components/common/DataTable';
import { countryName } from '@/components/inventory/countries';
import type { SupplierSummary } from '@/lib/api/procurement-suppliers';

/** `components/procurement/SupplierTable.tsx` — HO-IMP-002 §11.2, US-IMP-01. A list through
 *  `DataTable` (URL-driven page); the status is a word, not just a colour. */
export function SupplierTable({
  rows,
  totalCount,
  pageSize,
  isLoading,
  isError,
  onRetry,
}: {
  rows: SupplierSummary[] | undefined;
  totalCount: number;
  pageSize: number;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  return (
    <DataTable
      caption="Suppliers"
      rows={rows}
      getRowId={(row) => row.id}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      pagination={{ totalCount, pageSize }}
      emptyState={{
        title: 'No suppliers match',
        description: 'Add a supplier, or clear the filters.',
      }}
      rowHref={(row) => `/staff/procurement/suppliers/${row.id}`}
      columns={[
        { id: 'name', header: 'Supplier', cell: (row) => `${row.name}` },
        { id: 'code', header: 'Code', cell: (row) => row.code },
        { id: 'country', header: 'Country', cell: (row) => countryName(row.countryCode) },
        { id: 'currency', header: 'Currency', cell: (row) => row.currencyCode },
        {
          id: 'lead',
          header: 'Lead time',
          cell: (row) => `${row.leadTimeDays} ${row.leadTimeDays === 1 ? 'day' : 'days'}`,
        },
        { id: 'terms', header: 'Payment terms', cell: (row) => row.paymentTerms },
        {
          id: 'status',
          header: 'Status',
          cell: (row) => (
            <Badge variant={row.isActive ? 'success' : 'neutral'}>
              {row.isActive ? 'Active' : 'Inactive'}
            </Badge>
          ),
        },
      ]}
    />
  );
}
