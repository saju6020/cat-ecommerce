import { apiFetch } from './client';
import type { BoardingAvailability, DaycareOffer, StaySearchParams, UnitTypeInfo } from './types';

/**
 * `lib/api/boarding-public.ts` — HO-BRD-002 §8.2: the two public Boarding reads (HO-BRD-001 §12.4).
 * Availability is `[PublicEndpoint]` with the `availability` rate limit. The server prices every
 * type for the dates and cat count — the browser never computes a price or a conflict (ADR-FE-105).
 */

export async function getBoardingAvailability(
  params: StaySearchParams,
): Promise<BoardingAvailability> {
  const query = new URLSearchParams({
    branchId: params.branchId,
    arrivalOn: params.arrivalOn,
    departureOn: params.departureOn,
    catCount: String(params.catCount),
  });
  return apiFetch<BoardingAvailability>(`/api/boarding/availability?${query.toString()}`);
}

export interface UnitTypesResponse {
  unitTypes: UnitTypeInfo[];
  /** Not in HO-BRD-001 §12.4 — optional; the daycare form degrades without it (HO-BRD-002 §1 Amended). */
  daycare: DaycareOffer | null;
}

/** `GET /api/boarding/unit-types` — "types with capacity and current nightly/weekly/monthly rates".
 *  §12.4 does not say whether the body is a bare array or an object, so both are read. */
export async function getBoardingUnitTypes(): Promise<UnitTypesResponse> {
  const body = await apiFetch<UnitTypeInfo[] | UnitTypesResponse>('/api/boarding/unit-types');
  if (Array.isArray(body)) return { unitTypes: body, daycare: null };
  return { unitTypes: body.unitTypes ?? [], daycare: body.daycare ?? null };
}
