import { apiFetch } from './client';
import type { PagedResult } from './types';
import type { QuantityValue } from '@/lib/format/units';

/**
 * `lib/api/formulary.ts` — HO-PHM-002 §8.2. Built to the REAL controllers on master
 * (`FormularyController`, `AdminFormularyController`) and `PharmacyFormularyViewModels.cs`, not only
 * HO-PHM-001 §12.4. Enum-shaped strings are the C# `Enumeration.DisplayName`s. Every quantity is
 * `{ amount, unit }` with the amount a string (UI-8).
 */

export type MedicineForm =
  | 'Tablet'
  | 'Capsule'
  | 'OralLiquid'
  | 'OralPaste'
  | 'Powder'
  | 'Injection'
  | 'Topical'
  | 'EyeDrops'
  | 'EarDrops'
  | 'Transdermal'
  | 'Other';
export type DosingBasis = 'PerKg' | 'Fixed';
export type InteractionSeverity = 'Minor' | 'Moderate' | 'Major' | 'Contraindicated';
export type AdministrationRoute =
  | 'Oral'
  | 'Subcutaneous'
  | 'Intramuscular'
  | 'Intravenous'
  | 'Topical'
  | 'Ophthalmic'
  | 'Otic'
  | 'Transdermal'
  | 'Inhaled'
  | 'Rectal';

export interface FormularyMedicineSummary {
  id: string;
  code: string;
  name: string;
  form: MedicineForm;
  strengthText: string;
  dosingBasis: DosingBasis;
  isControlled: boolean;
  isActive: boolean;
  /** "Has linked SKUs". */
  isStocked: boolean;
}

export interface FormularyInteraction {
  id: string;
  interactsWithMedicineId: string | null;
  interactsWithMedicineName: string | null;
  interactsWithTerm: string | null;
  severity: InteractionSeverity;
  note: string;
}

export interface FormularyLinkedSku {
  skuId: string;
  productName: string;
  variantLabel: string;
}

export interface FormularyMedicineDetail {
  id: string;
  code: string;
  name: string;
  activeIngredients: string[];
  drugClasses: string[];
  form: MedicineForm;
  strength: { amount: QuantityValue; per: QuantityValue; text: string };
  defaultRoute: AdministrationRoute;
  allowedRoutes: AdministrationRoute[];
  dosingBasis: DosingBasis;
  doseRange: { minMgPerKg: string; maxMgPerKg: string } | null;
  contraindications: string;
  interactions: FormularyInteraction[];
  /** `DispensingUnit` display name. */
  dispensingUnit: string;
  isControlled: boolean;
  controlledSchedule: string | null;
  isActive: boolean;
  linkedSkus: FormularyLinkedSku[];
  version: number;
}

export interface FormularySkuSearchResult {
  skuId: string;
  productName: string;
  variantLabel: string;
  barcode: string | null;
  isClinicOnly: boolean;
  /** Set when the SKU already belongs to a medicine — it cannot be linked to a second one. */
  linkedMedicineId: string | null;
}

/** `FormularyMedicineRequest` — create (with `code`) and update (with `expectedVersion`). */
export interface FormularyMedicineInput {
  code: string;
  name: string;
  activeIngredients: string[];
  drugClasses: string[];
  form: string;
  strength: { amount: number; unit: string; perQuantity: number; perUnit: string };
  defaultRoute: string;
  allowedRoutes: string[];
  dosingBasis: string;
  doseRange: { minMgPerKg: number; maxMgPerKg: number } | null;
  contraindications: string;
  interactions: Array<{
    interactsWithMedicineId: string | null;
    interactsWithTerm: string | null;
    severity: string;
    note: string;
  }>;
  dispensingUnit: string;
  linkedSkuIds: string[];
  expectedVersion: number;
}

export interface FormularyListParams {
  search?: string;
  isControlled?: boolean;
  isActive?: boolean;
  pageNumber?: number;
  pageSize?: number;
}

/** `?a=1&b=false` — booleans are kept when they are `false` (an explicit "inactive only" filter). */
export function queryString(
  params: Record<string, string | number | boolean | undefined | null>,
): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '') continue;
    search.set(key, String(value));
  }
  const text = search.toString();
  return text ? `?${text}` : '';
}

export function listFormulary(
  params: FormularyListParams,
): Promise<PagedResult<FormularyMedicineSummary>> {
  return apiFetch(`/api/staff/formulary${queryString({ ...params })}`);
}

export function getFormularyMedicine(medicineId: string): Promise<FormularyMedicineDetail> {
  return apiFetch(`/api/staff/formulary/${medicineId}`);
}

export function searchFormularySkus(term: string): Promise<FormularySkuSearchResult[]> {
  return apiFetch(`/api/admin/formulary/sku-search${queryString({ term })}`);
}

export function createFormularyMedicine(
  body: FormularyMedicineInput,
): Promise<{ medicineId: string }> {
  return apiFetch('/api/admin/formulary', { method: 'POST', body });
}

export function updateFormularyMedicine(
  medicineId: string,
  body: FormularyMedicineInput,
): Promise<void> {
  return apiFetch(`/api/admin/formulary/${medicineId}`, { method: 'PUT', body });
}

export function setMedicineControlled(
  medicineId: string,
  body: { isControlled: boolean; controlledSchedule?: string | null; reason?: string | null },
): Promise<void> {
  return apiFetch(`/api/admin/formulary/${medicineId}/controlled`, { method: 'PUT', body });
}

export function setMedicineActive(medicineId: string, isActive: boolean): Promise<void> {
  return apiFetch(`/api/admin/formulary/${medicineId}/active`, {
    method: 'PUT',
    body: { isActive },
  });
}
