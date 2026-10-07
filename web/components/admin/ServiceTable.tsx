import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { buttonVariants } from '@/components/ui/button';
import { DataTable } from '@/components/common/DataTable';
import { formatMoney } from '@/lib/format/money';
import type { ClinicService } from '@/lib/api/admin-catalogue';

/** `components/admin/ServiceTable.tsx` — HO-ADM-002 §11.3, US-ADM-04. U-C3: an unpriced service
 *  shows a "Not priced" badge with explanatory text — such a service cannot be booked. */
export interface ServiceTableProps {
  services: ClinicService[] | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  canManage: boolean;
}

export function ServiceTable({
  services,
  isLoading,
  isError,
  onRetry,
  canManage,
}: ServiceTableProps) {
  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="font-display text-xl font-semibold text-ink">Services</h1>
        {canManage && (
          <Link href="/staff/admin/services/new" className={buttonVariants({ variant: 'primary' })}>
            New service
          </Link>
        )}
      </div>

      <DataTable
        caption="Services"
        rows={services}
        getRowId={(row) => row.id}
        isLoading={isLoading}
        isError={isError}
        onRetry={onRetry}
        emptyState={{ title: 'No services yet' }}
        rowHref={(row) => `/staff/admin/services/${row.id}`}
        columns={[
          { id: 'code', header: 'Code', cell: (row) => row.code },
          { id: 'name', header: 'Name', cell: (row) => row.name },
          { id: 'category', header: 'Category', cell: (row) => row.category },
          { id: 'duration', header: 'Duration', cell: (row) => `${row.durationMinutes} min` },
          {
            id: 'price',
            header: 'Current price',
            cell: (row) =>
              row.isPriced && row.currentPrice ? (
                formatMoney(row.currentPrice)
              ) : (
                <Badge variant="warning">Not priced</Badge>
              ),
          },
          {
            id: 'online',
            header: 'Online bookable',
            cell: (row) => (row.isBookableOnline ? 'Yes' : 'No'),
          },
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
    </div>
  );
}
