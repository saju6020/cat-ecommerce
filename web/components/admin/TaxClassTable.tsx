import { Badge } from '@/components/ui/badge';
import { DataTable } from '@/components/common/DataTable';
import type { TaxClass } from '@/lib/api/admin-catalogue';

/** `components/admin/TaxClassTable.tsx` — HO-ADM-002 §11.3, US-ADM-04. No `/new` route (§8.1's
 *  route map lists only `tax-classes/page.tsx` and `tax-classes/[taxClassId]/page.tsx`) — the
 *  create form is inline above this table on the list page, same pattern as `RoomForm`/`RoomList`. */
export interface TaxClassTableProps {
  taxClasses: TaxClass[] | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}

export function TaxClassTable({ taxClasses, isLoading, isError, onRetry }: TaxClassTableProps) {
  return (
    <DataTable
      caption="Tax classes"
      rows={taxClasses}
      getRowId={(row) => row.id}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      emptyState={{ title: 'No tax classes yet' }}
      rowHref={(row) => `/staff/admin/tax-classes/${row.id}`}
      columns={[
        { id: 'code', header: 'Code', cell: (row) => row.code },
        { id: 'name', header: 'Name', cell: (row) => row.name },
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
