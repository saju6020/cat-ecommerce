import { HttpResponse } from 'msw';
import { accountsFixtureIds, getStaffRoleIdsForRequest } from '../areas/accounts';
import { administrationFixtureIds } from '../admin/fixtures';

/**
 * `mocks/handlers/billing/fixtures.ts` — HO-BIL-002 §12. Ids and response helpers shared by the
 * Billing mock modules. Shapes are generated from HO-BIL-001 §12.4 **as the real controllers send
 * them** (`Read/ViewModels/Billing*ViewModels.cs`): no field here is one the API does not return
 * (HO-ACC-002 §5.1). Like the other mocks, session identity is not modelled beyond the signed-in
 * staff member being `billingIds.meId` (the seeded administrator) and the staff roles the accounts
 * mock exposes; a `403` a test needs is produced with `server.use(...)`.
 *
 * The mock plays the server: its money arithmetic (integer minor units) lives in `store.ts` and
 * ONLY there — the real UI never computes money (ADR-FE-080).
 */
export const billingIds = {
  mainBranchId: administrationFixtureIds.mainBranchId,
  /** The second branch has no billing profile (`BillingProfileMissing`). */
  londonBranchId: administrationFixtureIds.londonBranchId,

  customerId: accountsFixtureIds.customerId, // "Sam Rahman" — also the signed-in portal customer
  customerName: 'Sam Rahman',
  otherCustomerId: 'c0000000-0000-0000-0000-0000000000b2', // "Rahima Akter" — 404 for Sam on the portal
  otherCustomerName: 'Rahima Akter',

  meId: accountsFixtureIds.adminStaffId,
  meName: 'Ada Administrator',
  otherStaffId: 's0000000-0000-0000-0000-0000000000b1',
  otherStaffName: 'Rezwan Ahmed',

  taxStd: administrationFixtureIds.taxClassStandardId,
  taxNoRate: 't0000000-0000-0000-0000-00000000a0c3',

  // charges (Sam, main branch)
  chargeConsultId: 'ca000000-0000-0000-0000-000000000001',
  chargeVaccineId: 'ca000000-0000-0000-0000-000000000002',
  chargeFoodId: 'ca000000-0000-0000-0000-000000000003',
  chargeBoardingId: 'ca000000-0000-0000-0000-000000000004',
  chargeRacyId: 'ca000000-0000-0000-0000-000000000005',
  chargeNoRateId: 'ca000000-0000-0000-0000-000000000006',
  chargeDentalId: 'ca000000-0000-0000-0000-000000000007', // on the draft

  // invoices
  draftInvoiceId: 'd1000000-0000-0000-0000-000000000001',
  issuedInvoiceId: 'd1000000-0000-0000-0000-000000000101',
  partPaidInvoiceId: 'd1000000-0000-0000-0000-000000000102',
  paidInvoiceId: 'd1000000-0000-0000-0000-000000000103',
  creditedInvoiceId: 'd1000000-0000-0000-0000-000000000104',
  correctedInvoiceId: 'd1000000-0000-0000-0000-000000000105',
  otherCustomerInvoiceId: 'd1000000-0000-0000-0000-000000000201',

  creditNoteId: 'e1000000-0000-0000-0000-000000000001',

  // payments
  cashPaymentId: 'f1000000-0000-0000-0000-000000000001',
  partPaymentId: 'f1000000-0000-0000-0000-000000000002',
  freeCreditPaymentId: 'f1000000-0000-0000-0000-000000000003',
  depositPaymentId: 'f1000000-0000-0000-0000-000000000004',
  pendingOtherPaymentId: 'f1000000-0000-0000-0000-000000000005',
  pendingOwnPaymentId: 'f1000000-0000-0000-0000-000000000006',
  reversedPaymentId: 'f1000000-0000-0000-0000-000000000007',
  rejectedPaymentId: 'f1000000-0000-0000-0000-000000000008',
  otherCustomerPaymentId: 'f1000000-0000-0000-0000-000000000009',

  // reconciliation
  statementId: 'b5000000-0000-0000-0000-000000000001',
  codPaymentUnremittedId: 'f2000000-0000-0000-0000-000000000001',
  codPaymentShortfallId: 'f2000000-0000-0000-0000-000000000002',
  codPaymentRemittedId: 'f2000000-0000-0000-0000-000000000003',
} as const;

export interface MockViolation {
  propertyName: string;
  errorCode: string;
  errorMessage: string;
}

export function violation(
  propertyName: string,
  errorCode: string,
  errorMessage: string,
  status = 400,
) {
  return HttpResponse.json(
    {
      errors: [{ propertyName, errorCode, errorMessage, resourceName: 'Billing' }],
      isValid: false,
    },
    { status },
  );
}

export function notFound() {
  return new HttpResponse(null, { status: 404 });
}

export function forbidden() {
  return HttpResponse.json(
    { errors: [{ errorCode: 'ActionNotPermitted', errorMessage: 'Not permitted.' }] },
    { status: 403 },
  );
}

/** `Administrator` and `Accountant` hold the accounts-only actions (approve, reverse, close,
 *  reconcile, export); a request with no modelled staff session is treated as permitted. */
export function isAccountsRole(request: Request): boolean {
  const roles = getStaffRoleIdsForRequest(request);
  if (roles === null) return true;
  return roles.includes('Administrator') || roles.includes('Accountant');
}

export function isAdministrator(request: Request): boolean {
  const roles = getStaffRoleIdsForRequest(request);
  if (roles === null) return true;
  return roles.includes('Administrator');
}

export function paged<T>(items: T[], pageNumber: number, pageSize: number) {
  const start = (pageNumber - 1) * pageSize;
  return {
    items: items.slice(start, start + pageSize),
    pageNumber,
    pageSize,
    totalCount: items.length,
  };
}

export function pageParams(url: URL, defaultSize = 50): { pageNumber: number; pageSize: number } {
  const pageNumber = Math.max(1, Math.trunc(Number(url.searchParams.get('pageNumber')) || 1));
  const pageSize = Math.min(200, Math.max(1, Math.trunc(Number(url.searchParams.get('pageSize')) || defaultSize)));
  return { pageNumber, pageSize };
}
