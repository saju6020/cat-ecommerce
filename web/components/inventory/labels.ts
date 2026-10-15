import type { Storage, StorageCondition, StockLocationKind } from '@/lib/api/inventory-lookup';
import type { AlertKind } from '@/lib/api/inventory-alerts';
import type { BatchStatus } from '@/lib/api/inventory-stock';
import type {
  AdjustmentReason,
  AdjustmentStatus,
  CountScope,
  CountStatus,
} from '@/lib/api/inventory-control';
import type {
  ConsignmentStatus,
  CostComponentKind,
  ImportDocumentKind,
} from '@/lib/api/procurement-consignments';
import type {
  PurchaseOrderReceiptState,
  PurchaseOrderStatus,
} from '@/lib/api/procurement-orders';
import { trimDecimalString } from '@/lib/format/inventory';

/**
 * `components/inventory/labels.ts` — HO-IMP-002 §8.4/§11. Plain-language wording for the server's
 * enumeration names. Presentation only: it words what the server decided and decides nothing.
 */

function temperature(value: number | null): string {
  return value === null ? '' : trimDecimalString(String(value));
}

/** `"Refrigerated 2–8 °C"`, `"Room temperature, dry"`, `"Frozen"` — always text (US-IMP-16 AC1). */
export function describeStorage(storage: Storage | null | undefined): string | null {
  if (!storage) return null;
  const min = temperature(storage.minTemperatureC);
  const max = temperature(storage.maxTemperatureC);
  const range = min && max ? ` ${min}–${max} °C` : min ? ` from ${min} °C` : max ? ` up to ${max} °C` : '';
  switch (storage.condition) {
    case 'Ambient':
      return `Room temperature, dry${range}`;
    case 'CoolDry':
      return `Cool and dry${range}`;
    case 'Refrigerated':
      return `Refrigerated${range}`;
    case 'Frozen':
      return `Frozen${range}`;
    default:
      return `${storage.condition}${range}`;
  }
}

export const STORAGE_CONDITION_OPTIONS: ReadonlyArray<{ value: StorageCondition; label: string }> = [
  { value: 'Ambient', label: 'Room temperature, dry' },
  { value: 'CoolDry', label: 'Cool and dry' },
  { value: 'Refrigerated', label: 'Refrigerated' },
  { value: 'Frozen', label: 'Frozen' },
];

export const LOCATION_KIND_LABELS: Readonly<Record<StockLocationKind, string>> = {
  ShopFloor: 'Shop floor',
  Warehouse: 'Warehouse',
  ClinicDispensary: 'Clinic dispensary',
  BoardingStore: 'Boarding store',
  Quarantine: 'Quarantine',
};

export function locationKindLabel(kind: string): string {
  return LOCATION_KIND_LABELS[kind as StockLocationKind] ?? kind;
}

export const MOVEMENT_LABELS: Readonly<Record<string, string>> = {
  Receipt: 'receiving goods',
  TransferIn: 'transfers in',
  TransferOut: 'transfers out',
  AdjustmentIn: 'adjustments that add stock',
  AdjustmentOut: 'adjustments that remove stock',
  CountGain: 'count gains',
  CountLoss: 'count losses',
  Wastage: 'wastage',
  Sale: 'sales',
  Dispense: 'dispensing medicine',
  ClinicalUse: 'clinical use',
  BoardingUse: 'boarding use',
  Return: 'customer returns',
};

export function movementLabel(movement: string): string {
  return MOVEMENT_LABELS[movement] ?? movement;
}

export const STOCK_CLASS_OPTIONS = [
  { value: '', label: 'All classes' },
  { value: 'Food', label: 'Food' },
  { value: 'Medicine', label: 'Medicine' },
  { value: 'Consumable', label: 'Consumable' },
  { value: 'General', label: 'General' },
] as const;

export function stockClassLabel(value: string): string {
  return STOCK_CLASS_OPTIONS.find((option) => option.value === value)?.label ?? value;
}

