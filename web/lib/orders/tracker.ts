import type { TrackerStep, TrackerStatus } from '@/components/hearth/OrderTracker';
import type { CustomerOrderDetail, OrderStatus } from '@/lib/api/my-orders';
import { formatOrderWhen } from '@/lib/format/orders';

/**
 * `lib/orders/tracker.ts` - maps the server's order status and status history onto the five-node
 * tracker (docs/design/orders/OrderDetail.dc.html): Placed, Confirmed, Packing, Dispatched, Delivered.
 * It only reads what the server sent: the node for the order's current status is `current`, earlier
 * nodes are `done`, later ones `future`; a captured time comes from `statusHistory` (and `placedOn`
 * for Placed). A delivered, returned or refunded order is complete. A cancelled order has no
 * tracker (`null`), because a cancellation is not a stage on the way.
 */

const STAGES = ['Placed', 'Confirmed', 'Packing', 'Dispatched', 'Delivered'] as const;
type Stage = (typeof STAGES)[number];

/** Which node the order is on. `PendingPayment` is still "Placed": the order waits for the transfer. */
const STAGE_OF_STATUS: Partial<Record<OrderStatus, Stage>> = {
  PendingPayment: 'Placed',
  Confirmed: 'Confirmed',
  Packing: 'Packing',
  Dispatched: 'Dispatched',
  Delivered: 'Delivered',
  Returned: 'Delivered',
  Refunded: 'Delivered',
};

const COMPLETE: OrderStatus[] = ['Delivered', 'Returned', 'Refunded'];

function labelOf(stage: Stage, pickup: boolean): string {
  if (!pickup) return stage;
  if (stage === 'Dispatched') return 'Ready to collect';
  if (stage === 'Delivered') return 'Collected';
  return stage;
}

export function orderTrackerSteps(
  order: Pick<
    CustomerOrderDetail,
    'status' | 'fulfilmentMethod' | 'statusHistory' | 'placedOn' | 'branchTimeZoneId' | 'fulfilment'
  >,
  { waitingForTransfer = false }: { waitingForTransfer?: boolean } = {},
): TrackerStep[] | null {
  const stage = STAGE_OF_STATUS[order.status];
  if (!stage) return null;
  const pickup = order.fulfilmentMethod === 'Pickup';
  const currentIndex = STAGES.indexOf(stage);
  const complete = COMPLETE.includes(order.status);
  const zone = order.branchTimeZoneId;

  const when = (name: Stage): string | undefined => {
    if (name === 'Placed') return formatOrderWhen(order.placedOn, zone);
    const entry = [...order.statusHistory].reverse().find((change) => change.to === name);
    return entry ? formatOrderWhen(entry.changedOn, zone) : undefined;
  };

  return STAGES.map((name, index): TrackerStep => {
    const status: TrackerStatus =
      complete || index < currentIndex ? 'done' : index === currentIndex ? 'current' : 'future';
    let caption = status === 'future' ? undefined : when(name);
    if (status === 'current' && name === 'Placed' && waitingForTransfer) {
      caption = 'Waiting for your transfer';
    }
    if (status === 'future' && name === 'Delivered' && !pickup) {
      const slot = order.fulfilment.slotWindow;
      if (slot) caption = `Expected ${slot}`;
    }
    return { label: labelOf(name, pickup), status, caption };
  });
}
