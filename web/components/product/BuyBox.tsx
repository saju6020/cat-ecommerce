import * as React from 'react';
import { Info } from 'lucide-react';
import { formatMoney } from '@/lib/format/money';
import { formatBestBefore, taxLabel } from '@/lib/format/shop';
import { AvailabilityText } from './StatusText';
import type { DetailSku } from '@/lib/api/shop-catalogue';

export interface BuyBoxProps {
  sku: DetailSku;
  pricesIncludeTax: boolean;
  /** `PrescriptionGate`, shown when the product needs a vet authorisation. */
  gate?: React.ReactNode;
  wishlist?: React.ReactNode;
  stockAlert?: React.ReactNode;
  /** The action slot — EMPTY in this slice. HO-ORD-002 fills it with *Add to cart* and the
   *  `PrescriptionCatPicker` (this handover §11.3); no cart exists here. */
  actions?: React.ReactNode;
}

/**
 * HO-SHP-002 §11.3 `BuyBox` — everything that decides whether a variant can be bought, for the
 * selected SKU, ALL from API fields (ADR-FE-032): price with the tax label, availability, the
 * best-before notice (`expiryNotice`, US-SHP-07 AC2), the prescription gate, wishlist and
 * back-in-stock controls. No stock number, no arithmetic.
 */
export function BuyBox({
  sku,
  pricesIncludeTax,
  gate,
  wishlist,
  stockAlert,
  actions,
}: BuyBoxProps) {
  return (
    <section
      aria-label="Price and availability"
      className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      <p className="text-2xl font-bold text-ink">
        {sku.price ? formatMoney(sku.price) : 'Price not available'}{' '}
        {sku.price && (
          <span className="text-sm font-normal text-ink-muted">{taxLabel(pricesIncludeTax)}</span>
        )}
      </p>
      <p>
        <AvailabilityText availability={sku.availability} />
      </p>
      {sku.expiryNotice && (
        <p className="flex items-start gap-2 rounded-[var(--radius-sm)] bg-surface-sunken p-3 text-sm text-ink">
          <Info size={16} className="mt-0.5 flex-none" aria-hidden="true" />
          <span>
            Best before {formatBestBefore(sku.expiryNotice.expiresOn)} — this is the date on the
            stock we would send you.
          </span>
        </p>
      )}
      {gate}
      <div className="flex flex-col gap-2">
        {wishlist}
        {stockAlert}
      </div>
      <div data-slot="buy-box-actions">{actions}</div>
    </section>
  );
}
