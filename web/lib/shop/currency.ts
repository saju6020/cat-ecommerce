import type { SkuPriceHistory, StaffProductDetail } from '@/lib/api/staff-shop';

/**
 * Used only when the API has shown no price at all (a brand-new product's first SKU price).
 * Set `NEXT_PUBLIC_CURRENCY_CODE` for another currency (ADR-FE-036).
 */
export const FALLBACK_CURRENCY_CODE = process.env.NEXT_PUBLIC_CURRENCY_CODE || 'BDT';

/**
 * The currency a SKU's new price is entered in (S16): the one the API already uses for this SKU's
 * prices, else for any SKU of the product, else the configured fallback. Never a literal in a label.
 */
export function priceCurrencyCode(
  product: Pick<StaffProductDetail, 'skus'>,
  history?: SkuPriceHistory,
): string {
  const fromHistory = [history?.current, ...(history?.scheduled ?? []), ...(history?.past ?? [])]
    .map((entry) => entry?.amount.currencyCode)
    .find(Boolean);
  if (fromHistory) return fromHistory;
  for (const sku of product.skus) {
    const code = sku.currentPrice?.currencyCode ?? sku.scheduledPrice?.currencyCode;
    if (code) return code;
  }
  return FALLBACK_CURRENCY_CODE;
}
