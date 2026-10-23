'use client';

import { resolveFileUrl } from '@/lib/api/files';
import Link from 'next/link';
import { useState, type ReactNode } from 'react';
import { ArrowLeft, ArrowUpRight, CircleCheck, Phone } from 'lucide-react';
import { Amount } from '@/components/billing/Amount';
import { QueryBoundary } from '@/components/billing/QueryBoundary';
import { cardClass, flatCardClass } from '@/components/hearth/card';
import { StatusHero } from '@/components/hearth/StatusHero';
import { Button, buttonVariants } from '@/components/ui/button';
import { hasOrderingCode, describeOrderingError } from '@/lib/api/errors';
import type { CustomerOrderDetail, ReorderResult } from '@/lib/api/my-orders';
import {
  fulfilmentLabel,
  formatOrderWhen,
  paymentMethodLabel,
  refundMethodLabel,
  returnStatusLabel,
} from '@/lib/format/orders';
import { formatDateOnly } from '@/lib/format/time';
import { moneySign } from '@/lib/money';
import {
  useCancelMyOrder,
  useMyOrder,
  usePublicBranches,
  useRefreshMyOrder,
  useReorder,
  useSubmitTransferReference,
} from '@/lib/hooks/useMyOrders';
import { useIdempotencyKey } from '@/lib/hooks/useIdempotencyKey';
import { orderTrackerSteps } from '@/lib/orders/tracker';
import { cn } from '@/lib/utils';
import { BankTransferInstructions } from './BankTransferInstructions';
import { CancelOrderDialog } from './CancelOrderDialog';
import { OrderLines, type DisplayLine } from './OrderLines';
import {
  ContactShopCard,
  ExpiredOrderCard,
  OrderTimeline,
  TrackingPanel,
  safeHttpUrl,
} from './OrderPanels';
import { OrderStatusBadge } from './OrderStatusBadge';
import { OrderSummary } from './OrderSummary';
import { OrderTrackerCard } from './OrderTrackerCard';

/**
 * US-ORD-19 AC3 — a `Cancelled` bank-transfer order the customer did not cancel: the cancellation
 * happened at or after the payment deadline. The customer detail carries no cancellation reason, so
 * this is read from two server instants (HO-ORD-002 §1 Amended: reported as a backend gap).
 */
export function isExpiredTransfer(order: CustomerOrderDetail): boolean {
  if (
    order.status !== 'Cancelled' ||
    order.paymentMethod !== 'BankTransfer' ||
    !order.paymentDueBy
  ) {
    return false;
  }
  const cancelled = [...order.statusHistory].reverse().find((entry) => entry.to === 'Cancelled');
  return !!cancelled && Date.parse(cancelled.changedOn) >= Date.parse(order.paymentDueBy);
}

const KV_TERM = 'text-xs text-ink-muted';
const HEARTH_STATUSES = ['PendingPayment', 'Confirmed', 'Packing', 'Dispatched', 'Delivered'];

