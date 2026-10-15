import { apiFetch } from './client';
import { buildQuery, type Storage } from './inventory-lookup';
import type { PagedResult } from './types';
import type { Money } from '@/lib/format/money';

/**
 * `lib/api/inventory-stock.ts` — HO-IMP-002 §8.2, US-IMP-06/07/08/09/15. `StaffStockController`,
 * `StaffBatchesController`, `StaffLedgerController`. Every figure is the server's (ADR-FE-040);
 * quantities are strings with `stockUnit` beside them.
 */

export interface StockSummary {
  skuId: string;
  skuCode: string;
  displayName: string;
  stockUnit: string;
  stockClass: string;
  onHand: string;
  reserved: string;
  usable: string;
  sellable: string;
  expiredQuantity: string;
  heldQuantity: string;
  nextExpiresOn: string | null;
  storage: Storage | null;
}

export interface StockListParams {
  search?: string;
  locationId?: string;
  class?: string;
  belowReorderPoint?: boolean;
  pageNumber?: number;
  pageSize?: number;
}

export async function getStockSummary(params: StockListParams): Promise<PagedResult<StockSummary>> {
  return apiFetch(`/api/staff/inventory/stock${buildQuery({ ...params })}`);
}

export interface StockLocationQuantity {
  locationId: string;
  locationCode: string;
  locationKind: string;
  onHand: string;
  reserved: string;
}

export interface StockMatrixBatch {
  batchId: string;
  lotNumber: string;
  expiresOn: string | null;
  isExpired: boolean;
  daysToExpiry: number | null;
  status: BatchStatus;
  locations: StockLocationQuantity[];
}

export interface SkuStockMatrix {
  skuId: string;
  skuCode: string;
  displayName: string;
  stockUnit: string;
  storage: Storage | null;
  branchTimeZoneId: string;
  batches: StockMatrixBatch[];
}

export async function getSkuStockMatrix(skuId: string): Promise<SkuStockMatrix> {
  return apiFetch(`/api/staff/inventory/stock/${encodeURIComponent(skuId)}`);
}

export interface FefoPreviewLine {
  batchId: string;
  lotNumber: string;
  expiresOn: string | null;
  locationId: string;
  locationCode: string;
  quantity: string;
}

export interface FefoPreview {
  lines: FefoPreviewLine[];
  shortfall: string;
  stockUnit: string;
}

export type FefoPurpose = 'Sale' | 'Dispense' | 'ClinicalUse' | 'BoardingUse';

/** A refusal (unknown SKU, a purpose the location does not permit) is a `400`. */
export async function getFefoPreview(params: {
  skuId: string;
  quantity: string;
  purpose: FefoPurpose;
  locationId?: string;
}): Promise<FefoPreview> {
  return apiFetch(`/api/staff/inventory/fefo-preview${buildQuery({ ...params })}`);
}

export type BatchStatus = 'Available' | 'Quarantined' | 'Recalled' | 'WrittenOff';

export interface BatchSummary {
  id: string;
  skuId: string;
  skuCode: string;
  displayName: string;
  lotNumber: string;
  expiresOn: string | null;
  isExpired: boolean;
  daysToExpiry: number | null;
  branchTimeZoneId: string;
  status: BatchStatus;
  receivedQuantity: string;
  onHand: string;
  stockUnit: string;
  consignmentId: string | null;
}

export interface BatchListParams {
  search?: string;
  skuId?: string;
  status?: string;
  expiringWithinDays?: number;
  consignmentId?: string;
  pageNumber?: number;
  pageSize?: number;
}

export async function getBatches(params: BatchListParams): Promise<PagedResult<BatchSummary>> {
  return apiFetch(`/api/staff/inventory/batches${buildQuery({ ...params })}`);
}

export interface BatchPosition {
  locationId: string;
  locationCode: string;
  locationKind: string;
  onHand: string;
  reserved: string;
}

