'use client';

import Link from 'next/link';
import { CheckCircle2 } from 'lucide-react';
import { QueryBoundary } from '@/components/billing/QueryBoundary';
import { buttonVariants } from '@/components/ui/button';
import { BankTransferInstructions } from '@/components/orders/BankTransferInstructions';
import { OrderLines, type DisplayLine } from '@/components/orders/OrderLines';
import { OrderSummary } from '@/components/orders/OrderSummary';
import { fulfilmentLabel, paymentMethodLabel, paymentNextStep } from '@/lib/format/orders';
import type { CustomerOrderDetail } from '@/lib/api/my-orders';
import { useMyOrder, useRefreshMyOrder, useSubmitTransferReference } from '@/lib/hooks/useMyOrders';

function Placed({ order }: { order: CustomerOrderDetail }) {
  const submitReference = useSubmitTransferReference(order.id);
  const refresh = useRefreshMyOrder(order.id);
  const lines: DisplayLine[] = order.lines.map((line) => ({
    key: line.lineId,
    productName: line.productName,
    variantLabel: line.variantLabel,
    cat: line.cat,
    quantity: line.quantity,
    unitPrice: line.unitPrice,
    lineTotal: line.lineTotal,
  }));

  return (
    <div className="flex flex-col gap-5">
      <header className="flex flex-col gap-1">
        <p className="flex items-center gap-2 text-sm font-semibold text-accent-ink">
          <CheckCircle2 size={18} aria-hidden="true" />
          Order placed
        </p>
        <h1 className="font-display text-2xl font-semibold text-ink [overflow-wrap:anywhere]">
          {`Thank you — order ${order.orderNumber}`}
        </h1>
        <p className="text-sm text-ink-muted">
          {`${fulfilmentLabel(order.fulfilmentMethod)} · ${paymentMethodLabel(order.paymentMethod)}`}
        </p>
      </header>

      {order.paymentMethod !== 'BankTransfer' && (
        <p role="status" className="rounded-[var(--radius-md)] border border-accent/30 bg-accent-soft p-4 text-sm font-semibold text-accent-ink">
          {paymentNextStep(order.paymentMethod, order.fulfilment.pickupBranchName)}
        </p>
      )}

      {order.transferInstructions && order.status === 'PendingPayment' && (
        <BankTransferInstructions
          instructions={order.transferInstructions}
          paymentDueBy={order.paymentDueBy}
          zone={order.branchTimeZoneId}
          reportedReference={order.customerTransferReference}
          onSubmitReference={(reference) => submitReference.mutateAsync(reference)}
          onExpired={refresh}
        />
      )}

      <div className="grid items-start gap-5 lg:grid-cols-[minmax(0,1.6fr)_minmax(0,1fr)]">
        <section aria-label="Items in your order" className="min-w-0 rounded-[var(--radius-md)] border border-border bg-surface px-4">
          <OrderLines lines={lines} caption="Items in your order" />
        </section>
        <div className="rounded-[var(--radius-md)] border border-border bg-surface p-4">
          <OrderSummary
            totals={order.totals}
            serviceLevel={order.fulfilment.serviceLevel}
            fulfilment={order.fulfilmentMethod}
          />
        </div>
      </div>

      <div className="flex flex-wrap gap-3">
        <Link href={`/portal/orders/${order.id}`} className={buttonVariants({ variant: 'primary' })}>
          View your order
        </Link>
        <Link href="/shop" className={buttonVariants({ variant: 'secondary' })}>
          Continue shopping
        </Link>
      </div>
    </div>
  );
}

/** `/checkout/complete/[orderId]` — HO-ORD-002 §11.5 `OrderPlaced`. */
export function OrderPlacedScreen({ orderId }: { orderId: string }) {
  const query = useMyOrder(orderId);
  return (
    <QueryBoundary
      query={query}
      notFoundTitle="We can't find that order"
      notFoundDescription="It may not be yours, or the link may be wrong."
      loadingLabel="Loading your order…"
    >
      {(order) => <Placed order={order} />}
    </QueryBoundary>
  );
}
