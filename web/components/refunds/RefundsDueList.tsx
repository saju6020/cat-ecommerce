'use client';

import { useState } from 'react';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { AmountCell } from '@/components/billing/Amount';
import { PageHeader } from '@/components/billing/PageHeader';
import { type Column, ResponsiveTable } from '@/components/billing/ResponsiveTable';
import { StaffOrdersTabs } from '@/components/orders/StaffOrdersTabs';
import { Button } from '@/components/ui/button';
import { ApiForbiddenError } from '@/lib/api/client';
import { describeRefundReason } from '@/lib/api/errors';
import type { RefundDueOrder } from '@/lib/api/staff-refunds';
import { orderStatusLabel } from '@/lib/format/orders';
import { useRefundsDue } from '@/lib/hooks/useStaffReturns';
import { RefundDialog } from './RefundDialog';

/**
 * `components/refunds/RefundsDueList.tsx` — HO-ORD-002 §11.9, US-ORD-15. Orders where money is owed back
 * (a cancelled paid order) or where a return has been received and awaits its refund. Each row shows what
 * was paid, what was already refunded and the server's figure for what is due, and opens the refund
 * dialog (which fetches its own, fresh options). Refreshes every 30 s.
 */
const PAGE_SIZE = 20;

export function RefundsDueList() {
  const [page, setPage] = useState(1);
  const query = useRefundsDue({ pageNumber: page, pageSize: PAGE_SIZE });
  const [target, setTarget] = useState<RefundDueOrder | null>(null);
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;

  const columns: Array<Column<RefundDueOrder>> = [
    { id: 'order', header: 'Order', primary: true, sortValue: (o) => o.orderNumber, cell: (o) => o.orderNumber },
    { id: 'customer', header: 'Customer', sortValue: (o) => o.customerName, cell: (o) => o.customerName },
    { id: 'status', header: 'Status', cell: (o) => orderStatusLabel(o.status) },
    { id: 'why', header: 'Why', cell: (o) => o.reasons.map(describeRefundReason).join(', ') },
    { id: 'paid', header: 'Paid', numeric: true, cell: (o) => <AmountCell money={o.amountPaid} /> },
    { id: 'refunded', header: 'Refunded', numeric: true, cell: (o) => <AmountCell money={o.amountRefunded} /> },
    { id: 'due', header: 'Refund due', numeric: true, cell: (o) => <AmountCell money={o.refundDue} /> },
    {
      id: 'action',
      header: 'Action',
      cell: (o) => (
        <Button size="sm" onClick={() => setTarget(o)}>
          Refund<span className="sr-only"> {o.orderNumber}</span>
        </Button>
      ),
    },
  ];

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Refunds due" description="Orders where money is owed back to the customer. The list refreshes every 30 seconds." />
      <StaffOrdersTabs />
      <ResponsiveTable
        caption="Refunds due"
        columns={columns}
        rows={query.data?.items}
        getRowId={(o) => o.orderId}
        rowHref={(o) => `/staff/orders/${o.orderId}`}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        emptyState={{ title: 'No refunds are due.', description: 'Cancelled paid orders and received returns appear here.' }}
        pager={query.data ? { pageNumber: query.data.pageNumber, pageSize: query.data.pageSize, totalCount: query.data.totalCount, onPageChange: setPage } : undefined}
      />
      {target && (
        <RefundDialog
          orderId={target.orderId}
          orderNumber={target.orderNumber}
          open
          onOpenChange={(next) => !next && setTarget(null)}
        />
      )}
    </div>
  );
}
