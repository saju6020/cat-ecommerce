import type { HttpHandler } from 'msw';
import { lookupHandlers } from './lookup';
import { procurementHandlers } from './procurement';
import { consignmentHandlers } from './consignments';
import { stockHandlers } from './stock';
import { receiptHandlers } from './receipts';
import { controlHandlers } from './control';
import { alertHandlers } from './alerts';
import { recallHandlers } from './recalls';

export { inventoryFixtureIds } from './fixtures';
export { resetInventoryMockData } from './store';

/**
 * HO-IMP-002 §12/§13 — this handover's own handlers live under `mocks/handlers/inventory/**`.
 * `mocks/handlers/index.ts` collects them (aliased there, like HO-ADM-002's), and the pre-wired
 * `areas/inventory.ts` stub stays empty. Every payload is typed with the `lib/api/*` interfaces,
 * which mirror the REAL view models on master — a mock may not return a field the API does not.
 */
export const inventoryAreaHandlers: HttpHandler[] = [
  ...lookupHandlers,
  ...procurementHandlers,
  ...consignmentHandlers,
  ...stockHandlers,
  ...receiptHandlers,
  ...controlHandlers,
  ...alertHandlers,
  ...recallHandlers,
];