export interface BatchStatusChange {
  from: string;
  to: string;
  reason: string | null;
  changedOn: string;
  changedBy: string;
}

export interface BatchRecall {
  recallNumber: string;
  reason: string;
  customerAdvice: string;
  declaredOn: string;
  declaredBy: string;
  notifiedOn: string | null;
  notifiedBy: string | null;
  notifiedRecipientCount: number;
  notificationRuns: number;
  closedOn: string | null;
  closingNote: string | null;
}

/** `purchaseUnitCost`, `unitCostBase` and `costBasis` are `null` for a caller without
 *  `InventoryReports.View` (the real API returns the batch, with the cost fields null). */
export interface BatchDetail {
  id: string;
  skuId: string;
  skuCode: string;
  displayName: string;
  stockUnit: string;
  stockClass: string;
  branchId: string;
  lotNumber: string;
  manufacturedOn: string | null;
  expiresOn: string | null;
  isExpired: boolean;
  daysToExpiry: number | null;
  branchTimeZoneId: string;
  status: BatchStatus;
  statusReason: string | null;
  receivedOn: string;
  receivedQuantity: string;
  goodsReceiptId: string;
  goodsReceiptNumber: string | null;
  consignmentId: string | null;
  consignmentReference: string | null;
  supplierId: string | null;
  originCountryCode: string | null;
  purchaseUnitCost: Money | null;
  unitCostBase: Money | null;
  costBasis: string | null;
  positions: BatchPosition[];
  recall: BatchRecall | null;
  statusHistory: BatchStatusChange[];
  /** `quarantine`, `release`, `recall`, `withdraw-recall`, `write-off`. */
  allowedActions: string[];
  storage: Storage | null;
  version: number;
}

export async function getBatch(id: string): Promise<BatchDetail> {
  return apiFetch(`/api/staff/inventory/batches/${encodeURIComponent(id)}`);
}

export async function quarantineBatch(id: string, reason: string): Promise<void> {
  return apiFetch(`/api/staff/inventory/batches/${encodeURIComponent(id)}/quarantine`, {
    method: 'POST',
    body: { reason },
  });
}

export async function releaseBatch(id: string): Promise<void> {
  return apiFetch(`/api/staff/inventory/batches/${encodeURIComponent(id)}/release`, { method: 'POST' });
}

/** Refused (`StockLocationNotFound`) unless the caller is assigned to EVERY branch holding the batch. */
export async function writeOffBatch(id: string, reason: string): Promise<void> {
  return apiFetch(`/api/staff/inventory/batches/${encodeURIComponent(id)}/write-off`, {
    method: 'POST',
    body: { reason },
  });
}

export interface LedgerEntry {
  sequence: number;
  occurredOn: string;
  kind: string;
  quantityDelta: string;
  stockUnit: string;
  locationId: string;
  locationCode: string | null;
  batchId: string;
  lotNumber: string | null;
  skuId: string;
  skuCode: string | null;
  referenceKind: string;
  referenceId: string;
  referenceLabel: string | null;
  fefoOverridden: boolean;
  overrideReason: string | null;
  performedBy: string;
  performedByName: string | null;
  note: string | null;
}

export interface LedgerParams {
  from: string;
  to: string;
  skuId?: string;
  batchId?: string;
  locationId?: string;
  kind?: string;
  referenceKind?: string;
  referenceId?: string;
  pageNumber?: number;
  pageSize?: number;
}

export async function searchLedger(params: LedgerParams): Promise<PagedResult<LedgerEntry>> {
  return apiFetch(`/api/staff/inventory/ledger${buildQuery({ ...params })}`);
}

/** The server's CSV (injection-guarded, ≤ 50 000 rows, `413` beyond). A plain link — never
 *  generated client-side. Needs `InventoryReports.View`. */
export function getLedgerExportPath(params: Omit<LedgerParams, 'pageNumber' | 'pageSize'>): string {
  return `/api/staff/inventory/ledger/export.csv${buildQuery({ ...params })}`;
}
