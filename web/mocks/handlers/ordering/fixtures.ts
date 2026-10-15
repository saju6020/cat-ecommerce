import { HttpResponse } from 'msw';
import { accountsFixtureIds } from '../areas/accounts';
import { administrationFixtureIds } from '../admin/fixtures';
import { shopFixtureIds } from '../shop/fixtures';

export { forbidden, notFound, paged, pageParams } from '../billing/fixtures';

/**
 * `mocks/handlers/ordering/fixtures.ts` — HO-ORD-002 §12.1. Ids and response helpers for the Ordering
 * mock. Every shape the handlers send is checked against the real DTOs (the ViewModels under
 * src/Read and the controllers under src/Api), not only the handover: a mock may not contain a field
 * HO-ORD-001 does not specify.
 */
export const orderingIds = {
  customerId: accountsFixtureIds.customerId,
  customerName: 'Sam Rahman',
  customerMobile: accountsFixtureIds.customerMobile,
  adminStaffId: accountsFixtureIds.adminStaffId,
  /** "Someone else" — recorded the seeded held payment; `__orderingMock.actAs(...)` switches to it. */
  otherStaffId: 's0000000-0000-0000-0000-0000000000f1',
  mainBranchId: administrationFixtureIds.mainBranchId,
  secondBranchId: administrationFixtureIds.londonBranchId,
  timeZoneId: 'Asia/Dhaka',
  courierPathaoId: 'c1000000-0000-0000-0000-000000000001',
  courierInHouseId: 'c1000000-0000-0000-0000-000000000002',
  zoneDhakaId: 'd1000000-0000-0000-0000-000000000001',
  zoneInactiveId: 'd1000000-0000-0000-0000-000000000002',
  codeWelcomeId: 'dc000000-0000-0000-0000-000000000001',
  codeSaveId: 'dc000000-0000-0000-0000-000000000002',
  codeExpiredId: 'dc000000-0000-0000-0000-000000000003',
  locationCounterId: 'a1000000-0000-0000-0000-000000000001',
  locationShelfId: 'a1000000-0000-0000-0000-000000000002',
  catWhiskers: shopFixtureIds.catAllergy,
  catMittens: shopFixtureIds.catNoDob,
  catBiscuit: shopFixtureIds.catEstimatedDob,
  skuKitten400: shopFixtureIds.skuKitten400,
  skuKitten2kg: shopFixtureIds.skuKitten2kg,
  skuWhiskas: shopFixtureIds.skuWhiskasKitten,
  skuRxEligible: shopFixtureIds.skuRxEligible,
  skuRxNoAuth: shopFixtureIds.skuRxNoAuth,
  skuOutOfStock: shopFixtureIds.skuKitten4kg,
  orders: {
    awaitingTransfer: 'f0000000-0000-0000-0000-000000000101',
    transferReported: 'f0000000-0000-0000-0000-000000000102',
    heldPayment: 'f0000000-0000-0000-0000-000000000103',
    confirmedCod: 'f0000000-0000-0000-0000-000000000104',
    packingPickup: 'f0000000-0000-0000-0000-000000000105',
    dispatchedCod: 'f0000000-0000-0000-0000-000000000106',
    delivered: 'f0000000-0000-0000-0000-000000000107',
    expired: 'f0000000-0000-0000-0000-000000000108',
    counterSale: 'f0000000-0000-0000-0000-000000000109',
    deliveredOpenedFood: 'f0000000-0000-0000-0000-000000000110',
    cancelledPaid: 'f0000000-0000-0000-0000-000000000111',
  },
  returns: {
    requested: 'f2000000-0000-0000-0000-000000000201',
    requestedDefect: 'f2000000-0000-0000-0000-000000000202',
    approved: 'f2000000-0000-0000-0000-000000000203',
    received: 'f2000000-0000-0000-0000-000000000204',
  },
  issueId: 'f3000000-0000-0000-0000-000000000301',
} as const;

export function violation(
  propertyName: string,
  errorCode: string,
  errorMessage: string,
  status = 400,
) {
  return HttpResponse.json(
    {
      errors: [{ propertyName, errorCode, errorMessage, resourceName: 'Ordering' }],
      isValid: false,
    },
    { status },
  );
}

export function violations(
  list: Array<{ propertyName: string; errorCode: string; errorMessage: string }>,
  status = 400,
) {
  return HttpResponse.json(
    { errors: list.map((e) => ({ ...e, resourceName: 'Ordering' })), isValid: false },
    { status },
  );
}

export const noContent = () => new HttpResponse(null, { status: 204 });
export const unauthorised = () => HttpResponse.json({ error: 'session_invalid' }, { status: 401 });

/** True when the request carries the customer session cookie the accounts mock set. */
export function hasCustomerSession(request: Request): boolean {
  const jar = typeof document !== 'undefined' ? document.cookie : '';
  if (/(?:^|;\s*)pc_session=/.test(jar)) return true;
  return /(?:^|;\s*)pc_session=/.test(request.headers.get('cookie') ?? '');
}
