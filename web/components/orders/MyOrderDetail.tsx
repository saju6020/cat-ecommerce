'use client';

import Link from 'next/link';
import { useState } from 'react';
import { Amount } from '@/components/billing/Amount';
import { PageHeader, Section } from '@/components/billing/PageHeader';
import { QueryBoundary } from '@/components/billing/QueryBoundary';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
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
import { BankTransferInstructions } from './BankTransferInstructions';
import { OrderLines, type DisplayLine } from './OrderLines';
import { ContactShopCard, ExpiredOrderCard, OrderTimeline, TrackingPanel } from './OrderPanels';
import { OrderStatusBadge } from './OrderStatusBadge';
import { OrderSummary } from './OrderSummary';

/**
 * US-ORD-19 AC3 — a `Cancelled` bank-transfer order the customer did not cancel: the cancellation
 * happened at or after the payment deadline. The customer detail carries no cancellation reason, so
 * this is read from two server instants (HO-ORD-002 §1 Amended: reported as a backend gap).
 */
export function isExpiredTransfer(order: CustomerOrderDetail): boolean {
  if (order.status !== 'Cancelled' || order.paymentMethod !== 'BankTransfer' || !order.paymentDueBy) {
    return false;
  }
  const cancelled = [...order.statusHistory].reverse().find((entry) => entry.to === 'Cancelled');
  return !!cancelled && Date.parse(cancelled.changedOn) >= Date.parse(order.paymentDueBy);
}

