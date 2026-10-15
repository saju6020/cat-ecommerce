import Link from 'next/link';
import { buttonVariants } from '@/components/ui/button';
import { ReorderButton } from '@/components/portal/reorder/ReorderButton';
import type { PortalOrders } from '@/lib/api/portal';
import { formatDateOnly, localDateOf } from '@/lib/format/time';
import { formatMoney } from '@/lib/format/money';
import { SHOP, portalOrderHref } from '@/lib/routing/portal-links';
import { orderStatusLabel } from './labels';
import { StatusBadge } from './StatusBadge';
import { TileEmpty, TileRow } from './DashboardSection';

/** The shop's own zone: an order's day is the day in Dhaka, not in the viewer's browser zone (the
 *  dashboard carries no zone for orders; same helpers as the appointments tile). */
const COMMERCE_TIME_ZONE = 'Asia/Dhaka';

/** Re-order is shown from the order item's own `canReorder` (Orders' rule) and goes through
 *  Orders' endpoint (ADR-FE-123). `trackingNumber` is shown as the tracking reference when present. */
export function OrdersTile({ data }: { data: PortalOrders }) {
  if (data.items.length === 0) {
    return (
      <>
        <TileEmpty>You haven’t placed an order yet.</TileEmpty>
        <Link href={SHOP} className={buttonVariants({ variant: 'secondary', size: 'sm' })}>
          Browse the shop
        </Link>
        <div className="h-3" />
      </>
    );
  }
  return (
    <ul>
      {data.items.map((order) => (
        <TileRow
          key={order.orderId}
          main={
            <>
              <Link
                href={portalOrderHref(order.orderId)}
                className="text-[14.5px] font-bold text-ink no-underline hover:underline"
              >
                {order.orderNumber}
              </Link>
              <p className="mt-0.5 text-[13px] leading-snug text-ink-muted">
                Placed {formatDateOnly(localDateOf(order.placedOn, COMMERCE_TIME_ZONE))} ·{' '}
                {order.itemCount} {order.itemCount === 1 ? 'item' : 'items'} ·{' '}
                {formatMoney(order.total)}
                {order.trackingNumber && (
                  <>
                    <br />
                    Tracking {order.trackingNumber}
                  </>
                )}
              </p>
            </>
          }
          side={
            <>
              <StatusBadge status={orderStatusLabel(order.status)} />
              <ReorderButton
                orderId={order.orderId}
                orderNumber={order.orderNumber}
                canReorder={order.canReorder}
              />
            </>
          }
        />
      ))}
    </ul>
  );
}
