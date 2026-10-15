import type { SkuLookup, StockLocation } from '@/lib/api/inventory-lookup';
import type { PostGoodsReceiptRequest } from '@/lib/api/inventory-receipts';
import type { PurchaseOrderDetail } from '@/lib/api/procurement-orders';
import type { ReceiptDraft, ReceiptDraftLine } from '@/lib/drafts/receipt-draft';
import { quantityProblem, QUANTITY_PATTERN, UNIT_COST_PATTERN, RATE_PATTERN, isZeroDecimal } from '@/lib/validation/inventory';
import { FALLBACK_CURRENCY_CODE } from '@/lib/shop/currency';
import { describeStorage } from './labels';
import { newRowKey, type DistributedErrors } from './lineErrors';

/**
 * `components/inventory/receiptModel.ts` — HO-IMP-002 §11.3. The goods-receipt form's data model:
 * building a line from a SKU or a purchase-order line, choosing the default location (OQ-4),
 * validating every line locally (so a mistyped field is caught beside the pallet, not after a round
 * trip), and shaping the request. Quantities and costs stay STRINGS throughout (ADR-FE-041).
 */
export type ReceiptFormState = Omit<ReceiptDraft, 'savedAt'>;

export const EMPTY_RECEIPT: ReceiptFormState = {
  source: 'direct',
  purchaseOrderId: '',
  consignmentId: '',
  deliveryReference: '',
  lines: [],
};

/** OQ-4: the branch's warehouse when it can take a receipt, else the first location that can. */
export function defaultReceiptLocation(locations: StockLocation[] | undefined): string {
  const usable = (locations ?? []).filter((l) => l.isActive && l.permittedMovements.includes('Receipt'));
  return (usable.find((l) => l.kind === 'Warehouse') ?? usable[0])?.id ?? '';
}

export function lineFromSku(sku: SkuLookup, locationId: string): ReceiptDraftLine {
  return {
    key: newRowKey(),
    skuId: sku.skuId,
    skuLabel: `${sku.displayName} — ${sku.skuCode}`,
    stockUnit: sku.stockUnit,
    allowsFractional: sku.allowsFractionalQuantity,
    expiryRequired: sku.expiryRequired,
    storageLabel: describeStorage(sku.storage),
    purchaseOrderLineId: '',
    lotNumber: '',
    manufacturedOn: '',
    expiresOn: '',
    quantityAccepted: '',
    quantityRejected: '',
    rejectionReason: '',
    rejectionNote: '',
    locationId,
    holdForInspection: false,
    unitCostAmount: '',
    unitCostCurrency: FALLBACK_CURRENCY_CODE,
    exchangeRateToBase: '',
    costFromPurchaseOrder: false,
  };
}

/** Lines for what a purchase order still expects: outstanding quantities pre-filled, cost from the
 *  order and read-only there, the order's deliver-to location as the default. */
export function linesFromPurchaseOrder(
  order: PurchaseOrderDetail,
  skusByCode: Record<string, SkuLookup | null | undefined>,
): ReceiptDraftLine[] {
  return order.lines.flatMap((poLine) => {
    const sku = skusByCode[poLine.skuCode];
    if (!sku) return [];
    const base = lineFromSku(sku, order.deliverToLocationId);
    return [
      {
        ...base,
        purchaseOrderLineId: poLine.lineId,
        quantityAccepted: poLine.outstandingQuantity,
        unitCostAmount: poLine.unitCost.amount,
        unitCostCurrency: poLine.unitCost.currencyCode,
        costFromPurchaseOrder: true,
      },
    ];
  });
}

