import { apiFetch } from './client';
import { buildQuery } from './inventory-lookup';
import type { PagedResult } from './types';

/**
 * `lib/api/inventory-breaches.ts` — HO-IMP-002 §8.2, US-IMP-16 AC2. `StaffStorageBreachesController`.
 * Anyone who handles stock may REPORT a breach; quarantining and closing are the manager's.
 * `observedOn` must not be in the future (server rule) and is sent as an ISO instant with offset.
 */

export type BreachStatus = 'Open' | 'Quarantined' | 'Closed';

export interface BreachSummary {
  id: string;
  locationId: string;
  locationCode: string | null;
  observedOn: string;
  condition: string;
  status: BreachStatus;
  affectedBatchCount: number;
  reportedBy: string;
  reportedOn: string;
}

export interface BreachAffectedBatch {
  batchId: string;
  skuCode: string | null;
  displayName: string | null;
  lotNumber: string | null;
  heldAtLocation: string;
}

/** `allowedActions`: `quarantine`, `close`. */
export interface BreachDetail {
  summary: BreachSummary;
  temperatureC: number | null;
  durationMinutes: number | null;
  affectedBatches: BreachAffectedBatch[];
  quarantinedOn: string | null;
  quarantinedBy: string | null;
  closedOn: string | null;
  closedBy: string | null;
  closingNote: string | null;
  allowedActions: string[];
}

export async function getBreaches(params: {
  status?: string;
  pageNumber?: number;
  pageSize?: number;
}): Promise<PagedResult<BreachSummary>> {
  return apiFetch(`/api/staff/inventory/storage-breaches${buildQuery(params)}`);
}

export async function getBreach(id: string): Promise<BreachDetail> {
  return apiFetch(`/api/staff/inventory/storage-breaches/${encodeURIComponent(id)}`);
}

export interface ReportBreachRequest {
  locationId: string;
  /** ISO-8601 with offset. */
  observedOn: string;
  condition: string;
  temperatureC: string | null;
  durationMinutes: number | null;
}

export async function reportBreach(
  request: ReportBreachRequest,
): Promise<{ breachId: string; affectedBatchCount: number }> {
  return apiFetch('/api/staff/inventory/storage-breaches', { method: 'POST', body: request });
}

export async function quarantineBreachStock(id: string): Promise<void> {
  return apiFetch(`/api/staff/inventory/storage-breaches/${encodeURIComponent(id)}/quarantine`, { method: 'POST' });
}

export async function closeBreach(id: string, note: string): Promise<void> {
  return apiFetch(`/api/staff/inventory/storage-breaches/${encodeURIComponent(id)}/close`, {
    method: 'POST',
    body: { note },
  });
}
