import { Amount } from '@/components/billing/Amount';
import type { OrderTotals } from '@/lib/api/checkout';
import { serviceLevelLabel } from '@/lib/format/orders';

/**
 * `components/orders/OrderSummary.tsx` — ⭐ HO-ORD-002 §11.4, US-ORD-04. One definition list, every
 * component on its own row, **always all of them** (zero rows show `BDT 0.00`, never hidden):
 * *Items subtotal*, *Item discounts*, *Order discount ({code})*, *Delivery ({level})*, *Tax*, then
 * **Total**. Every value is a server string rendered by `Amount`/`formatMoney`; nothing here adds,
 * subtracts or compares (ADR-FE-092). Reused verbatim by checkout, the order details and the POS.
 */
export function OrderSummary({
  totals,
  discountCode,
  serviceLevel,
  fulfilment,
  heading = 'Order summary',
  totalLabel = 'Total',
}: {
  totals: OrderTotals;
  discountCode?: string | null;
  serviceLevel?: string | null;
  fulfilment?: string;
  heading?: string;
  totalLabel?: string;
}) {
  const deliveryLabel =
    fulfilment === 'Pickup' || fulfilment === 'Counter'
      ? 'Delivery'
      : serviceLevel
        ? `Delivery (${serviceLevelLabel(serviceLevel).replace(' delivery', '')})`
        : 'Delivery';
  const rows: Array<[string, OrderTotals['subtotal']]> = [
    ['Items subtotal', totals.subtotal],
    ['Item discounts', totals.lineDiscountTotal],
    [discountCode ? `Order discount (${discountCode})` : 'Order discount', totals.orderDiscount],
    [deliveryLabel, totals.deliveryCharge],
    ['Tax', totals.taxTotal],
  ];
  return (
    <section aria-label={heading} className="min-w-0">
      <h3 className="mb-2 font-display text-base font-semibold text-ink">{heading}</h3>
      <dl className="m-0">
        {rows.map(([label, value]) => (
          <div
            key={label}
            className="flex items-baseline justify-between gap-3 border-b border-dashed border-border py-1.5 text-sm"
          >
            <dt className="text-ink-muted">{label}</dt>
            <dd className="m-0 whitespace-nowrap">
              <Amount money={value} />
            </dd>
          </div>
        ))}
        <div className="mt-1 flex items-baseline justify-between gap-3 border-t border-ink pt-2.5 text-[15.5px] font-bold">
          <dt className="text-ink">{totalLabel}</dt>
          <dd className="m-0 whitespace-nowrap">
            <Amount money={totals.grandTotal} />
          </dd>
        </div>
      </dl>
    </section>
  );
}
