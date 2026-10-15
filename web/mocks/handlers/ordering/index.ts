import type { HttpHandler } from 'msw';
import { adminOrderingHandlers } from './admin';
import { orderingCatsHandlers } from './cats';
import { installOrderingMockControls } from './control';
import { customerOrderingHandlers } from './customer';
import { posOrderingHandlers } from './pos';
import { staffOrderingHandlers } from './staff';

/**
 * `mocks/handlers/ordering/index.ts` — HO-ORD-002 §12/§13. This handover's own handlers live under
 * `mocks/handlers/ordering/**`; `mocks/handlers/areas/orders.ts` stays the empty stub HO-CAT-002
 * scaffolded (the HO-ADM-002 / HO-BIL-002 precedent), and `mocks/handlers/index.ts` adds this array.
 * It is registered BEFORE the Inventory handlers because the order screen's batch list for the shop
 * mock's SKUs falls through to Inventory's handler for any other SKU.
 */
export const orderingAreaHandlers: HttpHandler[] = [
  ...customerOrderingHandlers,
  ...staffOrderingHandlers,
  ...posOrderingHandlers,
  ...adminOrderingHandlers,
];

// Test controls for the mock server (window.__orderingMock) — development / test builds only.
if (typeof window !== 'undefined' && process.env.NODE_ENV !== 'production') {
  installOrderingMockControls();
}

export { orderingCatsHandlers };
export { orderingIds } from './fixtures';
export { resetOrderingMockData } from './store';
