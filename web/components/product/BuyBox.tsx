import * as React from 'react';
import { Clock } from 'lucide-react';
import { DeliveryPromiseCard } from '@/components/hearth/DeliveryPromise';
import { formatMoney } from '@/lib/format/money';
import { formatBestBefore, taxLabel } from '@/lib/format/shop';
import { AvailabilityText } from './StatusText';
import type { DetailSku } from '@/lib/api/shop-catalogue';

export interface BuyBoxProps {
  sku: DetailSku;
  pricesIncludeTax: boolean;
  /** The size option cards, drawn between the price and the buy row. */
  variants?: React.ReactNode;
  /** `PrescriptionGate`, shown when the product needs a vet authorisation. */
  gate?: React.ReactNode;
  /** Only used with a custom `actions` slot; the default buy row carries its own wishlist button. */
  wishlist?: React.ReactNode;
  stockAlert?: React.ReactNode;
  /** The action slot: the quantity stepper, *Add to cart* and wishlist row (or HO-ORD-002's own). */
  actions?: React.ReactNode;
}

/**
 * HO-SHP-002 §11.3 `BuyBox` — everything that decides whether a variant can be bought, for the
 * selected SKU, ALL from API fields (ADR-FE-032): price with the tax label and availability (the
 * "Price and availability" region), the size cards, the best-before notice (`expiryNotice`,
 * US-SHP-07 AC2), the prescription gate, the buy row, the back-in-stock card and the delivery
 * promise. No stock number, no arithmetic.
 */
export function BuyBox({
  sku,
  pricesIncludeTax,
  variants,
  gate,
  wishlist,
  stockAlert,
  actions,
}: BuyBoxProps) {
  return (
    <>
      <section aria-label="Price and availability" className="flex flex-col gap-2">
        <p className="m-0 flex flex-wrap items-baseline gap-x-3 gap-y-1">
          <strong className="text-4xl font-bold tracking-tight text-ink">
            {sku.price ? formatMoney(sku.price) : 'Price not available'}
          </strong>
          {sku.price && (
            <span className="text-sm font-normal text-ink-muted">
              {taxLabel(pricesIncludeTax)} · {sku.label}
            </span>
          )}
        </p>
        <p className="m-0">
          <AvailabilityText availability={sku.availability} />
        </p>
      </section>
      {variants}
      {sku.expiryNotice && (
        <p className="m-0 flex items-start gap-2.5 rounded-[18px] bg-warning-bg px-4 py-3 text-[13.5px] leading-snug text-warning-ink">
          <Clock size={16} className="mt-0.5 flex-none" aria-hidden="true" />
          <span>
            Best before {formatBestBefore(sku.expiryNotice.expiresOn)} — this is the date on the
            stock we would send you.
          </span>
        </p>
      )}
      {gate}
      <div data-slot="buy-box-actions">{actions}</div>
      {wishlist && <div className="flex flex-col gap-2">{wishlist}</div>}
      {stockAlert && (
        <div id="stock-alert" className="scroll-mt-24 rounded-card bg-surface-sunken p-4">
          {stockAlert}
        </div>
      )}
      <DeliveryPromiseCard />
    </>
  );
}