/** Local checks, mirroring what the server will check — it stays the authority. */
export function validateReceipt(state: ReceiptFormState): DistributedErrors {
  const found: DistributedErrors = { fields: {}, lines: {}, general: [], firstTarget: null };
  if (state.source === 'purchase-order' && !state.purchaseOrderId) {
    found.fields.purchaseOrderId = 'Choose the purchase order.';
    found.firstTarget ??= 'purchaseOrderId';
  }
  if (state.source === 'consignment' && !state.consignmentId) {
    found.fields.consignmentId = 'Choose the consignment.';
    found.firstTarget ??= 'consignmentId';
  }
  if (state.lines.length === 0) found.general.push('Add at least one line — scan a barcode or find an item.');
  state.lines.forEach((line, index) => {
    const errors: Record<string, string> = {};
    const flag = (field: string, message: string) => {
      errors[field] ??= message;
      found.firstTarget ??= `line-${index}-${field}`;
    };
    const accepted = line.quantityAccepted.trim();
    const rejected = line.quantityRejected.trim();
    const takes = accepted !== '' && !isZeroDecimal(accepted);
    const rejects = rejected !== '' && !isZeroDecimal(rejected);
    const options = { stockUnit: line.stockUnit, allowsFractional: line.allowsFractional };
    if (accepted === '' && rejected === '') flag('quantityAccepted', `Enter how many ${line.stockUnit} were accepted or rejected.`);
    if (accepted !== '') {
      const problem = quantityProblem(accepted, options);
      if (problem) flag('quantityAccepted', problem);
    }
    if (rejected !== '') {
      const problem = quantityProblem(rejected, options);
      if (problem) flag('quantityRejected', problem);
    }
    if (takes && line.lotNumber.trim() === '') flag('lotNumber', 'A lot number is required for accepted stock.');
    if (takes && line.expiryRequired && line.expiresOn === '') flag('expiresOn', 'Food and medicine must be received with an expiry date.');
    if (rejects && line.rejectionReason === '') flag('rejectionReason', 'Say why the stock was rejected.');
    if (line.locationId === '') flag('locationId', 'Choose where it is going.');
    if (takes && !line.costFromPurchaseOrder) {
      if (!UNIT_COST_PATTERN.test(line.unitCostAmount.trim())) flag('unitCostAmount', 'Enter the unit cost with at most 4 decimal places.');
      if (!/^[A-Za-z]{3}$/.test(line.unitCostCurrency.trim())) flag('unitCostCurrency', 'Use a three-letter currency code.');
      if (line.exchangeRateToBase.trim() !== '' && !RATE_PATTERN.test(line.exchangeRateToBase.trim())) flag('exchangeRateToBase', 'Enter the rate with at most 6 decimal places.');
    }
    if (takes && !QUANTITY_PATTERN.test(accepted) && !errors.quantityAccepted) flag('quantityAccepted', 'Enter a valid quantity.');
    if (Object.keys(errors).length > 0) found.lines[index] = errors;
  });
  return found;
}

const orNull = (v: string) => (v.trim() === '' ? null : v.trim());

export function toPostRequest(state: ReceiptFormState): PostGoodsReceiptRequest {
  return {
    supplierId: null,
    purchaseOrderId: state.source === 'purchase-order' ? orNull(state.purchaseOrderId) : null,
    consignmentId: state.source === 'consignment' ? orNull(state.consignmentId) : null,
    deliveryReference: orNull(state.deliveryReference),
    lines: state.lines.map((line) => {
      const rejects = line.quantityRejected.trim() !== '' && !isZeroDecimal(line.quantityRejected);
      const takes = line.quantityAccepted.trim() !== '' && !isZeroDecimal(line.quantityAccepted);
      const ownCost = takes && !line.costFromPurchaseOrder;
      return {
        skuId: line.skuId,
        purchaseOrderLineId: orNull(line.purchaseOrderLineId),
        lotNumber: line.lotNumber.trim(),
        manufacturedOn: orNull(line.manufacturedOn),
        expiresOn: orNull(line.expiresOn),
        quantityAccepted: line.quantityAccepted.trim() === '' ? '0' : line.quantityAccepted.trim(),
        quantityRejected: line.quantityRejected.trim() === '' ? '0' : line.quantityRejected.trim(),
        rejectionReason: rejects ? orNull(line.rejectionReason) : null,
        rejectionNote: rejects ? orNull(line.rejectionNote) : null,
        locationId: line.locationId,
        holdForInspection: line.holdForInspection,
        unitCostAmount: ownCost ? line.unitCostAmount.trim() : null,
        unitCostCurrency: ownCost ? line.unitCostCurrency.trim().toUpperCase() : null,
        exchangeRateToBase: ownCost ? orNull(line.exchangeRateToBase) : null,
      };
    }),
  };
}
