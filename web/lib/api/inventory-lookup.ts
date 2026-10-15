import { apiFetch } from './client';
import type { PagedResult } from './types';

/**
 * `lib/api/inventory-lookup.ts` — HO-IMP-002 §8.2. SKU lookup (the SKU picker's source, OQ-1),
 * stock locations and storage profiles. Built to the REAL controllers on master
 * (`StaffSkuLookupController`, `StaffInventoryLocationsController`, `StaffStorageProfilesController`),
 * not only HO-IMP-001 §12.4. The branch rides in `X-Branch-Id` (client.ts), never in a path.
 */

export type { PagedResult };

/** Builds `?a=1&b=2` from defined, non-empty values (booleans only when true). */
export function buildQuery(
  params: Record<string, string | number | boolean | undefined | null>,
): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '' || value === false) continue;
    search.set(key, String(value));
  }
  const query = search.toString();
  return query ? `?${query}` : '';
}

export type StorageCondition = 'Ambient' | 'CoolDry' | 'Refrigerated' | 'Frozen';
export type StockClass = 'Food' | 'Medicine' | 'Consumable' | 'General';

/** `StorageViewModel` — temperatures are JSON numbers on the wire (C# `decimal?`). */
export interface Storage {
  condition: StorageCondition;
  minTemperatureC: number | null;
  maxTemperatureC: number | null;
  handlingNotes: string | null;
}

export interface SkuLookup {
  skuId: string;
  productId: string;
  skuCode: string;
  displayName: string;
  barcode: string | null;
  stockClass: StockClass;
  expiryRequired: boolean;
  stockUnit: string;
  allowsFractionalQuantity: boolean;
  countryOfOriginCode: string | null;
  isActive: boolean;
  storage: Storage | null;
}

/** At most 20 rows (server cap, not paged). Exactly one of `search` / `barcode` is needed. */
export async function lookupSkus(params: {
  search?: string;
  barcode?: string;
}): Promise<SkuLookup[]> {
  return apiFetch<SkuLookup[]>(`/api/staff/inventory/skus${buildQuery(params)}`);
}

export type StockLocationKind =
  'ShopFloor' | 'Warehouse' | 'ClinicDispensary' | 'BoardingStore' | 'Quarantine';

export interface StockLocation {
  id: string;
  code: string;
  name: string;
  kind: StockLocationKind;
  isActive: boolean;
  /** `MovementKind` names this kind of location accepts (`StockLocationRules`). */
  permittedMovements: string[];
}

/** The branch's locations — a plain array, not paged. */
export async function getLocations(): Promise<StockLocation[]> {
  return apiFetch<StockLocation[]>('/api/staff/inventory/locations');
}

export interface CreateLocationRequest {
  code: string;
  name: string;
  kind: StockLocationKind;
}

export async function createLocation(
  request: CreateLocationRequest,
): Promise<{ locationId: string }> {
  return apiFetch('/api/staff/inventory/locations', { method: 'POST', body: request });
}

export async function updateLocation(id: string, request: { name: string }): Promise<void> {
  return apiFetch(`/api/staff/inventory/locations/${encodeURIComponent(id)}`, { method: 'PUT', body: request });
}

export async function setLocationActivation(id: string, isActive: boolean): Promise<void> {
  return apiFetch(`/api/staff/inventory/locations/${encodeURIComponent(id)}/activation`, {
    method: 'PUT',
    body: { isActive },
  });
}

export interface StorageProfile {
  productId: string;
  storage: Storage;
  version: number;
}

/** `404` when the product has no profile yet. */
export async function getStorageProfile(productId: string): Promise<StorageProfile> {
  return apiFetch<StorageProfile>(`/api/staff/inventory/storage-profiles/${encodeURIComponent(productId)}`);
}

/** Temperatures are decimal STRINGS here (the server reads them as decimals). */
export interface SetStorageProfileRequest {
  condition: StorageCondition;
  minTemperatureC: string | null;
  maxTemperatureC: string | null;
  handlingNotes: string | null;
}

export async function setStorageProfile(
  productId: string,
  request: SetStorageProfileRequest,
): Promise<void> {
  return apiFetch(`/api/staff/inventory/storage-profiles/${encodeURIComponent(productId)}`, {
    method: 'PUT',
    body: request,
  });
}
