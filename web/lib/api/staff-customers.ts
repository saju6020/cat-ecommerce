import { apiFetch } from './client';
import type { PagedResult } from './types';

/**
 * Staff-facing customer registration, lookup, duplicates and merge — HO-ACC-002 §8.2's
 * `lib/api/staff-customers.ts` (US-ACC-10/11). Shapes verified against
 * `StaffCustomersController.cs` and `src/Read/.../ViewModels/{StaffCustomer,
 * DuplicateCustomerGroup}ViewModel.cs`. `mobileMatchedOn` is HO-ACC-001 §1 Amended (2026-09-25
 * SECURITY gate fix) — "Primary" / "Additional" / `null`, see each field's doc comment.
 */

// -------------------------------------------------------------------------------------------
// Walk-in registration — C15, US-ACC-10
// -------------------------------------------------------------------------------------------

export interface RegisterWalkInCustomerRequest {
  fullName: string;
  mobileNumber: string;
}

export async function registerWalkInCustomer(
  request: RegisterWalkInCustomerRequest,
): Promise<{ customerId: string }> {
  return apiFetch<{ customerId: string }>('/api/staff/customers', {
    method: 'POST',
    body: request,
  });
}

// -------------------------------------------------------------------------------------------
// Lookup / detail — Q-H1/Q-H2
// -------------------------------------------------------------------------------------------

/** `"Primary"` is a genuine shared primary number; `"Additional"` is only an unverified claim on
 *  someone else's number (never verified — HO-ACC-001 §1 Amended); `null` = no mobile search term,
 *  or matched by email only. */
export type MobileMatchedOn = 'Primary' | 'Additional' | null;

export interface StaffCustomerSummary {
  id: string;
  fullName: string;
  mobileNumber: string | null;
  emailAddress: string | null;
  recordStatus: 'Complete' | 'Incomplete';
  status: 'Active' | 'Merged' | 'Anonymised';
  mobileMatchedOn: MobileMatchedOn;
}

export interface FindCustomersParams {
  mobile?: string;
  email?: string;
}

export async function findCustomers(params: FindCustomersParams): Promise<StaffCustomerSummary[]> {
  const query = new URLSearchParams();
  if (params.mobile) query.set('mobile', params.mobile);
  if (params.email) query.set('email', params.email);
  return apiFetch<StaffCustomerSummary[]>(`/api/staff/customers?${query.toString()}`);
}

export interface DuplicateCustomerCat {
  catId: string;
  name: string;
  isDeceased: boolean;
}

/** CR MEDIUM 2 (first-pass gate fix round, item 13) — the fresh single-customer read (Q-H1,
 *  MergeReview's own source, ADR-FE-011) used to report only `catCount`; `cats` is new. `catCount`
 *  is kept, unchanged, for `CustomerDetail.tsx` — always equal to `cats.length`. */
export interface StaffCustomer {
  id: string;
  fullName: string;
  mobileNumber: string | null;
  emailAddress: string | null;
  recordStatus: 'Complete' | 'Incomplete';
  registrationChannel: 'SelfService' | 'Reception';
  catCount: number;
  cats: DuplicateCustomerCat[];
  status: 'Active' | 'Merged' | 'Anonymised';
  mergedIntoCustomerId: string | null;
}

export async function getStaffCustomer(customerId: string): Promise<StaffCustomer> {
  return apiFetch<StaffCustomer>(`/api/staff/customers/${encodeURIComponent(customerId)}`);
}

// -------------------------------------------------------------------------------------------
// Duplicates — Q-I1, US-ACC-11
// -------------------------------------------------------------------------------------------

/** CR MEDIUM 2 (first-pass gate fix round, item 13) — `recordStatus`/`registrationChannel` are
 *  new; §11.12's prose always asked for them per member, but the real API (Q-I1) didn't carry them
 *  before this fix (a §20.7 contract gap this round closes, not a client-side addition — see the
 *  matching Amended row in HO-ACC-001 §1). */
export interface DuplicateCustomerMember {
  customerId: string;
  fullName: string;
  mobileNumber: string | null;
  emailAddress: string | null;
  cats: DuplicateCustomerCat[];
  mobileMatchedOn: MobileMatchedOn;
  recordStatus: 'Complete' | 'Incomplete';
  registrationChannel: 'SelfService' | 'Reception';
}

export interface DuplicateCustomerGroup {
  members: DuplicateCustomerMember[];
}

export interface GetDuplicateGroupsParams {
  pageNumber?: number;
  pageSize?: number;
}

// Coordinator heads-up (2026-09-26): an integrator branch about to merge caps `pageSize` at 100
// and `pageNumber` at 1,000,000 on this endpoint too (400 otherwise). No caller here passes either
// today (`DuplicateGroupList.tsx`'s own `DUPLICATE_GROUPS_PAGE_SIZE` is 20), but clamping
// defensively at the one call site every request goes through means a future caller can never
// accidentally send an oversized value.
const MAX_PAGE_SIZE = 100;
const MAX_PAGE_NUMBER = 1_000_000;

export async function getDuplicateCustomerGroups(
  params: GetDuplicateGroupsParams = {},
): Promise<PagedResult<DuplicateCustomerGroup>> {
  const query = new URLSearchParams();
  query.set('pageNumber', String(Math.min(params.pageNumber ?? 1, MAX_PAGE_NUMBER)));
  query.set('pageSize', String(Math.min(params.pageSize ?? 20, MAX_PAGE_SIZE)));
  return apiFetch<PagedResult<DuplicateCustomerGroup>>(
    `/api/staff/customers/duplicates?${query.toString()}`,
  );
}

// -------------------------------------------------------------------------------------------
// Merge — C16, US-ACC-11, ADR-FE-011
// -------------------------------------------------------------------------------------------

export interface MergeCustomerRequest {
  survivingCustomerId: string;
  reason: string;
}

export interface MergeCustomerResult {
  survivingCustomerId: string;
  isReapplication: boolean;
}

export async function mergeCustomer(
  duplicateCustomerId: string,
  request: MergeCustomerRequest,
): Promise<MergeCustomerResult> {
  return apiFetch<MergeCustomerResult>(
    `/api/staff/customers/${encodeURIComponent(duplicateCustomerId)}/merge`,
    { method: 'POST', body: request },
  );
}
