import Link from 'next/link';
import { Amount } from '@/components/billing/Amount';
import { Section } from '@/components/billing/PageHeader';
import { Button } from '@/components/ui/button';
import type { StaffOrderDetail, StaffOrderLine } from '@/lib/api/staff-orders';
import { fulfilmentLabel, formatOrderWhen, paymentMethodLabel } from '@/lib/format/orders';
import { formatDateOnly } from '@/lib/format/time';
import { OrderLines, type DisplayLine } from './OrderLines';
import { safeHttpUrl } from './OrderPanels';
import { OrderStatusBadge } from './OrderStatusBadge';

/**
 * `components/orders/StaffOrderPanels.tsx` — HO-ORD-002 §11.8. The read-only sections of the staff order
 * detail: customer and fulfilment, the lines with their **batch numbers and expiry**, money received and
 * returned, the shipment, and the status history **with staff names**. Everything shown is the server's;
 * the only decision made here is which fields exist.
 */
function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <>
      <dt className="text-ink-muted">{label}</dt>
      <dd className="m-0 min-w-0 [overflow-wrap:anywhere]">{children}</dd>
    </>
  );
}

export function CustomerFulfilmentPanel({ order }: { order: StaffOrderDetail }) {
  const f = order.fulfilment;
  const address = [f.line1, f.line2, f.city, f.district, f.postalCode].filter(Boolean).join(', ');
  return (
    <Section title="Customer and fulfilment">
      <dl className="m-0 grid grid-cols-[minmax(0,9rem)_minmax(0,1fr)] gap-x-4 gap-y-1 text-sm">
        <Row label="Customer">
          {order.customerAnonymised ? (
            'Customer anonymised'
          ) : order.customerId ? (
            <Link href={`/staff/customers/${order.customerId}`} className="font-semibold text-accent-ink hover:underline">
              {f.recipientName}
            </Link>
          ) : (
            f.recipientName
          )}
        </Row>
        <Row label="Fulfilment">{fulfilmentLabel(order.fulfilmentMethod)}</Row>
        {f.recipientPhone && <Row label="Phone">{f.recipientPhone}</Row>}
        {address && <Row label="Address">{address}</Row>}
        {f.deliveryInstructions && <Row label="Instructions">{f.deliveryInstructions}</Row>}
        {f.zoneName && <Row label="Delivery zone">{f.zoneName}</Row>}
        {f.slotDate && f.slotWindow && <Row label="Slot">{`${formatDateOnly(f.slotDate)}, ${f.slotWindow} (${order.branchTimeZoneId})`}</Row>}
        {f.pickupBranchName && <Row label="Collect at">{f.pickupBranchName}</Row>}
        {order.collectBy && <Row label="Collect by">{formatDateOnly(order.collectBy)}</Row>}
        <Row label="Payment">{paymentMethodLabel(order.paymentMethod)}</Row>
        {order.discountCode && <Row label="Discount code">{order.discountCode}</Row>}
      </dl>
    </Section>
  );
}

export function StaffLinesPanel({
  order,
  onReallocate,
}: {
  order: StaffOrderDetail;
  /** Set when the server allows fulfilment work on this order; absent otherwise. */
  onReallocate?: (line: StaffOrderLine) => void;
}) {
  const lines: DisplayLine[] = order.lines.map((l) => ({
    key: l.lineId,
    productName: l.productName,
    variantLabel: l.variantLabel,
    cat: l.cat,
    quantity: l.quantity,
    unitPrice: l.unitPrice,
    lineTotal: l.lineTotal,
  }));
  return (
    <Section title="Items">
      <OrderLines
        caption="Items on this order"
        lines={lines}
        extra={(line) => {
          const source = order.lines.find((l) => l.lineId === line.key);
          if (!source) return null;
          return (
            <div className="mt-1 text-sm text-ink-muted">
              {source.allocations.length === 0 ? (
                <p>Not allocated to a batch yet.</p>
              ) : (
                <ul aria-label={`Batches for ${source.productName}`} className="m-0 list-none p-0">
                  {source.allocations.map((a) => (
                    <li key={`${a.batchId}-${a.locationId}`}>
                      Batch {a.batchNumber}
                      {a.expiresOn ? `, expires ${formatDateOnly(a.expiresOn)}` : ''} · {a.locationName} · {a.quantity} unit{a.quantity === 1 ? '' : 's'}
                    </li>
                  ))}
                </ul>
              )}
              {source.quantityReturned > 0 && <p>{source.quantityReturned} returned.</p>}
              {onReallocate && (
                <Button variant="ghost" size="sm" onClick={() => onReallocate(source)}>
                  Reallocate {source.productName}
                </Button>
              )}
            </div>
          );
        }}
      />
    </Section>
  );
}

