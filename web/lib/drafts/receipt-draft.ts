/**
 * `lib/drafts/receipt-draft.ts` — HO-IMP-002 §8.3, ADR-FE-043. The goods-receipt form's draft,
 * kept in `sessionStorage` (tab-scoped — never `localStorage`) under
 * `imp.receipt-draft.{staffAccountId}`.
 *
 * What may be stored: SKU ids, lot numbers, dates, quantities, costs and location ids — nothing
 * personal. `sanitiseDraft` is the one gate every write goes through, and it copies ONLY the
 * allow-listed fields, so a future field added to the form cannot leak into storage by accident.
 */
export interface ReceiptDraftLine {
  key: string;
  skuId: string;
  skuLabel: string;
  stockUnit: string;
  allowsFractional: boolean;
  expiryRequired: boolean;
  storageLabel: string | null;
  purchaseOrderLineId: string;
  lotNumber: string;
  manufacturedOn: string;
  expiresOn: string;
  quantityAccepted: string;
  quantityRejected: string;
  rejectionReason: string;
  rejectionNote: string;
  locationId: string;
  holdForInspection: boolean;
  unitCostAmount: string;
  unitCostCurrency: string;
  exchangeRateToBase: string;
  costFromPurchaseOrder: boolean;
}

export interface ReceiptDraft {
  source: 'direct' | 'purchase-order' | 'consignment';
  purchaseOrderId: string;
  consignmentId: string;
  deliveryReference: string;
  lines: ReceiptDraftLine[];
  savedAt: string;
}

const KEY_PREFIX = 'imp.receipt-draft.';

export function receiptDraftKey(staffAccountId: string): string {
  return `${KEY_PREFIX}${staffAccountId}`;
}

function storage(): Storage | null {
  try {
    return typeof window === 'undefined' ? null : window.sessionStorage;
  } catch {
    return null;
  }
}

function str(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

function sanitiseLine(line: ReceiptDraftLine): ReceiptDraftLine {
  return {
    key: str(line.key),
    skuId: str(line.skuId),
    skuLabel: str(line.skuLabel),
    stockUnit: str(line.stockUnit),
    allowsFractional: line.allowsFractional === true,
    expiryRequired: line.expiryRequired === true,
    storageLabel: typeof line.storageLabel === 'string' ? line.storageLabel : null,
    purchaseOrderLineId: str(line.purchaseOrderLineId),
    lotNumber: str(line.lotNumber),
    manufacturedOn: str(line.manufacturedOn),
    expiresOn: str(line.expiresOn),
    quantityAccepted: str(line.quantityAccepted),
    quantityRejected: str(line.quantityRejected),
    rejectionReason: str(line.rejectionReason),
    rejectionNote: str(line.rejectionNote),
    locationId: str(line.locationId),
    holdForInspection: line.holdForInspection === true,
    unitCostAmount: str(line.unitCostAmount),
    unitCostCurrency: str(line.unitCostCurrency),
    exchangeRateToBase: str(line.exchangeRateToBase),
    costFromPurchaseOrder: line.costFromPurchaseOrder === true,
  };
}

export function sanitiseDraft(draft: ReceiptDraft): ReceiptDraft {
  const source =
    draft.source === 'purchase-order' || draft.source === 'consignment' ? draft.source : 'direct';
  return {
    source,
    purchaseOrderId: str(draft.purchaseOrderId),
    consignmentId: str(draft.consignmentId),
    deliveryReference: str(draft.deliveryReference),
    lines: Array.isArray(draft.lines) ? draft.lines.map(sanitiseLine) : [],
    savedAt: str(draft.savedAt),
  };
}

export function saveReceiptDraft(staffAccountId: string, draft: ReceiptDraft): boolean {
  const store = storage();
  if (!store) return false;
  try {
    store.setItem(receiptDraftKey(staffAccountId), JSON.stringify(sanitiseDraft(draft)));
    return true;
  } catch {
    return false; // quota / private mode — the draft is a convenience, never a requirement
  }
}

export function loadReceiptDraft(staffAccountId: string): ReceiptDraft | null {
  const store = storage();
  if (!store) return null;
  try {
    const raw = store.getItem(receiptDraftKey(staffAccountId));
    if (!raw) return null;
    const parsed = JSON.parse(raw) as ReceiptDraft;
    return parsed && Array.isArray(parsed.lines) ? sanitiseDraft(parsed) : null;
  } catch {
    return null;
  }
}

export function clearReceiptDraft(staffAccountId: string): void {
  try {
    storage()?.removeItem(receiptDraftKey(staffAccountId));
  } catch {
    // best effort
  }
}
