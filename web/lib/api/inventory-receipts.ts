import { apiFetch } from './client';
import { buildQuery, type Storage } from './inventory-lookup';
import type { PagedResult } from './types';
import type { Money } from '@/lib/format/money';

/**
 * `lib/api/inventory-receipts.ts` — HO-IMP-002 §8.2, US-IMP-04. `StaffGoodsReceiptsController`.
 * A receipt is posted in ONE command and is immutable afterwards. A `400` carries EVERY line
 * violation, named `Lines[i].Field`.
 */

export interface GoodsReceiptSummary {
  id: string;
  number: string;
  receivedOn: string;
  supplierId: string | null;
  supplierName: string | null;
  purchaseOrderId: string | null;
  purchaseOrderNumber: string | null;
  consignmentId: string | null;
  deliveryReference: string | null;
  lineCount: number;
  batchCount: number;
  receivedBy: string;
}

export interface GoodsReceiptLine {
  lineId: string;
  skuId: string;
  skuCode: string;
  displayName: string;
  stockUnit: string;
  lotNumber: string;
  manufacturedOn: string | null;
  expiresOn: string | null;
  quantityAccepted: string;
  quantityRejected: string;
  rejectionReason: string | null;
  rejectionNote: string | null;
  locationId: string;
  locationCode: string | null;
  holdForInspection: boolean;
  /** `null` for a caller without `InventoryReports.View`. */
  unitCost: Money | null;
  batchId: string | null;
  batchStatus: string | null;
  storage: Storage | null;
}

export interface GoodsReceiptDetail {
  id: string;
  number: string;
  branchId: string;
  receivedOn: string;
  receivedBy: string;
  supplierId: string | null;
  supplierName: string | null;
  purchaseOrderId: string | null;
  purchaseOrderNumber: string | null;
  consignmentId: string | null;
  consignmentReference: string | null;
  deliveryReference: string | null;
  lines: GoodsReceiptLine[];
}

export interface GoodsReceiptListParams {
  from?: string;
  to?: string;
  supplierId?: string;
  purchaseOrderId?: string;
  pageNumber?: number;
  pageSize?: number;
}

export async function getGoodsReceipts(
  params: GoodsReceiptListParams,
): Promise<PagedResult<GoodsReceiptSummary>> {
  return apiFetch(`/api/staff/inventory/goods-receipts${buildQuery({ ...params })}`);
}

export async function getGoodsReceipt(id: string): Promise<GoodsReceiptDetail> {
  return apiFetch(`/api/staff/inventory/goods-receipts/${encodeURIComponent(id)}`);
}

export interface PostGoodsReceiptLine {
  skuId: string;
  purchaseOrderLineId: string | null;
  lotNumber: string;
  manufacturedOn: string | null;
  expiresOn: string | null;
  quantityAccepted: string;
  quantityRejected: string;
  rejectionReason: string | null;
  rejectionNote: string | null;
  locationId: string;
  holdForInspection: boolean;
  unitCostAmount: string | null;
  unitCostCurrency: string | null;
  exchangeRateToBase: string | null;
}

export interface PostGoodsReceiptRequest {
  supplierId: string | null;
  purchaseOrderId: string | null;
  consignmentId: string | null;
  deliveryReference: string | null;
  lines: PostGoodsReceiptLine[];
}

export async function postGoodsReceipt(
  request: PostGoodsReceiptRequest,
): Promise<{ goodsReceiptId: string; number: string; batchIds: string[] }> {
  return apiFetch('/api/staff/inventory/goods-receipts', { method: 'POST', body: request });
}
