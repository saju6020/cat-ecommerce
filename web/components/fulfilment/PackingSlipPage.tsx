'use client';

import { Amount } from '@/components/billing/Amount';
import { QueryBoundary } from '@/components/billing/QueryBoundary';
import { PrintSheet } from '@/components/print/PrintSheet';
import type { PackingSlip } from '@/lib/api/staff-orders';
import { fulfilmentLabel, formatOrderWhen } from '@/lib/format/orders';
import { usePackingSlip } from '@/lib/hooks/useStaffOrders';

/**
 * `components/fulfilment/PackingSlipPage.tsx` — HO-ORD-002 §11.10, US-ORD-10 AC2. The slip that goes in
 * the parcel: order number and date, customer and recipient, address or pickup branch, slot, every line
 * with quantity, **batch numbers** and the cat's name, the delivery instructions, and — for cash on
 * delivery — the amount the courier must collect, prominently. A4 `PrintSheet`: a real table with a
 * caption and headers, no navigation on paper.
 */
function Slip({ slip }: { slip: PackingSlip }) {
  const zone = slip.branchTimeZoneId;
  return (
    <div className="flex flex-col gap-4">
      <header className="border-b border-border-strong pb-3">
        <h2 className="text-lg font-semibold">Packing slip — {slip.orderNumber}</h2>
        <p className="text-xs text-ink-muted">Placed {formatOrderWhen(slip.placedOn, zone, true)}</p>
      </header>
      {slip.codAmountDue && (
        <p className="rounded-[var(--radius-sm)] border-2 border-ink p-3 text-center text-base font-bold">
          Cash on delivery — collect <Amount money={slip.codAmountDue} />
        </p>
      )}
      <dl className="m-0 grid grid-cols-[minmax(0,8rem)_minmax(0,1fr)] gap-x-4 gap-y-1">
        <dt className="text-ink-muted">Customer</dt>
        <dd className="m-0 [overflow-wrap:anywhere]">{slip.customerName}</dd>
        <dt className="text-ink-muted">Deliver to</dt>
        <dd className="m-0 [overflow-wrap:anywhere]">
          {slip.recipientName}
          {slip.recipientPhone ? `, ${slip.recipientPhone}` : ''}
        </dd>
        <dt className="text-ink-muted">{slip.fulfilmentMethod === 'Pickup' ? 'Collect at' : 'Address'}</dt>
        <dd className="m-0 [overflow-wrap:anywhere]">
          {slip.fulfilmentMethod === 'Pickup' ? (slip.pickupBranchName ?? '—') : (slip.addressText ?? '—')}
        </dd>
        <dt className="text-ink-muted">Method</dt>
        <dd className="m-0">{fulfilmentLabel(slip.fulfilmentMethod)}</dd>
        {slip.slot && (
          <>
            <dt className="text-ink-muted">Time window</dt>
            <dd className="m-0">{slip.slot}</dd>
          </>
        )}
        {slip.deliveryInstructions && (
          <>
            <dt className="text-ink-muted">Instructions</dt>
            <dd className="m-0 [overflow-wrap:anywhere]">{slip.deliveryInstructions}</dd>
          </>
        )}
      </dl>
      <table className="w-full border-collapse text-left">
        <caption className="sr-only">Items in order {slip.orderNumber}</caption>
        <thead>
          <tr className="border-b border-border-strong">
            <th scope="col" className="py-1 pr-2">#</th>
            <th scope="col" className="py-1 pr-2">Item</th>
            <th scope="col" className="py-1 pr-2 text-right">Qty</th>
            <th scope="col" className="py-1 pr-2">Batch</th>
            <th scope="col" className="py-1">For</th>
          </tr>
        </thead>
        <tbody>
          {slip.lines.map((line) => (
            <tr key={line.lineNumber} className="border-b border-border align-top">
              <td className="py-1.5 pr-2">{line.lineNumber}</td>
              <td className="py-1.5 pr-2 [overflow-wrap:anywhere]">{`${line.productName} ${line.variantLabel}`.trim()}</td>
              <td className="py-1.5 pr-2 text-right font-semibold tabular-nums">{line.quantity}</td>
              <td className="py-1.5 pr-2 [overflow-wrap:anywhere]">{line.batchNumbers.join(', ') || '—'}</td>
              <td className="py-1.5">{line.catName ?? '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function PackingSlipPage({ orderId }: { orderId: string }) {
  const query = usePackingSlip(orderId);
  return (
    <PrintSheet
      title="Packing slip"
      back={{ href: `/staff/orders/${orderId}`, label: 'Back to the order' }}
      size="A4"
      label="Packing slip"
    >
      <QueryBoundary query={query} notFoundTitle="Order not found" loadingLabel="Loading the packing slip…">
        {(slip) => <Slip slip={slip} />}
      </QueryBoundary>
    </PrintSheet>
  );
}
