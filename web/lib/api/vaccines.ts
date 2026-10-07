import { apiFetch } from './client';
import type { PagedResult } from './cats';

/**
 * Vaccine catalogue — HO-CAT-001 §12.3 Q6, §12.4 `VaccinesController`. US-CAT-05 depends on this
 * (HO-CAT-002 §3.2's "Plus the vaccine catalogue screens (list, register)").
 */

/** Q6 — GET /api/vaccines. */
export interface VaccineListItem {
  id: string;
  code: string;
  name: string;
  isCore: boolean;
  validityMonths: number;
  isActive: boolean;
}

export interface GetVaccinesParams {
  activeOnly?: boolean;
  coreOnly?: boolean;
  pageNumber?: number;
  pageSize?: number;
}

// See lib/api/cats.ts's PagedResult doc comment — GET /api/vaccines returns the same
// { items, pageNumber, pageSize, totalCount } envelope as GET /api/cats (owner decision
// 2026-09-26); no more pageSize+1 trim.
export async function getVaccines(
  params: GetVaccinesParams = {},
): Promise<PagedResult<VaccineListItem>> {
  const pageNumber = params.pageNumber ?? 1;
  const pageSize = params.pageSize ?? 20;
  const query = new URLSearchParams();
  if (params.activeOnly) query.set('activeOnly', 'true');
  if (params.coreOnly) query.set('coreOnly', 'true');
  query.set('pageNumber', String(pageNumber));
  query.set('pageSize', String(pageSize));

  return apiFetch<PagedResult<VaccineListItem>>(`/api/vaccines?${query.toString()}`);
}

export interface RegisterVaccineRequest {
  code: string;
  name: string;
  isCore: boolean;
  validityMonths: number;
  description: string | null;
}

export async function registerVaccine(
  request: RegisterVaccineRequest,
): Promise<{ vaccineId: string }> {
  return apiFetch<{ vaccineId: string }>('/api/vaccines', { method: 'POST', body: request });
}
