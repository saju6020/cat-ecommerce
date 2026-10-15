import { apiFetch } from './client';
import { buildQuery } from './inventory-lookup';
import type { PagedResult } from './types';
import type { Money } from '@/lib/format/money';

/** `lib/api/procurement-orders.ts` — HO-IMP-002 §8.2, US-IMP-02. `ProcurementPurchaseOrdersController`
 *  (the ACTIVE branch's orders; another branch's order is a `404`). */

export type PurchaseOrderStatus = 'Draft' | 'Issued' | 'Closed' | 'Cancelled';
export type PurchaseOrderReceiptState = 'NotReceived' | 'PartiallyReceived' | 'FullyReceived';

export interface PurchaseOrderSummary {
  id: string;
  number: string;
  supplierId: string;
  supplierName: string;
  status: PurchaseOrderStatus;
  receiptState: PurchaseOrderReceiptState;
  expectedDeliveryOn: string;
  totalAmount: Money;
}

export interface PurchaseOrderLine {
  lineId: string;
  skuId: string;
  skuCode: string;
  displayName: string;
  stockUnit: string;
  orderedQuantity: string;
  acceptedQuantity: string;
  rejectedQuantity: string;
  outstandingQuantity: string;
  unitCost: Money;
  lineTotal: Money;
}

export interface PurchaseOrderReceiptRef {
  goodsReceiptId: string;
  number: string;
  receivedOn: string;
}

/** `allowedActions`: `edit`, `issue`, `cancel`, `receive`, `close` (server-decided). */
export interface PurchaseOrderDetail {
  id: string;
  number: string;
  supplierId: string;
  supplierName: string;
  branchId: string;
  deliverToLocationId: string;
  currencyCode: string;
  expectedDeliveryOn: string;
  notes: string | null;
  status: PurchaseOrderStatus;
  statusReason: string | null;
  receiptState: PurchaseOrderReceiptState;
  issuedOn: string | null;
  totalAmount: Money;
  lines: PurchaseOrderLine[];
  receipts: PurchaseOrderReceiptRef[];
  allowedActions: string[];
  version: number;
}

export interface PurchaseOrderListParams {
  status?: string;
  supplierId?: string;
  receiptState?: string;
  from?: string;
  to?: string;
  pageNumber?: number;
  pageSize?: number;
}

export async function getPurchaseOrders(
  params: PurchaseOrderListParams,
): Promise<PagedResult<PurchaseOrderSummary>> {
  return apiFetch(`/api/staff/procurement/purchase-orders${buildQuery({ ...params })}`);
}

export async function getPurchaseOrder(id: string): Promise<PurchaseOrderDetail> {
  return apiFetch(`/api/staff/procurement/purchase-orders/${encodeURIComponent(id)}`);
}

export interface PurchaseOrderRequest {
  supplierId: string;
  deliverToLocationId: string;
  currencyCode: string | null;
  expectedDeliveryOn: string;
  notes: string | null;
  lines: Array<{ skuId: string; orderedQuantity: string; unitCostAmount: string }>;
}

export async function createPurchaseOrder(
  request: PurchaseOrderRequest,
): Promise<{ purchaseOrderId: string; number: string }> {
  return apiFetch('/api/staff/procurement/purchase-orders', { method: 'POST', body: request });
}

export async function replacePurchaseOrderDraft(
  id: string,
  request: PurchaseOrderRequest,
): Promise<void> {
  return apiFetch(`/api/staff/procurement/purchase-orders/${encodeURIComponent(id)}`, { method: 'PUT', body: request });
}

export async function issuePurchaseOrder(id: string): Promise<void> {
  return apiFetch(`/api/staff/procurement/purchase-orders/${encodeURIComponent(id)}/issue`, { method: 'POST' });
}

export async function cancelPurchaseOrder(id: string, reason: string): Promise<void> {
  return apiFetch(`/api/staff/procurement/purchase-orders/${encodeURIComponent(id)}/cancel`, {
    method: 'POST',
    body: { reason },
  });
}

export async function closePurchaseOrder(id: string, reason: string): Promise<void> {
  return apiFetch(`/api/staff/procurement/purchase-orders/${encodeURIComponent(id)}/close`, {
    method: 'POST',
    body: { reason },
  });
}