export function PaymentsPanel({ order }: { order: StaffOrderDetail }) {
  return (
    <Section title="Money">
      <dl className="m-0 grid grid-cols-[minmax(0,9rem)_minmax(0,1fr)] gap-x-4 gap-y-1 text-sm">
        <Row label="Paid"><Amount money={order.amountPaid} /></Row>
        <Row label="Refunded"><Amount money={order.amountRefunded} /></Row>
        <Row label="Refund due"><Amount money={order.refundDue} /></Row>
        {order.invoiceNumber && <Row label="Invoice">{order.invoiceNumber}</Row>}
        {order.codCollection && (
          <Row label="Cash collected">
            <Amount money={order.codCollection.amount} /> by {order.codCollection.collectorName} ({order.codCollection.collectorKind === 'Staff' ? 'staff' : 'courier'}) on{' '}
            {formatOrderWhen(order.codCollection.collectedOn, order.branchTimeZoneId)}
          </Row>
        )}
        {order.payments.length > 0 && (
          <Row label="Payments">
            <ul aria-label="Payments on this order" className="m-0 list-none p-0">
              {order.payments.map((p) => (
                <li key={p.paymentId}>
                  <Link href={`/staff/billing/payments/${p.paymentId}`} className="text-accent-ink hover:underline">
                    {p.method}
                  </Link>{' '}
                  <Amount money={p.amount} /> · {formatOrderWhen(p.recordedOn, order.branchTimeZoneId)}
                </li>
              ))}
            </ul>
          </Row>
        )}
        {order.creditNoteIds.length > 0 && <Row label="Credit notes">{order.creditNoteIds.length}</Row>}
      </dl>
    </Section>
  );
}

export function ShipmentPanel({ order, onEditTracking }: { order: StaffOrderDetail; onEditTracking: () => void }) {
  const s = order.shipment;
  if (!s) return null;
  return (
    <Section
      title="Shipment"
      actions={
        <Button variant="secondary" size="sm" onClick={onEditTracking}>
          {s.trackingNumber ? 'Change tracking number' : 'Enter tracking number'}
        </Button>
      }
    >
      <dl className="m-0 grid grid-cols-[minmax(0,9rem)_minmax(0,1fr)] gap-x-4 gap-y-1 text-sm">
        <Row label="Courier">{s.courierName}</Row>
        <Row label="Tracking number">
          {s.trackingNumber ? (
            safeHttpUrl(s.trackingUrl) ? (
              <a href={safeHttpUrl(s.trackingUrl) ?? undefined} target="_blank" rel="noopener noreferrer" className="font-semibold text-accent-ink hover:underline">
                {s.trackingNumber}
              </a>
            ) : (
              s.trackingNumber
            )
          ) : (
            'Not entered yet'
          )}
        </Row>
        <Row label="Dispatched">{formatOrderWhen(s.dispatchedOn, order.branchTimeZoneId)}</Row>
        {s.lastTrackingStatus && <Row label="Courier status">{s.lastTrackingStatus}</Row>}
      </dl>
    </Section>
  );
}

export function StaffHistoryPanel({ order }: { order: StaffOrderDetail }) {
  return (
    <Section title="History">
      <ol aria-label="Status history" className="m-0 flex list-none flex-col gap-2 p-0 text-sm">
        {order.statusHistory.map((entry, index) => (
          <li key={`${entry.changedOn}-${index}`} className="flex flex-wrap items-center gap-x-3 gap-y-1">
            <OrderStatusBadge status={entry.to} />
            <span>{formatOrderWhen(entry.changedOn, order.branchTimeZoneId)}</span>
            <span className="text-ink-muted">{entry.changedByName ?? 'The system'}</span>
            {entry.note && <span className="w-full text-ink [overflow-wrap:anywhere]">“{entry.note}”</span>}
          </li>
        ))}
      </ol>
    </Section>
  );
}
