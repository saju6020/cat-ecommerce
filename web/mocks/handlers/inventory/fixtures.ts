import { HttpResponse } from 'msw';
import { administrationFixtureIds } from '../admin/fixtures';
import { getStaffRoleIdsForRequest } from '../areas/accounts';

/**
 * `mocks/handlers/inventory/fixtures.ts` — HO-IMP-002 §12. Shared ids and response helpers for every
 * `mocks/handlers/inventory/*.ts` file. The wire shapes come from the REAL controllers and view
 * models on master (HO-ACC-002 §5.1: a mock must not return a field the API does not), enforced here
 * by typing every payload with the `lib/api/*` interfaces.
 */

export const inventoryFixtureIds = {
  mainBranchId: administrationFixtureIds.mainBranchId,
  londonBranchId: administrationFixtureIds.londonBranchId,

  warehouseId: 'e1000000-0000-4000-8000-000000000001',
  shopFloorId: 'e1000000-0000-4000-8000-000000000002',
  dispensaryId: 'e1000000-0000-4000-8000-000000000003',
  boardingStoreId: 'e1000000-0000-4000-8000-000000000004',
  quarantineId: 'e1000000-0000-4000-8000-000000000005',
  londonWarehouseId: 'e2000000-0000-4000-8000-000000000001',
  londonQuarantineId: 'e2000000-0000-4000-8000-000000000002',

  foodSkuId: 'f1000000-0000-4000-8000-000000000001',
  vaccineSkuId: 'f1000000-0000-4000-8000-000000000002',
  litterSkuId: 'f1000000-0000-4000-8000-000000000003',
  wetFoodSkuId: 'f1000000-0000-4000-8000-000000000004',
  foodBarcode: '4006381333931',
  vaccineBarcode: '5012345678900',

  supplierId: 'a1000000-0000-4000-8000-000000000001',
  supplier2Id: 'a1000000-0000-4000-8000-000000000002',
  inactiveSupplierId: 'a1000000-0000-4000-8000-000000000003',

  poPartId: 'd1000000-0000-4000-8000-000000000001',
  poOpenId: 'd1000000-0000-4000-8000-000000000002',
  poDraftId: 'd1000000-0000-4000-8000-000000000003',
  poClosedId: 'd1000000-0000-4000-8000-000000000004',
  poPartLine1Id: 'd2000000-0000-4000-8000-000000000001',
  poPartLine2Id: 'd2000000-0000-4000-8000-000000000002',

  consignmentPlannedId: '51000000-0000-4000-8000-000000000001',
  consignmentTransitId: '51000000-0000-4000-8000-000000000002',
  consignmentCustomsId: '51000000-0000-4000-8000-000000000003',
  consignmentClearedId: '51000000-0000-4000-8000-000000000004',
  consignmentStaleId: '51000000-0000-4000-8000-000000000005',
  consignmentCancelledId: '51000000-0000-4000-8000-000000000006',

  batchFoodFarId: 'c1000000-0000-4000-8000-000000000001',
  batchFoodSoonId: 'c1000000-0000-4000-8000-000000000002',
  batchFoodTodayId: 'c1000000-0000-4000-8000-000000000003',
  batchFoodExpiredId: 'c1000000-0000-4000-8000-000000000004',
  batchFoodQuarantinedId: 'c1000000-0000-4000-8000-000000000005',
  batchVaccineId: 'c1000000-0000-4000-8000-000000000006',
  batchRecalledId: 'c1000000-0000-4000-8000-000000000007',
  batchLitterId: 'c1000000-0000-4000-8000-000000000008',
  batchWrittenOffId: 'c1000000-0000-4000-8000-000000000009',
  batchWetFoodId: 'c1000000-0000-4000-8000-00000000000a',
  batchLondonId: 'c1000000-0000-4000-8000-00000000000b',

  receiptId: '61000000-0000-4000-8000-000000000001',
  transferId: '71000000-0000-4000-8000-000000000001',
  adjustmentPostedId: '81000000-0000-4000-8000-000000000001',
  adjustmentPendingId: '81000000-0000-4000-8000-000000000002',
  adjustmentStaleId: '81000000-0000-4000-8000-000000000003',
  countOpenId: '91000000-0000-4000-8000-000000000001',
  countSubmittedId: '91000000-0000-4000-8000-000000000002',
  countApprovalId: '91000000-0000-4000-8000-000000000003',
  breachId: 'b1000000-0000-4000-8000-000000000001',
  alertExpiryId: 'a2000000-0000-4000-8000-000000000001',
} as const;

/** The reason strings that make the mock behave like a server rule firing (one per §11.9 code). */
export const inventoryTriggers = {
  supplierCodeTaken: 'DUPLICATE',
  recallLot: 'L7702R',
} as const;

export const MOCK_BASE_CURRENCY = 'BDT';
export const MOCK_BRANCH_ZONE = 'Asia/Dhaka';
/** Above this base-currency value an adjustment or count needs a different approver (ADR-107). */
export const MOCK_APPROVAL_THRESHOLD = 5000;

// ---- roles (presentation of HO-IMP-001 §12.3, by name) ---------------------------------------