export const BATCH_STATUS_LABELS: Readonly<Record<BatchStatus, string>> = {
  Available: 'Available',
  Quarantined: 'Quarantined',
  Recalled: 'Recalled — held',
  WrittenOff: 'Written off',
};

export const PO_STATUS_LABELS: Readonly<Record<PurchaseOrderStatus, string>> = {
  Draft: 'Draft',
  Issued: 'Issued',
  Closed: 'Closed',
  Cancelled: 'Cancelled',
};

export const PO_RECEIPT_STATE_LABELS: Readonly<Record<PurchaseOrderReceiptState, string>> = {
  NotReceived: 'Nothing received',
  PartiallyReceived: 'Partly received',
  FullyReceived: 'Fully received',
};

export const CONSIGNMENT_STATUS_LABELS: Readonly<Record<ConsignmentStatus, string>> = {
  Planned: 'Planned',
  InTransit: 'In transit',
  AtCustoms: 'At customs',
  Cleared: 'Cleared',
  Cancelled: 'Cancelled',
};

export const DOCUMENT_KIND_LABELS: Readonly<Record<ImportDocumentKind, string>> = {
  ImportPermit: 'Import permit',
  HealthCertificate: 'Health certificate',
  VeterinaryCertificate: 'Veterinary certificate',
  CustomsDeclaration: 'Customs declaration',
  BillOfLading: 'Bill of lading',
  CommercialInvoice: 'Commercial invoice',
  PackingList: 'Packing list',
  CertificateOfOrigin: 'Certificate of origin',
  Other: 'Other document',
};

export const COST_KIND_LABELS: Readonly<Record<CostComponentKind, string>> = {
  Freight: 'Freight',
  Duty: 'Duty',
  Clearing: 'Clearing',
  Insurance: 'Insurance',
  Other: 'Other',
};

export const ADJUSTMENT_STATUS_LABELS: Readonly<Record<AdjustmentStatus, string>> = {
  Posted: 'Posted',
  PendingApproval: 'Pending approval',
  Rejected: 'Rejected',
  Withdrawn: 'Withdrawn',
};

export const COUNT_STATUS_LABELS: Readonly<Record<CountStatus, string>> = {
  Open: 'Counting',
  Submitted: 'Submitted — review the variances',
  AwaitingApproval: 'Awaiting approval',
  Committed: 'Committed',
  Cancelled: 'Cancelled',
};

export const COUNT_SCOPE_LABELS: Readonly<Record<CountScope, string>> = {
  FullLocation: 'Whole location',
  SelectedSkus: 'Selected items only',
};

/** The sign each reason fixes (the server validates it — `InvalidAdjustmentDirection`). */
export const ADJUSTMENT_REASONS: ReadonlyArray<{
  value: AdjustmentReason;
  label: string;
  help: string;
  direction: 'removes' | 'adds' | 'either';
}> = [
  { value: 'Damage', label: 'Damaged', help: 'Stock was damaged and cannot be used.', direction: 'removes' },
  { value: 'Theft', label: 'Theft or loss', help: 'Stock has gone missing.', direction: 'removes' },
  { value: 'Sampling', label: 'Sampling', help: 'Units given away or used as samples.', direction: 'removes' },
  { value: 'InternalUse', label: 'Internal use', help: 'Used by the clinic or shop itself.', direction: 'removes' },
  { value: 'Expired', label: 'Expired', help: 'Written down because it passed its expiry date.', direction: 'removes' },
  { value: 'RecallDisposal', label: 'Recall disposal', help: 'Recalled stock being disposed of.', direction: 'removes' },
  { value: 'Found', label: 'Found', help: 'Stock found that the books did not show.', direction: 'adds' },
  { value: 'Correction', label: 'Correction', help: 'Fixing a mistake — can add or remove.', direction: 'either' },
];

export function adjustmentReasonLabel(reason: string): string {
  return ADJUSTMENT_REASONS.find((r) => r.value === reason)?.label ?? reason;
}

