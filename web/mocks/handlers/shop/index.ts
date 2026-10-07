import type { HttpHandler } from 'msw';
import { catalogueHandlers } from './catalogue';
import { customerHandlers } from './customer';
import { staffHandlers } from './staff';

export { shopFixtureIds } from './fixtures';
export { resetShopMockData } from './store';

/**
 * HO-SHP-002 §12/§13 — this handover's own handlers live under `mocks/handlers/shop/**`.
 * `mocks/handlers/areas/shop.ts` (the pre-wired stub `mocks/handlers/index.ts` already imports)
 * re-exports this array, so the combined index needs no edit and no parallel branch conflicts on it.
 */
export const shopHandlers: HttpHandler[] = [
  ...catalogueHandlers,
  ...customerHandlers,
  ...staffHandlers,
];
