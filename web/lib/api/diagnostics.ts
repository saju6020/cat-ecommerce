import { apiFetch } from './client';
import { resolveStoredFile, type StoredFileViewModel } from './files';

/**
 * `lib/api/diagnostics.ts` — HO-EMR-002 §8.2 (US-CLN-14). Typed against `StaffDiagnosticsController`
 * and `DiagnosticViewModels.cs` on master. Numbers are strings at the stored precision (UI-8);
 * every result value carries its flag and the range it was judged against.
 */

export type DiagnosticKind = 'BloodWork' | 'Urinalysis' | 'FelvFivTest' | 'Imaging' | 'Other';
export type DiagnosticOrderStatus = 'Pending' | 'Resulted' | 'Cancelled';
export type ResultValueKind = 'Numeric' | 'Qualitative';
export type ResultFlag = 'Normal' | 'Low' | 'High' | 'Abnormal' | 'NotAssessed';

export interface DiagnosticResultValue {
  id: string;
  name: string;
  valueKind: ResultValueKind;
  numericValue: string | null;
  textValue: string | null;
  unit: string | null;
  referenceLow: string | null;
  referenceHigh: string | null;
  normalText: string | null;
  flag: ResultFlag;
}

export interface DiagnosticResultSet {
  id: string;
  reportedOn: string;
  note: string | null;
  supersedesResultSetId: string | null;
  recordedBy: string;
  recordedOn: string;
  values: DiagnosticResultValue[];
}

export interface DiagnosticAttachment {
  id: string;
  resultDate: string;
  note: string | null;
  attachedBy: string;
  attachedOn: string;
  file: StoredFileViewModel | null;
}

export interface DiagnosticOrder {
  id: string;
  version: number;
  visitId: string;
  catId: string;
  catName: string;
  kind: DiagnosticKind;
  description: string | null;
  status: DiagnosticOrderStatus;
  orderedOn: string;
  orderedBy: string;
  cancelledReason: string | null;
  branchTimeZoneId: string;
  requestedAnalytes: Array<{ analyteId: string; code: string; name: string }>;
  resultSets: DiagnosticResultSet[];
  attachments: DiagnosticAttachment[];
}

export interface PendingDiagnosticItem {
  id: string;
  visitId: string;
  catId: string;
  catName: string;
  kind: DiagnosticKind;
  status: DiagnosticOrderStatus;
  orderedOn: string;
  orderedBy: string;
}

export interface OrderDiagnosticRequest {
  kind: DiagnosticKind;
  description?: string;
  analyteIds: string[];
}

export interface ResultValueRequest {
  analyteId?: string;
  name?: string;
  valueKind: ResultValueKind;
  numericValue?: number;
  textValue?: string;
  unit?: string;
  referenceLow?: number;
  referenceHigh?: number;
  normalText?: string;
}

export interface RecordResultsRequest {
  reportedOn: string;
  values: ResultValueRequest[];
  note?: string;
  supersedesResultSetId?: string;
}

/** The server's judged flag per value (the POST response — numbers as JSON numbers). */
export interface RecordedResultValue {
  valueId: string;
  name: string;
  valueKind: ResultValueKind;
  numericValue: number | null;
  textValue: string | null;
  unit: string | null;
  referenceLow: number | null;
  referenceHigh: number | null;
  normalText: string | null;
  flag: ResultFlag;
}

const base = '/api/staff';

export function orderDiagnostic(
  visitId: string,
  request: OrderDiagnosticRequest,
): Promise<{ diagnosticOrderId: string }> {
  return apiFetch(`${base}/visits/${visitId}/diagnostics`, { method: 'POST', body: request });
}

export async function getDiagnosticOrder(orderId: string): Promise<DiagnosticOrder> {
  const order = await apiFetch<DiagnosticOrder>(`${base}/diagnostics/${orderId}`);
  // Attachment `file.url` is signed relative to the API origin -- anchor it (FIL backlog 25).
  return {
    ...order,
    attachments: order.attachments.map((a) => ({ ...a, file: resolveStoredFile(a.file) })),
  };
}

export function getPendingDiagnostics(): Promise<PendingDiagnosticItem[]> {
  return apiFetch(`${base}/diagnostics?status=Pending`);
}

export function recordResults(
  orderId: string,
  request: RecordResultsRequest,
): Promise<{ resultSetId: string; values: RecordedResultValue[] }> {
  return apiFetch(`${base}/diagnostics/${orderId}/results`, { method: 'POST', body: request });
}

export function attachDiagnosticFile(
  orderId: string,
  request: { fileId: string; resultDate: string; note?: string },
): Promise<{ attachmentId: string }> {
  return apiFetch(`${base}/diagnostics/${orderId}/attachments`, { method: 'POST', body: request });
}

export function cancelDiagnosticOrder(orderId: string, reason: string): Promise<void> {
  return apiFetch(`${base}/diagnostics/${orderId}/cancel`, { method: 'POST', body: { reason } });
}
