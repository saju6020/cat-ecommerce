'use client';

import Link from 'next/link';
import { Amount } from '@/components/billing/Amount';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { cardClass } from '@/components/hearth/card';
import { GreetingHeader } from '@/components/hearth/GreetingHeader';
import { HearthEmpty } from '@/components/hearth/HearthEmpty';
import { pillSelectClass } from '@/components/hearth/fields';
import { Button, buttonVariants } from '@/components/ui/button';
import { ORDER_STATUSES } from '@/lib/api/my-orders';
import { fulfilmentLabel, orderStatusLabel } from '@/lib/format/orders';
import { useMyOrders } from '@/lib/hooks/useMyOrders';
import { useUrlParams } from '@/lib/hooks/useUrlParams';
import { cn } from '@/lib/utils';
import { OrderStatusBadge } from './OrderStatusBadge';

const PAGE_SIZE = 10;
/** "Fri 16 Oct" in the branch's zone (the approved design shows the day, not the time). */
function shortDate(instant: string, timeZone: string): string {
  const date = new Date(instant);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('en-GB', {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    timeZone,
  })
    .format(date)
    .replace(',', '');
}

/**
 * `components/orders/MyOrdersList.tsx` — HO-ORD-002 §11.6, FR-ORD-13, UI-4, in the Hearth layout
 * (docs/design/orders/MyOrders.dc.html), restyled to the owner's approved account design (2026-10-17,
 * https://claude.ai/artifact/K3WKkwpv67BqLJJdSr3bUh): one card per order — number and total, then the
 * day, item count and delivery method, and the status chip (text first, never colour alone); the whole
 * card opens the order. Filter by status and page — both in the URL. Loading, error (with retry) and empty states are real; the empty state links to the shop.
 *
 * HO-POR-001 integration note: the portal dashboard's re-order shortcut needs `canReorder` and
 * `trackingNumber` on the list item (`ho/POR-001`, not on master yet); when that lands, add the two
 * fields to `CustomerOrderSummary` and a row action here — nothing else changes.
 */
export function MyOrdersList() {
  const params = useUrlParams();
  const status = params.get('status');
  const pageNumber = Math.max(1, Number(params.get('page')) || 1);
  const query = useMyOrders({ status: status || undefined, pageNumber, pageSize: PAGE_SIZE });
  const rows = query.data?.items;

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <GreetingHeader title="My orders">Your shop orders, newest first.</GreetingHeader>
        <div className="flex w-full flex-col gap-1.5 sm:w-[260px]">
          <label htmlFor="orders-status" className="text-[13px] font-semibold text-ink">
            Show
          </label>
          <select
            id="orders-status"
            value={status}
            onChange={(event) => params.set({ status: event.target.value })}
            className={pillSelectClass}
          >
            <option value="">All orders</option>
            {ORDER_STATUSES.map((value) => (
              <option key={value} value={value}>
                {orderStatusLabel(value)}
              </option>
            ))}
          </select>
        </div>
      </div>

      {query.isPending ? (
        <LoadingState rows={5} label="Loading your orders…" />
      ) : query.isError && !rows ? (
        <ErrorState onRetry={() => void query.refetch()} />
      ) : !rows || rows.length === 0 ? (
        <HearthEmpty
          title={status ? 'No orders with this status.' : "You haven't placed an order yet."}
          description={
            status
              ? 'Try another status, or show all your orders.'
              : 'When you do, you can follow it here, with tracking and returns.'
          }
          action={
            status ? (
              <Button
                variant="pillSecondary"
                size="pillMd"
                onClick={() => params.set({ status: '' })}
              >
                Clear the filter
              </Button>
            ) : (
              <Link href="/shop" className={buttonVariants({ variant: 'pill', size: 'pillMd' })}>
                Browse the shop
              </Link>
            )
          }
        />
      ) : (
        <>
          <ul aria-label="Your orders" className="m-0 flex list-none flex-col gap-4 p-0">
            {rows.map((order) => (
              <li key={order.id}>
                {/* The approved design (owner, 2026-10-17): one card per order, the whole card opens it. */}
                <Link
                  href={`/portal/orders/${order.id}`}
                  aria-label={`View order ${order.orderNumber}`}
                  className={cn(
                    cardClass,
                    'grid grid-cols-[minmax(0,1fr)_auto] items-center gap-x-4 gap-y-2 px-4 py-3.5 text-ink no-underline transition-colors hover:border-accent sm:px-[26px] sm:py-[22px]',
                  )}
                >
                  <strong className="text-[15.5px] tracking-[-0.01em] sm:text-[19px]">
                    {order.orderNumber}
                  </strong>
                  <strong className="text-right text-base tabular-nums sm:text-[21px]">
                    <Amount money={order.grandTotal} />
                  </strong>
                  <span className="text-[13.5px] text-ink-muted sm:text-base">
                    {`${shortDate(order.placedOn, order.branchTimeZoneId)} · ${order.itemCount} ${order.itemCount === 1 ? 'item' : 'items'} · ${fulfilmentLabel(order.fulfilmentMethod)}`}
                  </span>
                  <span className="justify-self-end">
                    <OrderStatusBadge status={order.status} />
                  </span>
                </Link>
              </li>
            ))}
          </ul>
          {query.data && Math.ceil(query.data.totalCount / query.data.pageSize) > 1 && (
            <nav
              aria-label="Pages"
              className="flex flex-wrap items-center justify-between gap-3 text-[13px] text-ink-muted"
            >
              {(() => {
                const { pageSize, totalCount } = query.data;
                const pageCount = Math.max(1, Math.ceil(totalCount / pageSize));
                const current = query.data.pageNumber;
                const first = (current - 1) * pageSize + 1;
                const last = Math.min(totalCount, first + rows.length - 1);
                return (
                  <>
                    <span>{`Showing ${first}–${last} of ${totalCount}`}</span>
                    <div className="flex flex-wrap items-center gap-3">
                      <Button
                        variant="pillSecondary"
                        size="pillMd"
                        disabled={current <= 1}
                        onClick={() => params.set({ page: String(current - 1) })}
                      >
                        ← Previous
                      </Button>
                      <span>{`Page ${current} of ${pageCount}`}</span>
                      <Button
                        variant="pillSecondary"
                        size="pillMd"
                        disabled={current >= pageCount}
                        onClick={() => params.set({ page: String(current + 1) })}
                      >
                        Next →
                      </Button>
                    </div>
                  </>
                );
              })()}
            </nav>
          )}
        </>
      )}
    </div>
  );
}
