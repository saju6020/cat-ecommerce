'use client';

import Link from 'next/link';
import { Amount } from '@/components/billing/Amount';
import { PageHeader } from '@/components/billing/PageHeader';
import { ResponsiveTable, type Column } from '@/components/billing/ResponsiveTable';
import { EmptyCatIllustration } from '@/components/common/EmptyState';
import { buttonVariants } from '@/components/ui/button';
import { ORDER_STATUSES, type CustomerOrderSummary } from '@/lib/api/my-orders';
import { formatOrderWhen, orderStatusLabel, paymentMethodLabel } from '@/lib/format/orders';
import { useMyOrders } from '@/lib/hooks/useMyOrders';
import { useUrlParams } from '@/lib/hooks/useUrlParams';
import { OrderStatusBadge } from './OrderStatusBadge';

const PAGE_SIZE = 10;

/**
 * `components/orders/MyOrdersList.tsx` — HO-ORD-002 §11.6, FR-ORD-13, UI-4. The customer's orders:
 * order number, date in the branch zone, status as text + chip, item count, total (the server's) and
 * payment method. Filter by status and page — both in the URL. The empty state links to the shop.
 *
 * HO-POR-001 integration note: the portal dashboard's re-order shortcut needs `canReorder` and
 * `trackingNumber` on the list item (`ho/POR-001`, not on master yet); when that lands, add the two
 * fields to `CustomerOrderSummary` and a column/row action here — nothing else changes.
 */
export function MyOrdersList() {
  const params = useUrlParams();
  const status = params.get('status');
  const pageNumber = Math.max(1, Number(params.get('page')) || 1);
  const query = useMyOrders({ status: status || undefined, pageNumber, pageSize: PAGE_SIZE });

  const columns: Array<Column<CustomerOrderSummary>> = [
    {
      id: 'number',
      header: 'Order',
      primary: true,
      cell: (order) => <span>{order.orderNumber}</span>,
    },
    {
      id: 'placed',
      header: 'Placed',
      cell: (order) => formatOrderWhen(order.placedOn, order.branchTimeZoneId),
    },
    { id: 'status', header: 'Status', cell: (order) => <OrderStatusBadge status={order.status} /> },
    { id: 'items', header: 'Items', numeric: true, cell: (order) => order.itemCount },
    {
      id: 'total',
      header: 'Total',
      numeric: true,
      cell: (order) => <Amount money={order.grandTotal} />,
    },
    { id: 'payment', header: 'Payment', cell: (order) => paymentMethodLabel(order.paymentMethod) },
  ];

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="My orders" description="Track, cancel or return an order." />
      <div className="flex flex-col gap-1.5 sm:max-w-xs">
        <label htmlFor="orders-status" className="text-sm font-semibold text-ink">
          Show
        </label>
        <select
          id="orders-status"
          value={status}
          onChange={(event) => params.set({ status: event.target.value })}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        >
          <option value="">All orders</option>
          {ORDER_STATUSES.map((value) => (
            <option key={value} value={value}>
              {orderStatusLabel(value)}
            </option>
          ))}
        </select>
      </div>
      <ResponsiveTable
        caption="Your orders"
        columns={columns}
        rows={query.data?.items}
        getRowId={(order) => order.id}
        rowHref={(order) => `/portal/orders/${order.id}`}
        isLoading={query.isPending}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        emptyState={{
          icon: <EmptyCatIllustration />,
          title: status ? 'No orders with this status.' : "You haven't placed an order yet.",
          description: status
            ? 'Try another status, or show all your orders.'
            : 'When you do, you can follow it here.',
          action: (
            <Link href="/shop" className={buttonVariants({ variant: 'primary' })}>
              Browse the shop
            </Link>
          ),
        }}
        pager={
          query.data
            ? {
                pageNumber: query.data.pageNumber,
                pageSize: query.data.pageSize,
                totalCount: query.data.totalCount,
                onPageChange: (next) => params.set({ page: String(next) }),
              }
            : undefined
        }
      />
    </div>
  );
}
