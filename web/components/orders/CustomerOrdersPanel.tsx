'use client';

import Link from 'next/link';
import { useState } from 'react';
import { AmountCell } from '@/components/billing/Amount';
import { Section } from '@/components/billing/PageHeader';
import { type Column, ResponsiveTable } from '@/components/billing/ResponsiveTable';
import { buttonVariants } from '@/components/ui/button';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import type { StaffCustomerOrderRow } from '@/lib/api/staff-orders';
import { formatOrderWhen } from '@/lib/format/orders';
import { useStaffCustomerOrders } from '@/lib/hooks/useStaffOrders';
import { OrderStatusBadge } from './OrderStatusBadge';

/**
 * `components/orders/CustomerOrdersPanel.tsx` - HO-ORD-002 §11.14, mounted on HO-ACC-002's customer page.
 * The customer's orders at the active branch (`GET /api/staff/customers/{customerId}/orders`, HO-ORD-001-FU2;
 * matched by customer id, newest first, paged by the server) with a link to each, plus **New counter sale
 * for this customer** (`/staff/pos?customerId=...`). A `403` (a role without `Orders.View`, or no usable
 * branch) hides the list and keeps only the counter-sale action, whose own screen enforces its permission.
 */
const PAGE_SIZE = 5;

const columns: Array<Column<StaffCustomerOrderRow>> = [
  {
    id: 'number',
    header: 'Order',
    primary: true,
    cell: (o) => (
      <Link href={`/staff/orders/${o.id}`} className="font-semibold text-accent-ink underline">
        {o.orderNumber}
      </Link>
    ),
  },
  { id: 'placed', header: 'Placed', cell: (o) => formatOrderWhen(o.placedOn, o.branchTimeZoneId) },
  { id: 'status', header: 'Status', cell: (o) => <OrderStatusBadge status={o.status} /> },
  { id: 'total', header: 'Total', numeric: true, cell: (o) => <AmountCell money={o.grandTotal} /> },
];

export function CustomerOrdersPanel({ customerId }: { customerId: string }) {
  const [page, setPage] = useState(1);
  const query = useStaffCustomerOrders(customerId, page, PAGE_SIZE);
  const refused = query.error instanceof ApiForbiddenError;
  const notFound = query.error instanceof ApiNotFoundError;

  return (
    <Section
      title="Orders"
      description={refused ? undefined : "This customer's orders at the active branch, newest first."}
      actions={
        <Link href={`/staff/pos?customerId=${encodeURIComponent(customerId)}`} className={buttonVariants({ variant: 'secondary', size: 'sm' })}>
          New counter sale for this customer
        </Link>
      }
    >
      {refused ? (
        <p className="text-sm text-ink-muted">You cannot view this customer&rsquo;s order history right now. You can still start a counter sale for this customer.</p>
      ) : notFound ? (
        <p role="alert" className="text-sm text-ink">This customer was not found.</p>
      ) : (
        <>
          <ResponsiveTable
            caption="Customer orders"
            columns={columns}
            rows={query.data?.items}
            getRowId={(o) => o.id}
            isLoading={query.isPending}
            isError={query.isError}
            onRetry={() => void query.refetch()}
            emptyState={{ title: 'No orders yet.', description: 'Orders this customer places at this branch appear here.' }}
            pager={
              query.data
                ? { pageNumber: query.data.pageNumber, pageSize: query.data.pageSize, totalCount: query.data.totalCount, onPageChange: setPage }
                : undefined
            }
          />
          <p className="mt-3 text-sm">
            <Link href="/staff/orders" className="font-semibold text-accent-ink underline">
              See all in Orders
            </Link>
          </p>
        </>
      )}
    </Section>
  );
}
