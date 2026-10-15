import type { HttpHandler } from 'msw';
import { anaesthesiaHandlers } from './anaesthesia';
import { bookingHandlers } from './booking';
import { catalogueHandlers } from './catalogue';
import { clinicalHandlers } from './clinical';
import { portalSurgeryHandlers } from './portal';
import { reportHandlers } from './reports';
import { supportHandlers } from './support';

export { surgeryFixtureIds, setSurgeryCallerRoles } from './fixtures';
export { resetSurgeryMockData, getDb as surgeryMockDb } from './store';

/**
 * HO-SRG-002 §12/§13 — this handover's own handlers live under `mocks/handlers/surgeries/**`.
 * `mocks/handlers/index.ts` collects them (aliased there, like the other areas'); the pre-wired
 * `areas/surgery.ts` stub stays empty. Order matters: the clinical handlers (which include the literal
 * `recovery-board` route) come BEFORE the booking handlers' `:id` detail route, and the cross-area
 * look-ups come first so they answer for this mock's cats and customers and fall through for the rest.
 */
export const surgeryAreaHandlers: HttpHandler[] = [
  ...clinicalHandlers,
  ...anaesthesiaHandlers,
  ...reportHandlers,
  ...catalogueHandlers,
  ...bookingHandlers,
  ...portalSurgeryHandlers,
];

/** Registered BEFORE the cat, customer and inventory mocks (see `mocks/handlers/index.ts`). */
export const surgerySupportHandlers: HttpHandler[] = supportHandlers;
