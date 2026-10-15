'use client';

import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { SelectField } from '@/components/billing/Fields';
import { PageHeader } from '@/components/billing/PageHeader';
import { type Column, ResponsiveTable } from '@/components/billing/ResponsiveTable';
import { Badge } from '@/components/ui/badge';
import { ApiForbiddenError } from '@/lib/api/client';
import type { StaffReturnRow } from '@/lib/api/staff-returns';
import { formatOrderWhen, returnKindLabel, returnNextStep, returnStatusLabel } from '@/lib/format/orders';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useStaffReturns } from '@/lib/hooks/useStaffReturns';
import { useUrlFilterState } from '@/lib/hooks/useUrlFilterState';

/**
 * `components/returns/ReturnsList.tsx` — HO-ORD-002 §11.10, US-ORD-13/14, UI-4. Returns at the active
 * branch, filtered by status (URL-backed), paged by the server and refreshed every 30 s. Status is text
 * first; the last column says what staff do next.
 */
const FIELDS = ['status', 'page'] as const;
const STATUSES = ['Requested', 'Approved', 'Received', 'Rejected', 'Closed'];
const PAGE_SIZE = 20;

export function ReturnsList() {
  const { values, setValue } = useUrlFilterState(FIELDS);
  const zone = useBranchTimeZone();
  const query = useStaffReturns({ status: values.status || undefined, pageNumber: Number(values.page) || 1, pageSize: PAGE_SIZE });
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;

  const columns: Array<Column<StaffReturnRow>> = [
    { id: 'order', header: 'Order', primary: true, sortValue: (r) => r.orderNumber, cell: (r) => r.orderNumber },
    { id: 'customer', header: 'Customer', sortValue: (r) => r.customerName, cell: (r) => r.customerName },
    { id: 'kind', header: 'Kind', cell: (r) => returnKindLabel(r.kind) },
    { id: 'status', header: 'Status', cell: (r) => <Badge variant={r.status === 'Requested' ? 'warning' : 'neutral'}>{returnStatusLabel(r.status)}</Badge> },
    { id: 'requested', header: 'Requested', sortValue: (r) => r.requestedOn, cell: (r) => formatOrderWhen(r.requestedOn, zone) },
    { id: 'items', header: 'Items', numeric: true, cell: (r) => `${r.quantity} in ${r.lineCount} line${r.lineCount === 1 ? '' : 's'}` },
    { id: 'next', header: 'Next step', cell: (r) => returnNextStep(r.status) },
  ];

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Returns" description="Customer returns and parcels that came back. The list refreshes every 30 seconds." />
      <div className="max-w-xs">
        <SelectField id="returns-status" label="Status" value={values.status} onChange={(e) => { setValue('status', e.target.value); setValue('page', ''); }}>
          <option value="">Any status</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>{returnStatusLabel(s)}</option>
          ))}
        </SelectField>
      </div>
      <ResponsiveTable
        caption="Returns"
        columns={columns}
        rows={query.data?.items}
        getRowId={(r) => r.id}
        rowHref={(r) => `/staff/returns/${r.id}`}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        emptyState={{ title: 'No returns.', description: values.status ? 'Change or clear the status filter.' : 'Return requests appear here when customers send them.' }}
        pager={query.data ? { pageNumber: query.data.pageNumber, pageSize: query.data.pageSize, totalCount: query.data.totalCount, onPageChange: (n) => setValue('page', String(n)) } : undefined}
      />
    </div>
  );
}
