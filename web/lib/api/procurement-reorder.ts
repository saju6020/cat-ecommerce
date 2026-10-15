import { apiFetch } from './client';
import { buildQuery } from './inventory-lookup';
import type { PagedResult } from './types';

/**
 * `lib/api/procurement-reorder.ts` — HO-IMP-002 §8.2, US-IMP-12. `StaffReorderPoliciesController`.
 * Suggestions are NOT paged on the real API — they are `{ items: [...] }`.
 */

export interface ReorderSuggestion {
  skuId: string;
  skuCode: string;
  displayName: string;
  stockUnit: string;
  locationId: string;
  locationCode: string | null;
  usable: string;
  onOrder: string;
  reorderPoint: string;
  isDefaultedPoint: boolean;
  averageDailyConsumption: string;
  leadTimeDays: number;
  coverDays: number;
  suggestedQuantity: string;
  preferredSupplierId: string | null;
  preferredSupplierName: string | null;
}

export async function getReorderSuggestions(params: {
  locationId?: string;
  supplierId?: string;
}): Promise<{ items: ReorderSuggestion[] }> {
  return apiFetch(`/api/staff/procurement/reorder-suggestions${buildQuery(params)}`);
}

export interface ReorderPolicy {
  skuId: string;
  skuCode: string;
  displayName: string;
  locationId: string;
  locationCode: string | null;
  reorderPoint: string;
  isDefaulted: boolean;
  reorderQuantity: string | null;
  preferredSupplierId: string | null;
  preferredSupplierName: string | null;
}

export async function getReorderPolicies(params: {
  locationId?: string;
  pageNumber?: number;
  pageSize?: number;
}): Promise<PagedResult<ReorderPolicy>> {
  return apiFetch(`/api/staff/inventory/reorder-policies${buildQuery(params)}`);
}

export interface SetReorderPolicyRequest {
  reorderPoint: string;
  reorderQuantity: string | null;
  preferredSupplierId: string | null;
}

export async function setReorderPolicy(
  skuId: string,
  locationId: string,
  request: SetReorderPolicyRequest,
): Promise<void> {
  return apiFetch(`/api/staff/inventory/reorder-policies/${encodeURIComponent(skuId)}/${encodeURIComponent(locationId)}`, {
    method: 'PUT',
    body: request,
  });
}
