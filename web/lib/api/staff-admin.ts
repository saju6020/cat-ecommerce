import { apiFetch } from './client';
import type { PagedResult } from './types';

/**
 * Staff account administration — HO-ACC-002 §8.2's `lib/api/staff-admin.ts` (US-ACC-07). Shapes
 * verified against `StaffAccountsController.cs`, `StaffRolesController.cs` and
 * `src/Read/.../ViewModels/{StaffAccountSummary,StaffRoleCatalogue}ViewModel.cs`.
 *
 * ARCH gate LOW (first pass) — `PagedResult<T>` moved to `lib/api/types.ts` (that file's own
 * comment); it used to be declared here and re-exported by `staff-customers.ts` for that area's
 * own use too.
 */

// -------------------------------------------------------------------------------------------
// Role catalogue — Q-F1. ADR-FE-009: rendered from the API, never a transcribed copy of Appendix D.
// -------------------------------------------------------------------------------------------

export interface StaffCapabilityCell {
  capability: string;
  roleId: string;
  access: string;
  qualifier: string | null;
}

export interface StaffRoleCatalogue {
  roleIds: string[];
  capabilities: string[];
  cells: StaffCapabilityCell[];
}

export async function getStaffRoleCatalogue(): Promise<StaffRoleCatalogue> {
  return apiFetch<StaffRoleCatalogue>('/api/staff-roles');
}

// -------------------------------------------------------------------------------------------
// Staff accounts — Q-F2/Q-F3/C9/C10
// -------------------------------------------------------------------------------------------

export interface StaffAccountSummary {
  id: string;
  fullName: string;
  mobileNumber: string | null;
  emailAddress: string | null;
  roleIds: string[];
  hasPassword: boolean;
}

export type StaffAccount = StaffAccountSummary;

export interface GetStaffAccountsParams {
  search?: string;
  roleId?: string;
  pageNumber?: number;
  pageSize?: number;
}

// Coordinator heads-up (2026-09-26): an integrator branch about to merge caps `pageSize` at 100
// and `pageNumber` at 1,000,000 on this endpoint (400 otherwise). No caller here passes either
// today (the one call site uses a fixed pageSize of 20 — app/(staff)/staff/admin/staff-accounts/
// page.tsx), but clamping defensively at the one call site every request goes through means a
// future caller can never accidentally send an oversized value, rather than relying on every call
// site remembering the limit itself.
const MAX_PAGE_SIZE = 100;
const MAX_PAGE_NUMBER = 1_000_000;

export async function getStaffAccounts(
  params: GetStaffAccountsParams = {},
): Promise<PagedResult<StaffAccountSummary>> {
  const query = new URLSearchParams();
  if (params.search) query.set('search', params.search);
  if (params.roleId) query.set('roleId', params.roleId);
  query.set('pageNumber', String(Math.min(params.pageNumber ?? 1, MAX_PAGE_NUMBER)));
  query.set('pageSize', String(Math.min(params.pageSize ?? 20, MAX_PAGE_SIZE)));
  return apiFetch<PagedResult<StaffAccountSummary>>(`/api/staff-accounts?${query.toString()}`);
}

export async function getStaffAccount(staffAccountId: string): Promise<StaffAccount> {
  return apiFetch<StaffAccount>(`/api/staff-accounts/${encodeURIComponent(staffAccountId)}`);
}

export interface CreateStaffAccountRequest {
  fullName: string;
  mobileNumber?: string | null;
  emailAddress?: string | null;
  roleIds: string[];
}

export async function createStaffAccount(
  request: CreateStaffAccountRequest,
): Promise<{ staffAccountId: string }> {
  return apiFetch<{ staffAccountId: string }>('/api/staff-accounts', {
    method: 'POST',
    body: request,
  });
}

export async function changeStaffRoles(staffAccountId: string, roleIds: string[]): Promise<void> {
  await apiFetch<void>(`/api/staff-accounts/${encodeURIComponent(staffAccountId)}/roles`, {
    method: 'PUT',
    body: { roleIds },
  });
}
