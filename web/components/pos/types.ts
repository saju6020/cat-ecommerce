import type { Money } from '@/lib/format/money';
import type { PosSku, TenderMethod } from '@/lib/api/pos';

/**
 * `components/pos/types.ts` — HO-ORD-002 §8.3. The point-of-sale basket lives in the `/staff/pos`
 * component's own state — never in storage (ADR-FE-095). A basket line is what the scan returned plus
 * the cashier's choices (quantity, cat); every price on screen is the server's quote.
 */
export interface BasketLine {
  skuId: string;
  productName: string;
  variantLabel: string;
  /** The shelf price the lookup returned — shown beside the line; the quote's figures are the ones that count. */
  unitPrice: Money | null;
  quantity: number;
  requiresPrescription: boolean;
  catId: string | null;
  /** The batch the server says will be used (first-expiry-first-out) — `fefoBatches[0]`. */
  batchLabel: string | null;
}

export interface PosCustomer {
  name: string;
  mobile: string;
  /** Set when an existing customer was attached (needed for a prescription line). */
  customerId: string | null;
}

export interface PosTender {
  id: string;
  method: TenderMethod;
  /** As typed — never parsed or added up here. */
  amount: string;
  reference: string;
}

export function basketLineFromSku(sku: PosSku): BasketLine {
  const batch = sku.fefoBatches[0];
  return {
    skuId: sku.skuId,
    productName: sku.productName,
    variantLabel: sku.variantLabel,
    unitPrice: sku.unitPrice,
    quantity: 1,
    requiresPrescription: sku.requiresPrescription,
    catId: null,
    batchLabel: batch ? `batch ${batch.lotNumber}${batch.expiresOn ? `, expires ${batch.expiresOn}` : ''}` : null,
  };
}