function Detail({ order, justPlaced }: { order: CustomerOrderDetail; justPlaced: boolean }) {
  const zone = order.branchTimeZoneId;
  const branches = usePublicBranches();
  const cancel = useCancelMyOrder(order.id);
  const submitReference = useSubmitTransferReference(order.id);
  const refresh = useRefreshMyOrder(order.id);
  const reorder = useReorder();
  const idempotency = useIdempotencyKey();
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [cancelError, setCancelError] = useState<string | null>(null);
  const [contact, setContact] = useState(false);
  const [reorderResult, setReorderResult] = useState<ReorderResult | null>(null);
  const [reorderError, setReorderError] = useState<string | null>(null);

  const branch =
    branches.data?.find((b) => b.id === order.fulfilment.pickupBranchId) ?? branches.data?.[0];
  const paid = moneySign(order.amountPaid.amount) !== 'zero';
  const expired = isExpiredTransfer(order);
  const lines: DisplayLine[] = order.lines.map((line) => ({
    key: line.lineId,
    productName: line.productName,
    variantLabel: line.variantLabel,
    cat: line.cat,
    quantity: line.quantity,
    unitPrice: line.unitPrice,
    lineTotal: line.lineTotal,
    taxRatePercent: line.taxRatePercent,
    imageUrl: resolveFileUrl(line.imagePath),
  }));
  const f = order.fulfilment;
  const pickup = order.fulfilmentMethod === 'Pickup';
  const address = [f.line1, f.line2, f.city, f.district, f.postalCode].filter(Boolean).join(', ');
  const placedText = formatOrderWhen(order.placedOn, zone, true);
  const slotText = f.slotWindow ?? f.slotDate;
  const trackHref = safeHttpUrl(order.shipment?.trackingUrl);
  const showHero = HEARTH_STATUSES.includes(order.status);
  const cancelBlocked = !order.canCancel && ['Packing', 'Dispatched'].includes(order.status);
  const cancelReason = contact
    ? 'This order can’t be cancelled online any more.'
    : order.status === 'Dispatched'
      ? 'It has already left the shop, so it can’t be cancelled here.'
      : 'We’ve started packing it, so it can’t be cancelled here.';
  const returnsNotYet =
    !order.canRequestReturn && ['Confirmed', 'Packing', 'Dispatched'].includes(order.status);
  const steps = orderTrackerSteps(order, { waitingForTransfer: order.status === 'PendingPayment' });
  const codUnpaid = order.paymentMethod === 'CashOnDelivery' && !paid;

  async function onCancel() {
    setCancelError(null);
    try {
      await cancel.mutateAsync();
      setConfirmOpen(false);
    } catch (error) {
      setConfirmOpen(false);
      if (hasOrderingCode(error, 'CancellationNotAvailable')) setContact(true);
      else setCancelError(describeOrderingError(error));
    }
  }

  function onReorder() {
    setReorderError(null);
    reorder.mutate(
      { orderId: order.id, idempotencyKey: idempotency.keyFor(order.id) },
      {
        onSuccess: (result) => {
          idempotency.renew();
          setReorderResult(result);
        },
        onError: (error) => {
          idempotency.settleFailure(error);
          setReorderError(describeOrderingError(error));
        },
      },
    );
  }

  const heroSentence = (): ReactNode => {
    switch (order.status) {
      case 'PendingPayment':
        return order.transferInstructions ? (
          <>
            We&rsquo;re holding your items until you transfer{' '}
            <strong>
              <Amount money={order.transferInstructions.amountDue} />
            </strong>
            .
          </>
        ) : (
          'We’re waiting for your payment.'
        );
      case 'Confirmed':
        return 'We’ve got your order and will start packing it soon.';
      case 'Packing':
        return pickup
          ? 'We’re packing it now and will tell you when it’s ready to collect.'
          : 'We’re packing it now and will tell you when it’s on its way.';
      case 'Dispatched':
        if (pickup) {
          return `Ready for collection at ${f.pickupBranchName ?? 'the branch'}${order.collectBy ? ` by ${formatDateOnly(order.collectBy)}` : ''}.`;
        }
        return (
          <>
            {order.shipment ? `Dispatched with ${order.shipment.courierName}. ` : 'Dispatched. '}
            {slotText && (
              <>
                Arriving <strong>{`${slotText} (${zone})`}</strong>.{' '}
              </>
            )}
            {codUnpaid && (
              <>
                Have <Amount money={order.totals.grandTotal} /> ready for the courier.
              </>
            )}
          </>
        );
      case 'Delivered':
        return 'Your order has been delivered.';
      default:
        return null;
    }
  };

  const headline: Record<string, string> = {
    PendingPayment: 'Waiting for your transfer',
    Confirmed: 'Your order is confirmed',
    Packing: 'We’re packing your order',
    Dispatched: pickup ? 'Ready for collection' : 'On its way to you',
    Delivered: 'Delivered',
  };

  return (
    <div className="flex flex-col gap-6">
      <Link
        href="/portal/orders"
        className="inline-flex min-h-11 w-fit items-center gap-1.5 text-sm font-semibold text-accent-ink no-underline hover:underline"
      >
        <ArrowLeft size={14} aria-hidden="true" />
        All orders
      </Link>

      {justPlaced && (
        // Owner, 2026-10-19: checkout lands here; the order is confirmed in words before anything else.
        <p
          role="status"
          className="m-0 flex items-center gap-2.5 rounded-[16px] bg-accent-tint px-4 py-3 text-[15px] font-semibold text-accent-ink"
        >
          <CircleCheck size={20} aria-hidden="true" className="flex-none" />
          {`Thank you, your order ${order.orderNumber} is placed.`}
        </p>
      )}

      {showHero ? (
        <StatusHero
          headingId="order-heading"
          eyebrow={`Order ${order.orderNumber} · placed ${placedText}`}
          headline={headline[order.status] ?? orderTitle(order)}
          actions={
            <>
              {order.shipment && trackHref && (
                <a
                  href={trackHref}
                  target="_blank"
                  rel="noopener noreferrer"
                  className={buttonVariants({ variant: 'pill', size: 'pillMd' })}
                >
                  {`Track parcel · ${order.shipment.courierName}${order.shipment.trackingNumber ? ` ${order.shipment.trackingNumber}` : ''}`}
                  <ArrowUpRight size={14} aria-hidden="true" />
                  <span className="sr-only"> (opens in a new tab)</span>
                </a>
              )}
              {branch?.phoneNumber && (
                <a
                  href={`tel:${branch.phoneNumber.replace(/\s/g, '')}`}
                  className={buttonVariants({ variant: 'pillSecondary', size: 'pillMd' })}
                >
                  <Phone size={14} aria-hidden="true" />
                  {`Call ${branch.name}`}
                </a>
              )}
            </>
          }
        >
          <p className="m-0">{heroSentence()}</p>
        </StatusHero>
      ) : (
        <section className={cn(cardClass, 'flex flex-col gap-2.5 sm:px-8 sm:py-7')}>
          <p className="m-0 text-[11px] font-bold uppercase tracking-[0.07em] text-ink-muted">
            {`Placed ${placedText} · ${paymentMethodLabel(order.paymentMethod)}`}
          </p>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <h1 className="m-0 font-display text-[32px] font-semibold leading-[1.1] text-ink [overflow-wrap:anywhere] md:text-[40px]">
              {orderTitle(order)}
            </h1>
            <OrderStatusBadge status={order.status} />
          </div>
        </section>
      )}

      <OrderTrackerCard steps={steps} />

      <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1.5fr)_minmax(0,1fr)]">
        <div className="flex min-w-0 flex-col gap-[18px]">
          {order.status === 'PendingPayment' && order.transferInstructions && (
            <BankTransferInstructions
              instructions={order.transferInstructions}
              paymentDueBy={order.paymentDueBy}
              zone={zone}
              reportedReference={order.customerTransferReference}
              onSubmitReference={(reference) => submitReference.mutateAsync(reference)}
              onExpired={refresh}
            />
          )}
          {expired && (
            <ExpiredOrderCard
              canReorder={order.canReorder}
              pending={reorder.isPending}
              result={reorderResult}
              error={reorderError}
              onReorder={onReorder}
            />
          )}
          <section
            aria-labelledby="order-items-heading"
            className={cn(cardClass, 'flex flex-col gap-3 sm:px-6')}
          >
            <h2
              id="order-items-heading"
              className="m-0 mt-1.5 font-display text-[22px] font-semibold text-ink"
            >
              In this order
            </h2>
            <OrderLines lines={lines} caption="Items in this order" appearance="hearth" />
            <OrderSummary
              appearance="hearth"
              bare
              totals={order.totals}
              serviceLevel={f.serviceLevel}
              fulfilment={order.fulfilmentMethod}
            />
            <dl className="m-0 grid gap-x-10 gap-y-2 rounded-2xl bg-surface-sunken px-4 py-3.5 text-sm sm:grid-cols-2">
              <div>
                <dt className={KV_TERM}>Paid so far</dt>
                <dd className="m-0">
                  <Amount money={order.amountPaid} />
                </dd>
              </div>
              <div>
                <dt className={KV_TERM}>Refunded</dt>
                <dd className="m-0">
                  <Amount money={order.amountRefunded} />
                </dd>
              </div>
            </dl>
          </section>
          {order.shipment && <TrackingPanel shipment={order.shipment} />}
        </div>

        <div className="flex min-w-0 flex-col gap-[18px]">
          <section aria-labelledby="where-heading" className={cardClass}>
            <h2 id="where-heading" className="m-0 text-base font-bold text-ink">
              {pickup ? 'Collecting from' : 'Delivering to'}
            </h2>
            <dl className="m-0 mt-2.5 flex flex-col gap-2 text-sm">
              <div>
                <dt className={KV_TERM}>Method</dt>
                <dd className="m-0">{fulfilmentLabel(order.fulfilmentMethod)}</dd>
              </div>
              <div>
                <dt className={KV_TERM}>Recipient</dt>
                <dd className="m-0 [overflow-wrap:anywhere]">
                  {f.recipientName}
                  {f.recipientPhone ? ` · ${f.recipientPhone}` : ''}
                </dd>
              </div>
              {!pickup ? (
                <>
                  <div>
                    <dt className={KV_TERM}>Address</dt>
                    <dd className="m-0 [overflow-wrap:anywhere]">{address}</dd>
                  </div>
                  {(f.slotDate || f.slotWindow) && (
                    <div>
                      <dt className={KV_TERM}>Delivery time</dt>
                      <dd className="m-0">{`${f.slotWindow ?? f.slotDate} (${zone})`}</dd>
                    </div>
                  )}
                </>
              ) : (
                <div>
                  <dt className={KV_TERM}>Branch</dt>
                  <dd className="m-0 [overflow-wrap:anywhere]">
                    {[f.pickupBranchName, f.pickupBranchAddress].filter(Boolean).join(' — ')}
                  </dd>
                </div>
              )}
              <div>
                <dt className={KV_TERM}>Payment</dt>
                <dd className="m-0">{paymentMethodLabel(order.paymentMethod)}</dd>
              </div>
              {order.invoiceNumber && (
                <div>
                  <dt className={KV_TERM}>Invoice</dt>
                  <dd className="m-0">{order.invoiceNumber}</dd>
                </div>
              )}
            </dl>
          </section>

          <OrderTimeline history={order.statusHistory} zone={zone} />

          {order.refunds.length > 0 && (
            <section aria-labelledby="refunds-heading" className={cardClass}>
              <h2 id="refunds-heading" className="m-0 text-base font-bold text-ink">
                Refunds
              </h2>
              <ul className="m-0 mt-2 list-none p-0 text-sm">
                {order.refunds.map((refund) => (
                  <li
                    key={`${refund.method}-${refund.issuedOn}`}
                    className="flex flex-wrap justify-between gap-2 border-b border-dashed border-border py-2 last:border-0"
                  >
                    <span>{`${refundMethodLabel(refund.method)} · ${formatOrderWhen(refund.issuedOn, zone)}`}</span>
                    <Amount money={refund.amount} />
                  </li>
                ))}
              </ul>
            </section>
          )}
          {order.returns.length > 0 && (
            <section aria-labelledby="your-returns-heading" className={cardClass}>
              <h2 id="your-returns-heading" className="m-0 text-base font-bold text-ink">
                Your returns
              </h2>
              <ul className="m-0 mt-2 list-none p-0 text-sm">
                {order.returns.map((ret) => (
                  <li key={ret.id} className="py-1">
                    {`Return — ${returnStatusLabel(ret.status)}`}
                  </li>
                ))}
              </ul>
            </section>
          )}
          {(order.canRequestReturn || order.status === 'Delivered' || returnsNotYet) && (
            <section
              aria-labelledby="returns-heading"
              className={cn(flatCardClass, 'flex flex-col gap-2')}
            >
              <h2 id="returns-heading" className="sr-only">
                Returns
              </h2>
              {order.canRequestReturn ? (
                <Link
                  href={`/portal/orders/${order.id}/return`}
                  className={cn(
                    buttonVariants({ variant: 'pillSecondary', size: 'pillMd' }),
                    'self-start',
                  )}
                >
                  Request a return
                </Link>
              ) : returnsNotYet ? (
                <>
                  <Button
                    variant="pillSecondary"
                    size="pillMd"
                    disabled
                    aria-describedby="return-why"
                    className="self-start disabled:bg-surface"
                  >
                    Request a return
                  </Button>
                  <p id="return-why" className="m-0 text-[13px] text-ink-muted">
                    You can request a return once the order has been delivered.
                  </p>
                </>
              ) : (
                <p className="m-0 text-sm text-ink-muted">
                  A return can&rsquo;t be requested for this order.
                  {order.returnWindowEndsOn
                    ? ` The return window ends on ${formatDateOnly(order.returnWindowEndsOn)}.`
                    : ''}
                </p>
              )}
            </section>
          )}
          {order.canCancel && !contact && (
            <section
              aria-labelledby="cancel-heading"
              className={cn(flatCardClass, 'flex flex-col gap-2.5')}
            >
              <h2 id="cancel-heading" className="m-0 text-base font-bold text-ink">
                Cancel this order
              </h2>
              <div>
                <Button variant="destructive" size="pillMd" onClick={() => setConfirmOpen(true)}>
                  Cancel order
                </Button>
              </div>
              {cancelError && (
                <p role="alert" className="m-0 text-sm font-semibold text-danger-ink">
                  {cancelError}
                </p>
              )}
            </section>
          )}
          {(contact || cancelBlocked) && <ContactShopCard branch={branch} reason={cancelReason} />}
        </div>
      </div>

      <CancelOrderDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        orderNumber={order.orderNumber}
        amountPaid={paid ? order.amountPaid : null}
        onConfirm={onCancel}
      />
    </div>
  );
}

function orderTitle(order: CustomerOrderDetail): string {
  return `Order ${order.orderNumber}`;
}

/** `/portal/orders/[orderId]` — HO-ORD-002 §11.6 `OrderDetail`. */
export function MyOrderDetail({
  orderId,
  justPlaced = false,
}: {
  orderId: string;
  justPlaced?: boolean;
}) {
  const query = useMyOrder(orderId);
  return (
    <QueryBoundary
      query={query}
      notFoundTitle="We can't find that order"
      notFoundDescription="It may not be yours, or the link may be wrong."
      loadingLabel="Loading your order…"
    >
      {(order) => <Detail order={order} justPlaced={justPlaced} />}
    </QueryBoundary>
  );
}
