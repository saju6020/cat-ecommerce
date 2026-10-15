import { apiFetch } from './client';
import { queryString } from './formulary';

/**
 * `lib/api/diet-authorisations.ts` — HO-PHM-002 §8.2. Built to `StaffDietAuthorisationsController`
 * and `PharmacyDietViewModels.cs`. Dates are `yyyy-MM-dd`; `status` is the EFFECTIVE one (an
 * authorisation whose last day has passed reads `Expired` whether or not the sweep has run).
 */
export type DietAuthorisationStatus = 'Active' | 'Expired' | 'Revoked';

export interface DietAuthorisation {
  id: string;
  catId: string;
  productId: string;
  productName: string;
  validFrom: string;
  validTo: string;
  status: DietAuthorisationStatus;
  authorisedBy: string;
  visitId: string;
  notes: string | null;
}

export interface DietProduct {
  productId: string;
  name: string;
  brand: string;
}

export interface AuthoriseDietInput {
  visitId: string;
  productId: string;
  validFrom: string;
  validTo: string;
  notes?: string | null;
}

const staff = '/api/staff';

export function searchDietProducts(search: string): Promise<DietProduct[]> {
  return apiFetch(`${staff}/diet-products${queryString({ search })}`);
}

export function authoriseDiet(
  catId: string,
  body: AuthoriseDietInput,
): Promise<{ authorisationId: string }> {
  return apiFetch(`${staff}/cats/${catId}/diet-authorisations`, { method: 'POST', body });
}

export function listCatDietAuthorisations(
  catId: string,
  scope: 'active' | 'all',
): Promise<DietAuthorisation[]> {
  return apiFetch(`${staff}/cats/${catId}/diet-authorisations${queryString({ scope })}`);
}

export function extendDietAuthorisation(id: string, validTo: string): Promise<void> {
  return apiFetch(`${staff}/diet-authorisations/${id}/extend`, {
    method: 'POST',
    body: { validTo },
  });
}

export function revokeDietAuthorisation(id: string, reason: string): Promise<void> {
  return apiFetch(`${staff}/diet-authorisations/${id}/revoke`, {
    method: 'POST',
    body: { reason },
  });
}
