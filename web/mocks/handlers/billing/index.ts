import type { HttpHandler } from 'msw';
import { opsBillingHandlers } from './ops';
import { portalBillingHandlers } from './portal';
import { staffBillingHandlers } from './staff';

/**
 * `mocks/handlers/billing/index.ts` — HO-BIL-002 §12/§13. This handover's own handlers live under
 * `mocks/handlers/billing/**`; `mocks/handlers/areas/billing.ts` stays the empty stub HO-CAT-002
 * scaffolded (HO-ADM-002's precedent), and `mocks/handlers/index.ts` adds this array.
 */
export const billingAreaHandlers: HttpHandler[] = [
  ...staffBillingHandlers,
  ...opsBillingHandlers,
  ...portalBillingHandlers,
];

export { billingIds } from './fixtures';
export { resetBillingMockData, setLoyaltyEnabledForMock } from './store';
