import type { HttpHandler } from 'msw';
import { appointmentsHandlers as appointmentsMockHandlers } from '../appointments';

/**
 * Appointments MSW request handlers.
 *
 * Owned entirely by the Appointments area. The handlers themselves live under
 * `mocks/handlers/appointments/**` (HO-APT-002 §12/§13) — this file, which
 * `mocks/handlers/index.ts` already imports, only re-exports them, the same split HO-ADM-002 uses
 * (`mocks/handlers/admin/**`).
 */
export const appointmentsHandlers: HttpHandler[] = appointmentsMockHandlers;
