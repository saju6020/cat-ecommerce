import type { HttpHandler } from 'msw';
import { accountsHandlers } from './areas/accounts';
import { catProfilesHandlers } from './areas/cat-profiles';
import { administrationHandlers } from './areas/administration';
// HO-ADM-002 §12/§13 — this handover's own handlers live under mocks/handlers/admin/** (not
// mocks/handlers/areas/administration.ts, which §13 does not list and is left as the empty stub
// HO-CAT-002 scaffolded); aliased to avoid colliding with the import above.
import { administrationHandlers as adminAreaHandlers } from './admin';
import { notificationsHandlers } from './areas/notifications';
// HO-NTF-002 §12/§13 — this handover's handlers live under mocks/handlers/notifications/** (the
// path its §12 names), aliased like HO-ADM-002's; areas/notifications.ts stays the empty stub.
import { notificationsAreaHandlers } from './notifications';
import { appointmentsHandlers } from './areas/appointments';
import { fileStorageHandlers } from './areas/file-storage';
import { shopHandlers } from './areas/shop';
import { inventoryHandlers } from './areas/inventory';
import { consultationHandlers } from './areas/consultation';
import { pharmacyHandlers } from './areas/pharmacy';
import { surgeryHandlers } from './areas/surgery';
import { billingHandlers } from './areas/billing';
import { ordersHandlers } from './areas/orders';
import { boardingHandlers } from './areas/boarding';
import { subscriptionsHandlers } from './areas/subscriptions';
import { portalHandlers } from './areas/portal';
import { reportingHandlers } from './areas/reporting';
import { crossCuttingHandlers } from './areas/cross-cutting';

/**
 * Every MSW handler, combined — one array per area (web/mocks/handlers/areas/<area>.ts),
 * collected here exactly once. Same rule as web/nav/index.ts: an area adds mock handlers by
 * populating its OWN file; this index is pre-wired for all 18 and should not need another edit.
 */
export const handlers: HttpHandler[] = [
  ...accountsHandlers,
  ...catProfilesHandlers,
  ...administrationHandlers,
  ...adminAreaHandlers,
  ...notificationsHandlers,
  ...notificationsAreaHandlers,
  ...appointmentsHandlers,
  ...fileStorageHandlers,
  ...shopHandlers,
  ...inventoryHandlers,
  ...consultationHandlers,
  ...pharmacyHandlers,
  ...surgeryHandlers,
  ...billingHandlers,
  ...ordersHandlers,
  ...boardingHandlers,
  ...subscriptionsHandlers,
  ...portalHandlers,
  ...reportingHandlers,
  ...crossCuttingHandlers,
];
