import type { HttpHandler } from 'msw';
import { catalogueHandlers } from './catalogue';
import { consentHandlers } from './consents';
import { diagnosticsHandlers } from './diagnostics';
import { historyHandlers } from './history';
import { portalVisitHandlers } from './portal';
import { visitStaffHandlers } from './staff';
import { clinicalUploadHandlers } from './uploads';

export { emrFixtureIds, setMockCallerRoles, setMockSignedUrlTtlSeconds } from './fixtures';
export { resetEmrMockData } from './store';

/**
 * HO-EMR-002 §12 — every clinical handler, generated from the real controllers on master
 * (HO-EMR-001 §12.4 as amended in this handover's report). Registered in `handlers/index.ts`
 * BEFORE `fileStorageHandlers` so the clinical upload registry sees staff uploads first.
 *
 * Deliberately absent: `GET /api/staff/dispensary/**` and the treatment endpoints — Phase F of the
 * backend is not built, so a mock for them would invent a contract (HO-EMR-002 §20.5).
 */
export const emrHandlers: HttpHandler[] = [
  ...clinicalUploadHandlers,
  ...visitStaffHandlers,
  ...diagnosticsHandlers,
  ...historyHandlers,
  ...consentHandlers,
  ...catalogueHandlers,
  ...portalVisitHandlers,
];
