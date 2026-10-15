import { apiFetch } from './client';

/**
 * `lib/api/billing-profile.ts` — HO-BIL-002 §11.10, verified against `AdminBillingProfilesController`:
 * `GET /api/admin/billing-profiles/{branchId}` (`Billing.View`; `404` when the branch has none yet)
 * and `PUT` (`BillingProfile.Manage`, `204`). The profile is copied onto every invoice at issue
 * (NFR-LEG-3); issued invoices keep what was printed on them.
 */

export interface BillingProfileBankAccount {
  bankName: string;
  accountName: string;
  accountNumber: string;
  branchName: string | null;
  routingNumber: string | null;
}

export interface BillingProfile {
  branchId: string;
  legalName: string;
  taxRegistrationNumber: string;
  addressLines: string[];
  phone: string | null;
  email: string | null;
  invoiceFooter: string | null;
  bankAccounts: BillingProfileBankAccount[];
  version: number;
}

export interface SetBillingProfileRequest {
  legalName: string;
  taxRegistrationNumber: string;
  addressLines: string[];
  phone?: string;
  email?: string;
  invoiceFooter?: string;
  bankAccounts: Array<{
    bankName: string;
    accountName: string;
    accountNumber: string;
    branchName?: string;
    routingNumber?: string;
  }>;
}

export async function getBillingProfile(branchId: string): Promise<BillingProfile> {
  return apiFetch<BillingProfile>(`/api/admin/billing-profiles/${encodeURIComponent(branchId)}`);
}

export async function setBillingProfile(
  branchId: string,
  request: SetBillingProfileRequest,
): Promise<void> {
  await apiFetch<void>(`/api/admin/billing-profiles/${encodeURIComponent(branchId)}`, {
    method: 'PUT',
    body: request,
  });
}
