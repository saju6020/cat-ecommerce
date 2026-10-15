import { apiFetch } from './client';
import { buildQuery } from './inventory-lookup';
import type { PagedResult } from './types';
import type { Money } from '@/lib/format/money';

/**
 * `lib/api/inventory-control.ts` — HO-IMP-002 §8.2, US-IMP-13/14. Transfers, adjustments and counts
 * (`StaffTransfersController`, `StaffAdjustmentsController`, `StaffCountsController`).
 */

// -------------------------------------------------------------------------------------------
// Transfers
// -------------------------------------------------------------------------------------------

export interface TransferSummary {
  id: string;
  number: string;
  fromLocationId: string;
  fromLocationCode: string | null;
  toLocationId: string;
  toLocationCode: string | null;
  transferredOn: string;
  transferredBy: string;
  lineCount: number;
  reason: string | null;
}

export interface TransferLine {
  lineId: string;
  skuId: string;
  skuCode: string;
  displayName: string;
  batchId: string;
  lotNumber: string;
  quantity: string;
  stockUnit: string;
}

export interface TransferDetail {
  id: string;
  number: string;
  branchId: string;
  fromLocationId: string;
  fromLocationCode: string | null;
  toLocationId: string;
  toLocationCode: string | null;
  reason: string | null;
  transferredOn: string;
  transferredBy: string;
  lines: TransferLine[];
}

export async function getTransfers(params: {
  pageNumber?: number;
  pageSize?: number;
}): Promise<PagedResult<TransferSummary>> {
  return apiFetch(`/api/staff/inventory/transfers${buildQuery(params)}`);
}

export async function getTransfer(id: string): Promise<TransferDetail> {
  return apiFetch(`/api/staff/inventory/transfers/${encodeURIComponent(id)}`);
}

export interface PostTransferRequest {
  fromLocationId: string;
  toLocationId: string;
  reason: string | null;
  lines: Array<{ skuId: string; batchId: string; quantity: string }>;
}

/** A transfer of stock that is no longer free is a `409 InsufficientStock`. */
export async function postTransfer(
  request: PostTransferRequest,
): Promise<{ transferId: string; number: string }> {
  return apiFetch('/api/staff/inventory/transfers', { method: 'POST', body: request });
}

// -------------------------------------------------------------------------------------------
// Adjustments
// -------------------------------------------------------------------------------------------

export type AdjustmentStatus = 'Posted' | 'PendingApproval' | 'Rejected' | 'Withdrawn';

export type AdjustmentReason =
  | 'Damage'
  | 'Theft'
  | 'Sampling'
  | 'InternalUse'
  | 'Expired'
  | 'RecallDisposal'
  | 'Found'
  | 'Correction';

export interface AdjustmentSummary {
  id: string;
  number: string;
  locationId: string;
  locationCode: string | null;
  reason: AdjustmentReason;
  status: AdjustmentStatus;
  valueBase: Money;
  requestedBy: string;
  requestedOn: string;
  lineCount: number;
}

export interface AdjustmentLine {
  lineId: string;
  skuId: string;
  skuCode: string;
  displayName: string;
  batchId: string;
  lotNumber: string;
  quantityDelta: string;
  stockUnit: string;
  unitCostBase: Money;
}

/** `allowedActions`: `approve`, `reject`, `withdraw` while pending, else empty. */
export interface AdjustmentDetail {
  id: string;
  number: string;
  branchId: string;
  locationId: string;
  locationCode: string | null;
  reason: AdjustmentReason;
  note: string;
  status: AdjustmentStatus;
  valueBase: Money;
  requestedBy: string;
  requestedOn: string;
  decidedBy: string | null;
  decidedOn: string | null;
  decisionNote: string | null;
  lines: AdjustmentLine[];
  allowedActions: string[];
}

export async function getAdjustments(params: {
  status?: string;
  pageNumber?: number;
  pageSize?: number;
}): Promise<PagedResult<AdjustmentSummary>> {
  return apiFetch(`/api/staff/inventory/adjustments${buildQuery(params)}`);
}

export async function getAdjustment(id: string): Promise<AdjustmentDetail> {
  return apiFetch(`/api/staff/inventory/adjustments/${encodeURIComponent(id)}`);
}

export interface RequestAdjustmentRequest {
  locationId: string;
  reason: AdjustmentReason;
  note: string;
  lines: Array<{ skuId: string; batchId: string; quantityDelta: string }>;
}

export interface RequestedAdjustment {
  adjustmentId: string;
  number: string;
  status: AdjustmentStatus;
  valueBase: Money;
  requiresApproval: boolean;
}

export async function requestAdjustment(
  request: RequestAdjustmentRequest,
): Promise<RequestedAdjustment> {
  return apiFetch('/api/staff/inventory/adjustments', { method: 'POST', body: request });
}

