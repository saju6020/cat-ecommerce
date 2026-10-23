import { Amount } from '@/components/billing/Amount';
import type { OrderTotals } from '@/lib/api/checkout';
import { summaryCardClass } from '@/components/hearth/card';
import { serviceLevelLabel } from '@/lib/format/orders';
import { cn } from '@/lib/utils';

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
  appearance = 'plain',
  headingId,
  footer,
  className,
  bare = false,
}: {
  totals: OrderTotals;
  discountCode?: string | null;
  serviceLevel?: string | null;
  fulfilment?: string;
  heading?: string;
  totalLabel?: string;
  /**
   * `hearth` (customer screens only) is the neutral card of docs/design/orders: a white card, a
   * Display-font title, dashed row rules and a bold 28px total. Still the same six server rows.
   */
  appearance?: 'plain' | 'hearth';
  headingId?: string;
  /** Rendered inside the card under the total (a step's Continue block, discount field...). */
  footer?: React.ReactNode;
  className?: string;
  /** `hearth` only: the rows without the card around them (the order detail puts them in its items card). */
  bare?: boolean;
}) {
  const hearth = appearance === 'hearth';
  const Heading = hearth ? 'h2' : 'h3';
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
    <section
      aria-label={heading}
      className={cn(
        'min-w-0',
        hearth && (bare ? 'flex flex-col gap-2' : cn(summaryCardClass, 'flex flex-col gap-4')),
        className,
      )}
    >
      <Heading
        id={headingId}
        className={cn(
          'font-display font-semibold text-ink',
          hearth ? 'm-0 text-2xl' : 'mb-2 text-base',
          bare && 'sr-only',
        )}
      >
        {heading}
      </Heading>
      <dl className="m-0">
        {rows.map(([label, value]) => (
          <div
            key={label}
            className={cn(
              'flex items-baseline justify-between gap-3 border-b border-dashed border-border text-sm',
              hearth ? 'py-[9px]' : 'py-1.5',
            )}
          >
            <dt className="text-ink-muted">{label}</dt>
            <dd className="m-0 whitespace-nowrap">
              <Amount money={value} />
            </dd>
          </div>
        ))}
        <div
          className={cn(
            'flex items-baseline justify-between gap-3 font-bold',
            hearth ? 'pt-3.5' : 'mt-1 border-t border-ink pt-2.5 text-[15.5px]',
          )}
        >
          <dt className={cn('text-ink', hearth && 'text-base')}>{totalLabel}</dt>
          <dd className={cn('m-0 whitespace-nowrap', hearth && 'text-[28px]')}>
            <Amount money={totals.grandTotal} />
          </dd>
        </div>
      </dl>
      {footer}
    </section>
  );
}
