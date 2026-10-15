import type { HttpHandler } from 'msw';
import { boardingCareHandlers } from './care';
import { boardingCustomerHandlers } from './customer';
import { boardingStaffHandlers } from './staff';

export { boardingFixtureIds, BILLING_LEDGER_STORAGE_KEY, RESERVED_SOON_SECONDS } from './fixtures';
export { resetBoardingMockData, setMockBillingLedger } from './store';

/**
 * HO-BRD-002 §12 — every Boarding handler, generated from HO-BRD-001 §12.4 (with `lib/api/types.ts`
 * noting where §12.4 is silent and the built backend will win). Aliased in
 * `mocks/handlers/index.ts` like the other split area folders; `areas/boarding.ts` stays the empty
 * stub the scaffold shipped.
 */
export const boardingAreaHandlers: HttpHandler[] = [
  ...boardingCustomerHandlers,
  ...boardingStaffHandlers,
  ...boardingCareHandlers,
];
