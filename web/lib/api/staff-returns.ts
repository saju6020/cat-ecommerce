import { apiFetch } from './client';
import type { PagedResult } from './types';
import type { ItemCondition, ReturnLineInput, ReturnReason } from './my-orders';

/**
 * `lib/api/staff-returns.ts` — HO-ORD-002 §8.2. Returns for staff, as `StaffReturnsController` sends
 * them. **There is no stock-decision call here**: restock / write-off left Ordering (HO-ORD-001 gate fix
 * 3, OQ-19) — a received return stays `Received` and its goods sit in the branch's quarantine location,
 * handled on Inventory's screen (HO-ORD-002 §1 Amended, 2026-10-11).
 */

export type ReturnStatus = 'Requested' | 'Approved' | 'Rejected' | 'Received' | 'Closed';

export interface StaffReturnRow {
  id: string;
  orderId: string;
  orderNumber: string;
  customerName: string;
  kind: 'CustomerReturn' | 'DeliveryFailure';
  status: ReturnStatus;
  requestedOn: string;
  lineCount: number;
  quantity: number;
}

export interface StaffReturnLine {
  orderLineId: string;
  productName: string;
  variantLabel: string;
  quantity: number;
  reason: ReturnReason;
  condition: ItemCondition;
  customerNote: string | null;
  requiresDefectConfirmation: boolean;
  defectConfirmed: boolean;
  defectNote: string | null;
  quantityReceived: number;
  anyBatchRecalled: boolean;
  batchNumbers: string[];
}

export interface StaffReturnDetail {
  id: string;
  version: number;
  orderId: string;
  orderNumber: string;
  customerName: string;
  kind: 'CustomerReturn' | 'DeliveryFailure';
  status: ReturnStatus;
  requestedOn: string;
  requestedBy: string;
  decidedOn: string | null;
  decidedBy: string | null;
  decisionNote: string | null;
  receivedOn: string | null;
  lines: StaffReturnLine[];
  branchTimeZoneId: string;
}

export function listStaffReturns(filters: {
  status?: string;
  pageNumber?: number;
  pageSize?: number;
}): Promise<PagedResult<StaffReturnRow>> {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(filters)) {
    if (value !== undefined && value !== '') search.set(key, String(value));
  }
  const text = search.toString();
  return apiFetch(`/api/staff/returns${text ? `?${text}` : ''}`);
}

export function getStaffReturn(returnRequestId: string): Promise<StaffReturnDetail> {
  return apiFetch(`/api/staff/returns/${encodeURIComponent(returnRequestId)}`);
}

export function requestReturnForCustomer(
  orderId: string,
  lines: ReturnLineInput[],
): Promise<{ returnRequestId: string; status: string }> {
  return apiFetch(`/api/staff/orders/${encodeURIComponent(orderId)}/returns`, {
    method: 'POST',
    body: { lines },
  });
}

export interface ApproveReturnInput {
  note?: string;
  lines: Array<{ orderLineId: string; defectConfirmed: boolean; defectNote?: string }>;
}

export function approveReturn(returnRequestId: string, input: ApproveReturnInput): Promise<void> {
  return apiFetch(`/api/staff/returns/${encodeURIComponent(returnRequestId)}/approve`, {
    method: 'POST',
    body: input,
  });
}

export function rejectReturn(returnRequestId: string, note: string): Promise<void> {
  return apiFetch(`/api/staff/returns/${encodeURIComponent(returnRequestId)}/reject`, {
    method: 'POST',
    body: { note },
  });
}

export function receiveReturn(
  returnRequestId: string,
  lines: Array<{ orderLineId: string; quantityReceived: number }>,
): Promise<void> {
  return apiFetch(`/api/staff/returns/${encodeURIComponent(returnRequestId)}/receive`, {
    method: 'POST',
    body: { lines },
  });
}
