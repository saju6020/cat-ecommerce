import { apiFetch, getApiUrl } from './client';
import { buildQuery } from './inventory-lookup';
import type { PagedResult } from './types';
import type { BatchRecall } from './inventory-stock';

/**
 * `lib/api/inventory-recalls.ts` — HO-IMP-002 §8.2, US-IMP-11. `StaffRecallsController`. A recall
 * applies to the batch at EVERY location and branch, so these calls are not scoped to the active
 * branch; every one needs `Recalls.Manage` (the trace holds customers' names and contact details).
 * The trace is never logged, never put in `sessionStorage`, never kept beyond the query cache.
 */

export type RecallStatus = 'Active' | 'Closed' | 'Withdrawn';

export interface RecallSummary {
  batchId: string;
  recallNumber: string;
  status: RecallStatus;
  skuId: string;
  skuCode: string;
  displayName: string;
  lotNumber: string;
  expiresOn: string | null;
  declaredOn: string;
  notifiedRecipientCount: number;
  notificationRuns: number;
  heldQuantity: string;
}

export interface RecallHeld {
  locationId: string;
  locationCode: string | null;
  branchId: string;
  onHand: string;
}

export interface RecallDetail {
  summary: RecallSummary;
  recall: BatchRecall;
  held: RecallHeld[];
}

export async function getRecalls(params: {
  status?: string;
  pageNumber?: number;
  pageSize?: number;
}): Promise<PagedResult<RecallSummary>> {
  return apiFetch(`/api/staff/inventory/recalls${buildQuery(params)}`);
}

export async function getRecall(batchId: string): Promise<RecallDetail> {
  return apiFetch(`/api/staff/inventory/recalls/${encodeURIComponent(batchId)}`);
}

export interface DeclareRecallRequest {
  confirmLotNumber: string;
  reason: string;
  customerAdvice: string;
}

export async function declareRecall(
  batchId: string,
  request: DeclareRecallRequest,
): Promise<{ recallNumber: string }> {
  return apiFetch(`/api/staff/inventory/batches/${encodeURIComponent(batchId)}/recall`, {
    method: 'POST',
    body: request,
  });
}

export interface RecallTraceCustomer {
  customerId: string;
  name: string;
  mobileNumber: string | null;
  email: string | null;
  isAnonymised: boolean;
  originalCustomerId: string | null;
}

export interface RecallTraceCat {
  catId: string;
  name: string;
  currentOwner: RecallTraceCustomer | null;
}

export interface RecallTraceReceived {
  occurredOn: string;
  kind: string;
  quantity: string;
  locationId: string;
  locationCode: string | null;
  referenceLabel: string | null;
  customer: RecallTraceCustomer | null;
  cat: RecallTraceCat | null;
  householdCats: string[];
}

export interface RecallTraceReturned {
  referenceKind: string;
  referenceId: string;
  referenceLabel: string | null;
  quantity: string;
  customer: RecallTraceCustomer | null;
}

export interface RecallTraceUnshipped {
  referenceKind: string;
  referenceId: string;
  quantity: string;
  customer: RecallTraceCustomer | null;
  cat: RecallTraceCat | null;
}

/** The whole trace comes in one response (not paged on the real API); the table pages client-side. */
export interface RecallTrace {
  batchId: string;
  recallNumber: string;
  skuCode: string;
  displayName: string;
  lotNumber: string;
  batchStatus: string;
  received: RecallTraceReceived[];
  returned: RecallTraceReturned[];
  unshippedDispatchBlocked: RecallTraceUnshipped[];
  unidentified: { count: number; quantity: string };
  stockHeld: RecallHeld[];
}

export async function getRecallTrace(batchId: string): Promise<RecallTrace> {
  return apiFetch(`/api/staff/inventory/recalls/${encodeURIComponent(batchId)}/trace`);
}

/** The server's CSV — audited as a business event there. A plain link. */
export function getRecallTraceCsvUrl(batchId: string): string {
  return getApiUrl(`/api/staff/inventory/recalls/${encodeURIComponent(batchId)}/trace.csv`);
}

export interface RecallNotificationRun {
  requested: number;
  alreadyNotified: number;
  remaining: number;
}

export async function notifyRecallRecipients(batchId: string): Promise<RecallNotificationRun> {
  return apiFetch(`/api/staff/inventory/recalls/${encodeURIComponent(batchId)}/notify`, { method: 'POST' });
}

export async function withdrawRecall(batchId: string, reason: string): Promise<void> {
  return apiFetch(`/api/staff/inventory/recalls/${encodeURIComponent(batchId)}/withdraw`, {
    method: 'POST',
    body: { reason },
  });
}

export async function closeRecall(batchId: string, note: string): Promise<void> {
  return apiFetch(`/api/staff/inventory/recalls/${encodeURIComponent(batchId)}/close`, {
    method: 'POST',
    body: { note },
  });
}
