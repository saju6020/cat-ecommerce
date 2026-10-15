import type { HttpHandler } from 'msw';
import { formularyHandlers } from './formulary';
import { prescribingHandlers } from './prescribing';
import { dispensingHandlers } from './dispensing';
import { registerHandlers } from './register';
import { dietHandlers } from './diet';
import { portalPharmacyHandlers } from './portal';

export { pharmacyFixtureIds, setPharmacyCallerRoles } from './fixtures';
export { resetPharmacyMockData, db as pharmacyMockDb } from './store';

/**
 * HO-PHM-002 §12/§13 — this handover's own handlers live under `mocks/handlers/pharmacy/**`.
 * `mocks/handlers/index.ts` collects them (aliased there, like HO-IMP-002's); the pre-wired
 * `areas/pharmacy.ts` stub stays empty. Every payload is typed with the `lib/api/*` interfaces,
 * which mirror the REAL view models on master — a mock may not return a field the API does not.
 */
export const pharmacyAreaHandlers: HttpHandler[] = [
  ...formularyHandlers,
  ...prescribingHandlers,
  ...dispensingHandlers,
  ...registerHandlers,
  ...dietHandlers,
  ...portalPharmacyHandlers,
];
