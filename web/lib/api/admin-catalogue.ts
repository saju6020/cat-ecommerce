import { apiFetch } from './client';
import type { Money } from '../format/money';
import type { RoomKind } from './admin-branches';

/** `lib/api/admin-catalogue.ts` — HO-ADM-002 §8.2. Services, effective-dated prices, tax classes
 *  and effective-dated tax rates (HO-ADM-001 §8.6/§12.4, ADR-049). */

export type ServiceCategory =
  | 'Consultation'
  | 'Vaccination'
  | 'Spay'
  | 'Neuter'
  | 'Dental'
  | 'Grooming'
  | 'Emergency'
  | 'FollowUp';

export const SERVICE_CATEGORIES: ServiceCategory[] = [
  'Consultation',
  'Vaccination',
  'Spay',
  'Neuter',
  'Dental',
  'Grooming',
  'Emergency',
  'FollowUp',
];

export interface PublicService {
  id: string;
  name: string;
  /** `PublicServiceViewModel.Description` (ADM backlog A-APT1). */
  description: string;
  category: ServiceCategory;
  durationMinutes: number;
  isBookableOnline: boolean;
  currentPrice: Money;
}

export async function getPublicServices(): Promise<PublicService[]> {
  return apiFetch<PublicService[]>('/api/services');
}

export interface ClinicService {
  id: string;
  code: string;
  name: string;
  description: string;
  category: ServiceCategory;
  durationMinutes: number;
  qualifiedRoleIds: string[];
  requiredRoomKind: RoomKind;
  taxClassId: string;
  isBookableOnline: boolean;
  isActive: boolean;
  isPriced: boolean;
  currentPrice: Money | null;
}

export async function getAdminServices(): Promise<ClinicService[]> {
  return apiFetch<ClinicService[]>('/api/admin/services');
}

export async function getAdminService(serviceId: string): Promise<ClinicService> {
  return apiFetch<ClinicService>(`/api/admin/services/${encodeURIComponent(serviceId)}`);
}

export interface CreateClinicServiceRequest {
  code: string;
  name: string;
  description: string;
  category: ServiceCategory;
  durationMinutes: number;
  qualifiedRoleIds: string[];
  requiredRoomKind: RoomKind;
  taxClassId: string;
  isBookableOnline: boolean;
}

export async function createClinicService(
  request: CreateClinicServiceRequest,
): Promise<{ serviceId: string }> {
  return apiFetch<{ serviceId: string }>('/api/admin/services', { method: 'POST', body: request });
}

export type UpdateClinicServiceRequest = Omit<CreateClinicServiceRequest, 'code'>;

export async function updateClinicService(
  serviceId: string,
  request: UpdateClinicServiceRequest,
): Promise<void> {
  await apiFetch<void>(`/api/admin/services/${encodeURIComponent(serviceId)}`, {
    method: 'PUT',
    body: request,
  });
}

export async function setServiceActivation(serviceId: string, isActive: boolean): Promise<void> {
  await apiFetch<void>(`/api/admin/services/${encodeURIComponent(serviceId)}/activation`, {
    method: 'PUT',
    body: { isActive },
  });
}

export interface ServicePrice {
  id: string;
  serviceId: string;
  amount: Money;
  effectiveFrom: string; // instant
  setBy: string;
}

export async function getServicePrices(serviceId: string): Promise<ServicePrice[]> {
  return apiFetch<ServicePrice[]>(`/api/admin/services/${encodeURIComponent(serviceId)}/prices`);
}

export interface SetServicePriceRequest {
  amount: string;
  currencyCode: string;
  effectiveFrom: string; // instant, >= now
}

export async function setServicePrice(
  serviceId: string,
  request: SetServicePriceRequest,
): Promise<{ servicePriceId: string }> {
  return apiFetch<{ servicePriceId: string }>(
    `/api/admin/services/${encodeURIComponent(serviceId)}/prices`,
    { method: 'POST', body: request },
  );
}

export interface TaxClass {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
}

export async function getTaxClasses(): Promise<TaxClass[]> {
  return apiFetch<TaxClass[]>('/api/admin/tax-classes');
}

export async function getTaxClass(taxClassId: string): Promise<TaxClass> {
  return apiFetch<TaxClass>(`/api/admin/tax-classes/${encodeURIComponent(taxClassId)}`);
}

export interface CreateTaxClassRequest {
  code: string;
  name: string;
}

export async function createTaxClass(
  request: CreateTaxClassRequest,
): Promise<{ taxClassId: string }> {
  return apiFetch<{ taxClassId: string }>('/api/admin/tax-classes', {
    method: 'POST',
    body: request,
  });
}

export interface UpdateTaxClassRequest {
  name: string;
  isActive: boolean;
}

export async function updateTaxClass(
  taxClassId: string,
  request: UpdateTaxClassRequest,
): Promise<void> {
  await apiFetch<void>(`/api/admin/tax-classes/${encodeURIComponent(taxClassId)}`, {
    method: 'PUT',
    body: request,
  });
}

export interface TaxRate {
  id: string;
  taxClassId: string;
  ratePercent: string;
  effectiveFrom: string; // instant
}

export async function getTaxRates(taxClassId: string): Promise<TaxRate[]> {
  return apiFetch<TaxRate[]>(`/api/admin/tax-classes/${encodeURIComponent(taxClassId)}/rates`);
}

export interface SetTaxRateRequest {
  ratePercent: string;
  effectiveFrom: string;
}

export async function setTaxRate(
  taxClassId: string,
  request: SetTaxRateRequest,
): Promise<{ taxRateId: string }> {
  return apiFetch<{ taxRateId: string }>(
    `/api/admin/tax-classes/${encodeURIComponent(taxClassId)}/rates`,
    { method: 'POST', body: request },
  );
}