export const ROLES = {
  stockView: [
    'Reception',
    'Nurse',
    'Vet',
    'Caretaker',
    'Inventory',
    'ShopManager',
    'Administrator',
  ],
  stockManage: ['Inventory', 'Administrator'],
  approve: ['Administrator'],
  recalls: ['Inventory', 'Administrator'],
  alertAck: ['Inventory', 'ShopManager', 'Administrator'],
  reports: ['Inventory', 'ShopManager', 'Accountant', 'Administrator'],
  procurementView: ['Inventory', 'ShopManager', 'Accountant', 'Administrator'],
  costs: ['Inventory', 'Accountant', 'Administrator'],
  locations: ['Administrator'],
} as const;

/** `null` when the request carries no staff session at all ("not modelled" — allow). */
export function callerRoles(request: Request): string[] | null {
  return getStaffRoleIdsForRequest(request);
}

export function isAllowed(request: Request, allowed: readonly string[]): boolean {
  const roles = callerRoles(request);
  return roles === null || roles.some((role) => allowed.includes(role));
}

/** Returns a `403 ActionNotPermitted` response when the caller's roles are known and none match. */
export function guard(request: Request, allowed: readonly string[]): Response | null {
  return isAllowed(request, allowed) ? null : forbiddenResponse();
}

export function forbiddenResponse(): Response {
  return HttpResponse.json(
    {
      errors: [
        {
          errorCode: 'ActionNotPermitted',
          errorMessage: 'You are not permitted to perform this action.',
        },
      ],
    },
    { status: 403 },
  );
}

export function branchOf(request: Request): string {
  return request.headers.get('x-branch-id') ?? inventoryFixtureIds.mainBranchId;
}

export interface Violation {
  propertyName: string;
  errorCode: string;
  errorMessage: string;
}

/** A real-shaped 400 body: `{ errors: [{ propertyName, errorCode, errorMessage, resourceName }], isValid }`. */
export function violations(list: Violation[], status: 400 | 409 = 400): Response {
  return HttpResponse.json(
    {
      errors: list.map((v) => ({ ...v, resourceName: 'Inventory' })),
      isValid: false,
    },
    { status },
  );
}

export function violation(
  propertyName: string,
  errorCode: string,
  errorMessage: string,
  status: 400 | 409 = 400,
) {
  return violations([{ propertyName, errorCode, errorMessage }], status);
}

export function notFoundResponse(): Response {
  return new HttpResponse(null, { status: 404 });
}

export function noContentResponse(): Response {
  return new HttpResponse(null, { status: 204 });
}

export function pageParams(request: Request): { page: number; size: number; url: URL } {
  const url = new URL(request.url);
  const page = parseInt(url.searchParams.get('pageNumber') ?? '1', 10) || 1;
  const size = Math.min(parseInt(url.searchParams.get('pageSize') ?? '25', 10) || 25, 100);
  return { page, size, url };
}

export function paged<T>(items: T[], request: Request) {
  const { page, size } = pageParams(request);
  return {
    items: items.slice((page - 1) * size, page * size),
    pageNumber: page,
    pageSize: size,
    totalCount: items.length,
  };
}

// ---- number and date helpers (mock-only — the app itself never does this arithmetic) ---------

/** `InventoryViewFormat.Quantity`: `0.###`, trailing zeros trimmed. */
export function q(value: number): string {
  return String(Math.round(value * 1000) / 1000);
}

export function money(amount: number, currencyCode = MOCK_BASE_CURRENCY) {
  return { amount: amount.toFixed(2), currencyCode };
}

export function unitCost(amount: number, currencyCode = MOCK_BASE_CURRENCY) {
  return { amount: amount.toFixed(4), currencyCode };
}

function dhakaDate(date: Date): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: MOCK_BRANCH_ZONE }).format(date);
}

export function todayDate(): string {
  return dhakaDate(new Date());
}

/** `yyyy-MM-dd` for `offsetDays` from today (branch-local). */
export function dateOffset(offsetDays: number): string {
  const base = new Date(`${todayDate()}T00:00:00Z`);
  base.setUTCDate(base.getUTCDate() + offsetDays);
  return base.toISOString().slice(0, 10);
}

export function daysBetween(fromDate: string, toDate: string): number {
  return Math.round(
    (new Date(`${toDate}T00:00:00Z`).getTime() - new Date(`${fromDate}T00:00:00Z`).getTime()) /
      86_400_000,
  );
}

export function nowIso(): string {
  return new Date().toISOString();
}

export function instantOffset(offsetDays: number, hour = 9): string {
  const base = new Date(`${dateOffset(offsetDays)}T00:00:00Z`);
  base.setUTCHours(hour);
  return base.toISOString();
}

/** A copy of `value` without `keys` — how the mock strips its internal bookkeeping fields
 *  (`_branchId`, …) so a response carries exactly the real view model's fields and nothing more. */
export function omit<T extends object, K extends keyof T>(value: T, ...keys: K[]): Omit<T, K> {
  const copy = { ...value };
  for (const key of keys) delete copy[key];
  return copy;
}