export async function approveAdjustment(id: string, note: string | null): Promise<void> {
  return apiFetch(`/api/staff/inventory/adjustments/${encodeURIComponent(id)}/approve`, {
    method: 'POST',
    body: { note },
  });
}

export async function rejectAdjustment(id: string, note: string): Promise<void> {
  return apiFetch(`/api/staff/inventory/adjustments/${encodeURIComponent(id)}/reject`, {
    method: 'POST',
    body: { note },
  });
}

export async function withdrawAdjustment(id: string): Promise<void> {
  return apiFetch(`/api/staff/inventory/adjustments/${encodeURIComponent(id)}/withdraw`, { method: 'POST' });
}

// -------------------------------------------------------------------------------------------
// Counts
// -------------------------------------------------------------------------------------------

export type CountStatus = 'Open' | 'Submitted' | 'AwaitingApproval' | 'Committed' | 'Cancelled';
export type CountScope = 'FullLocation' | 'SelectedSkus';

export interface CountSummary {
  id: string;
  number: string;
  locationId: string;
  locationCode: string | null;
  scope: CountScope;
  status: CountStatus;
  lineCount: number;
  varianceValueBase: Money | null;
  openedOn: string;
  openedBy: string;
}

/** `bookQuantity` and `variance` are `null` while the count is `Open` — counting is BLIND. */
export interface CountLine {
  skuId: string;
  skuCode: string;
  displayName: string;
  batchId: string;
  lotNumber: string;
  expiresOn: string | null;
  stockUnit: string;
  countedQuantity: string;
  bookQuantity: string | null;
  variance: string | null;
}

/** `allowedActions`: `record-lines`, `submit`, `cancel`, `commit`, `approve`, `reject`. */
export interface CountDetail {
  id: string;
  number: string;
  branchId: string;
  locationId: string;
  locationCode: string | null;
  scope: CountScope;
  skuIds: string[];
  status: CountStatus;
  openedOn: string;
  openedBy: string;
  submittedOn: string | null;
  varianceValueBase: Money | null;
  isBlind: boolean;
  lines: CountLine[];
  allowedActions: string[];
  version: number;
}

export interface CountVarianceLine {
  skuId: string;
  skuCode: string;
  displayName: string;
  batchId: string;
  lotNumber: string;
  stockUnit: string;
  bookQuantity: string;
  countedQuantity: string;
  variance: string;
  value: Money;
}

export interface CountVariance {
  countId: string;
  number: string;
  status: CountStatus;
  totalVarianceValueBase: Money;
  varianceLineCount: number;
  willNeedApproval: boolean;
  lines: CountVarianceLine[];
}

export async function getCounts(params: {
  status?: string;
  locationId?: string;
  pageNumber?: number;
  pageSize?: number;
}): Promise<PagedResult<CountSummary>> {
  return apiFetch(`/api/staff/inventory/counts${buildQuery(params)}`);
}

export async function openCount(request: {
  locationId: string;
  scope: CountScope;
  skuIds: string[];
}): Promise<{ countId: string; number: string }> {
  return apiFetch('/api/staff/inventory/counts', { method: 'POST', body: request });
}

export async function getCount(id: string): Promise<CountDetail> {
  return apiFetch(`/api/staff/inventory/counts/${encodeURIComponent(id)}`);
}

export async function recordCountLines(
  id: string,
  lines: Array<{ skuId: string; batchId: string; countedQuantity: string }>,
): Promise<void> {
  return apiFetch(`/api/staff/inventory/counts/${encodeURIComponent(id)}/lines`, { method: 'PUT', body: { lines } });
}

export async function submitCount(id: string): Promise<void> {
  return apiFetch(`/api/staff/inventory/counts/${encodeURIComponent(id)}/submit`, { method: 'POST' });
}

export async function getCountVariance(id: string): Promise<CountVariance> {
  return apiFetch(`/api/staff/inventory/counts/${encodeURIComponent(id)}/variance`);
}

export async function commitCount(id: string): Promise<{ status: CountStatus }> {
  return apiFetch(`/api/staff/inventory/counts/${encodeURIComponent(id)}/commit`, { method: 'POST' });
}

export async function cancelCount(id: string): Promise<void> {
  return apiFetch(`/api/staff/inventory/counts/${encodeURIComponent(id)}/cancel`, { method: 'POST' });
}

export async function approveCount(id: string): Promise<void> {
  return apiFetch(`/api/staff/inventory/counts/${encodeURIComponent(id)}/approve`, { method: 'POST' });
}

export async function rejectCount(id: string): Promise<void> {
  return apiFetch(`/api/staff/inventory/counts/${encodeURIComponent(id)}/reject`, { method: 'POST' });
}