/** `"Expires in 60 days"`, `"Below reorder point"`, … (US-IMP-10 AC1, US-IMP-12 AC1). */
export function describeAlert(kind: AlertKind, thresholdDays: number | null): string {
  switch (kind) {
    case 'ExpiryThreshold':
      return thresholdDays === null
        ? 'Expiring soon'
        : `Expires in ${thresholdDays} ${thresholdDays === 1 ? 'day' : 'days'} or less`;
    case 'Expired':
      return 'Expired — cannot be sold, dispensed or fed';
    case 'LowStock':
      return 'Below reorder point';
    case 'Shortfall':
      return 'Shortfall recorded';
    case 'StorageBreach':
      return 'Storage breach';
    case 'LedgerMismatch':
      return 'Ledger mismatch';
    default:
      return kind;
  }
}

export const ALERT_KIND_OPTIONS: ReadonlyArray<{ value: AlertKind | ''; label: string }> = [
  { value: '', label: 'All kinds' },
  { value: 'ExpiryThreshold', label: 'Expiring soon' },
  { value: 'Expired', label: 'Expired' },
  { value: 'LowStock', label: 'Below reorder point' },
  { value: 'Shortfall', label: 'Shortfall' },
  { value: 'StorageBreach', label: 'Storage breach' },
  { value: 'LedgerMismatch', label: 'Ledger mismatch' },
];

/** The ledger's `kind` (a movement) in plain words. */
export const LEDGER_KIND_LABELS: Readonly<Record<string, string>> = {
  Receipt: 'Received',
  TransferOut: 'Transferred out',
  TransferIn: 'Transferred in',
  AdjustmentIn: 'Adjusted up',
  AdjustmentOut: 'Adjusted down',
  CountGain: 'Count gain',
  CountLoss: 'Count loss',
  Wastage: 'Written off',
  Sale: 'Sold',
  Dispense: 'Dispensed',
  ClinicalUse: 'Used in clinic',
  BoardingUse: 'Fed during boarding',
  Return: 'Returned',
};

export function ledgerKindLabel(kind: string): string {
  return LEDGER_KIND_LABELS[kind] ?? kind;
}

export const REFERENCE_KIND_LABELS: Readonly<Record<string, string>> = {
  GoodsReceipt: 'Goods receipt',
  StockTransfer: 'Transfer',
  StockAdjustment: 'Adjustment',
  StockCount: 'Stock count',
  BatchWriteOff: 'Batch write-off',
  StorageBreach: 'Storage breach',
  CustomerOrder: 'Customer order',
  PosSale: 'Counter sale',
  Prescription: 'Prescription',
  Visit: 'Clinic visit',
  Surgery: 'Surgery',
  BoardingStay: 'Boarding stay',
  CustomerReturn: 'Customer return',
  ControlledDrugCorrection: 'Controlled-drug correction',
};

export function referenceKindLabel(kind: string): string {
  return REFERENCE_KIND_LABELS[kind] ?? kind;
}

/** The recall trace's movement kinds in the words a pharmacist would use. */
export function traceKindLabel(kind: string): string {
  switch (kind) {
    case 'Sale':
      return 'Bought';
    case 'Dispense':
      return 'Dispensed';
    case 'BoardingUse':
      return 'Fed during boarding';
    case 'ClinicalUse':
      return 'Used in clinic';
    default:
      return ledgerKindLabel(kind);
  }
}

export const REJECTION_REASONS = [
  { value: 'Damaged', label: 'Damaged' },
  { value: 'Expired', label: 'Expired' },
  { value: 'ShortShelfLife', label: 'Short shelf life' },
  { value: 'WrongItem', label: 'Wrong item' },
  { value: 'TemperatureBreach', label: 'Temperature breach' },
  { value: 'FailedInspection', label: 'Failed inspection' },
  { value: 'Other', label: 'Other' },
] as const;

export function rejectionReasonLabel(reason: string | null): string {
  if (!reason) return '—';
  return REJECTION_REASONS.find((r) => r.value === reason)?.label ?? reason;
}
