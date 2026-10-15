import { apiFetch } from './client';
import { buildQuery } from './inventory-lookup';
import type { PagedResult } from './types';

/** `lib/api/procurement-suppliers.ts` — HO-IMP-002 §8.2, US-IMP-01. `ProcurementSuppliersController`
 *  (suppliers are company-wide, not branch-scoped). */

export interface SupplierSummary {
  id: string;
  code: string;
  name: string;
  countryCode: string;
  currencyCode: string;
  leadTimeDays: number;
  paymentTerms: string;
  isActive: boolean;
}

export interface SupplierDetail {
  id: string;
  code: string;
  name: string;
  countryCode: string;
  contactName: string | null;
  phone: string | null;
  email: string | null;
  address: string | null;
  currencyCode: string;
  leadTimeDays: number;
  paymentTerms: string;
  paymentTermDays: number | null;
  notes: string | null;
  isActive: boolean;
  version: number;
}

export interface SupplierListParams {
  search?: string;
  countryCode?: string;
  isActive?: string;
  pageNumber?: number;
  pageSize?: number;
}

export async function getSuppliers(
  params: SupplierListParams,
): Promise<PagedResult<SupplierSummary>> {
  return apiFetch(`/api/staff/procurement/suppliers${buildQuery({ ...params })}`);
}

export async function getSupplier(id: string): Promise<SupplierDetail> {
  return apiFetch(`/api/staff/procurement/suppliers/${encodeURIComponent(id)}`);
}

export interface SupplierInput {
  name: string;
  countryCode: string;
  contactName: string | null;
  phone: string | null;
  email: string | null;
  address: string | null;
  currencyCode: string;
  leadTimeDays: number;
  paymentTerms: string;
  paymentTermDays: number | null;
  notes: string | null;
}

export interface CreateSupplierRequest extends SupplierInput {
  code: string;
}

export async function createSupplier(
  request: CreateSupplierRequest,
): Promise<{ supplierId: string }> {
  return apiFetch('/api/staff/procurement/suppliers', { method: 'POST', body: request });
}

export async function updateSupplier(id: string, request: SupplierInput): Promise<void> {
  return apiFetch(`/api/staff/procurement/suppliers/${encodeURIComponent(id)}`, { method: 'PUT', body: request });
}

export async function setSupplierActivation(id: string, isActive: boolean): Promise<void> {
  return apiFetch(`/api/staff/procurement/suppliers/${encodeURIComponent(id)}/activation`, {
    method: 'PUT',
    body: { isActive },
  });
}