function Detail({ order }: { order: CustomerOrderDetail }) {
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
  }));
  const f = order.fulfilment;
  const address = [f.line1, f.line2, f.city, f.district, f.postalCode].filter(Boolean).join(', ');

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

  return (
    <div className="flex flex-col gap-5">
      <PageHeader
        back={{ href: '/portal/orders', label: 'My orders' }}
        title={`Order ${order.orderNumber}`}
        description={`Placed ${formatOrderWhen(order.placedOn, zone, true)}`}
        actions={<OrderStatusBadge status={order.status} />}
      />

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
      {order.shipment && <TrackingPanel shipment={order.shipment} />}
      {order.status === 'Dispatched' && order.fulfilmentMethod === 'Pickup' && (
        <p role="status" className="rounded-[var(--radius-md)] border border-accent/30 bg-accent-soft p-4 text-sm font-semibold text-accent-ink">
          {`Ready for collection at ${f.pickupBranchName ?? 'the branch'}${order.collectBy ? ` by ${formatDateOnly(order.collectBy)}` : ''}.`}
        </p>
      )}

      <div className="grid items-start gap-5 lg:grid-cols-[minmax(0,1.6fr)_minmax(0,1fr)]">
        <div className="flex min-w-0 flex-col gap-5">
          <Section title="Items">
            <OrderLines lines={lines} caption="Items in this order" />
          </Section>
          <Section title={order.fulfilmentMethod === 'Pickup' ? 'Collection' : 'Delivery'}>
            <dl className="m-0 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
              <dt className="text-ink-muted">Method</dt>
              <dd className="m-0">{fulfilmentLabel(order.fulfilmentMethod)}</dd>
              <dt className="text-ink-muted">Recipient</dt>
              <dd className="m-0 [overflow-wrap:anywhere]">
                {f.recipientName}
                {f.recipientPhone ? ` · ${f.recipientPhone}` : ''}
              </dd>
              {order.fulfilmentMethod === 'Delivery' ? (
                <>
                  <dt className="text-ink-muted">Address</dt>
                  <dd className="m-0 [overflow-wrap:anywhere]">{address}</dd>
                  {(f.slotDate || f.slotWindow) && (
                    <>
                      <dt className="text-ink-muted">Delivery time</dt>
                      <dd className="m-0">{`${f.slotWindow ?? f.slotDate} (${zone})`}</dd>
                    </>
                  )}
                </>
              ) : (
                <>
                  <dt className="text-ink-muted">Branch</dt>
                  <dd className="m-0 [overflow-wrap:anywhere]">
                    {[f.pickupBranchName, f.pickupBranchAddress].filter(Boolean).join(' — ')}
                  </dd>
                </>
              )}
              <dt className="text-ink-muted">Payment</dt>
              <dd className="m-0">{paymentMethodLabel(order.paymentMethod)}</dd>
              {order.invoiceNumber && (
                <>
                  <dt className="text-ink-muted">Invoice</dt>
                  <dd className="m-0">{order.invoiceNumber}</dd>
                </>
              )}
            </dl>
          </Section>
          <OrderTimeline history={order.statusHistory} zone={zone} />
        </div>
        <div className="flex min-w-0 flex-col gap-5">
          <div className="rounded-[var(--radius-md)] border border-border bg-surface p-4">
            <OrderSummary totals={order.totals} serviceLevel={f.serviceLevel} fulfilment={order.fulfilmentMethod} />
            <dl className="m-0 mt-3 text-sm">
              <div className="flex justify-between gap-3 py-1">
                <dt className="text-ink-muted">Paid so far</dt>
                <dd className="m-0"><Amount money={order.amountPaid} /></dd>
              </div>
              <div className="flex justify-between gap-3 py-1">
                <dt className="text-ink-muted">Refunded</dt>
                <dd className="m-0"><Amount money={order.amountRefunded} /></dd>
              </div>
            </dl>
          </div>
          {order.refunds.length > 0 && (
            <Section title="Refunds">
              <ul className="m-0 list-none p-0 text-sm">
                {order.refunds.map((refund) => (
                  <li key={`${refund.method}-${refund.issuedOn}`} className="flex flex-wrap justify-between gap-2 py-1">
                    <span>{`${refundMethodLabel(refund.method)} · ${formatOrderWhen(refund.issuedOn, zone)}`}</span>
                    <Amount money={refund.amount} />
                  </li>
                ))}
              </ul>
            </Section>
          )}
          {order.returns.length > 0 && (
            <Section title="Your returns">
              <ul className="m-0 list-none p-0 text-sm">
                {order.returns.map((ret) => (
                  <li key={ret.id} className="py-1">
                    {`Return — ${returnStatusLabel(ret.status)}`}
                  </li>
                ))}
              </ul>
            </Section>
          )}
          {(order.canRequestReturn || order.status === 'Delivered') && (
            <Section title="Returns">
              {order.canRequestReturn ? (
                <Link href={`/portal/orders/${order.id}/return`} className={buttonVariants({ variant: 'secondary', size: 'sm' })}>
                  Request a return
                </Link>
              ) : (
                <p className="text-sm text-ink-muted">
                  A return can&rsquo;t be requested for this order.
                  {order.returnWindowEndsOn ? ` The return window ends on ${formatDateOnly(order.returnWindowEndsOn)}.` : ''}
                </p>
              )}
            </Section>
          )}
          {order.canCancel && !contact && (
            <Section title="Cancel this order">
              <div>
                <Button variant="destructive" size="sm" onClick={() => setConfirmOpen(true)}>
                  Cancel order
                </Button>
              </div>
              {cancelError && (
                <p role="alert" className="text-sm font-semibold text-danger-ink">
                  {cancelError}
                </p>
              )}
            </Section>
          )}
          {(contact || (!order.canCancel && ['Packing', 'Dispatched'].includes(order.status))) && (
            <ContactShopCard branch={branch} />
          )}
        </div>
      </div>

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title={`Cancel order ${order.orderNumber}?`}
        consequence={
          <>
            Your reserved items are released.
            {paid ? ' Any money you have paid will be refunded by the shop.' : ''}
          </>
        }
        confirmLabel="Cancel order"
        cancelLabel="Keep order"
        pendingLabel="Cancelling…"
        variant="destructive"
        onConfirm={onCancel}
      />
    </div>
  );
}

/** `/portal/orders/[orderId]` — HO-ORD-002 §11.6 `OrderDetail`. */
export function MyOrderDetail({ orderId }: { orderId: string }) {
  const query = useMyOrder(orderId);
  return (
    <QueryBoundary
      query={query}
      notFoundTitle="We can't find that order"
      notFoundDescription="It may not be yours, or the link may be wrong."
      loadingLabel="Loading your order…"
    >
      {(order) => <Detail order={order} />}
    </QueryBoundary>
  );
}
