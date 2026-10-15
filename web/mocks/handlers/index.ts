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
// HO-EMR-002 §12/§13 — this handover's handlers live under mocks/handlers/visits/** (the path its
// §12 names; areas/consultation.ts stays the empty stub). Registered BEFORE fileStorageHandlers: the
// clinical staff-upload handler records what it accepted so an attach can answer with the file.
import { emrHandlers } from './visits';
import { shopHandlers } from './areas/shop';
import { inventoryHandlers } from './areas/inventory';
// HO-IMP-002 §12/§13 — this handover's handlers live under mocks/handlers/inventory/** (aliased,
// like HO-ADM-002's); areas/inventory.ts stays the empty stub.
import { inventoryAreaHandlers } from './inventory';
import { consultationHandlers } from './areas/consultation';
import { pharmacyHandlers } from './areas/pharmacy';
// HO-PHM-002 §12/§13 — this handover's handlers live under mocks/handlers/pharmacy/**, aliased like
// HO-IMP-002's; areas/pharmacy.ts stays the empty stub.
import { pharmacyAreaHandlers } from './pharmacy';
import { surgeryHandlers } from './areas/surgery';
// HO-SRG-002 §12/§13 — this handover's handlers live under mocks/handlers/surgeries/** (aliased, like
// HO-PHM-002's); areas/surgery.ts stays the empty stub. The look-up handlers answer only for the
// surgery mock's own cats and customers and fall through otherwise, so they are registered FIRST.
import { surgeryAreaHandlers, surgerySupportHandlers } from './surgeries';
import { billingHandlers } from './areas/billing';
// HO-BIL-002 §12/§13 — this handover's handlers live under mocks/handlers/billing/**, aliased like
// HO-ADM-002's and HO-NTF-002's; areas/billing.ts stays the empty stub.
import { billingAreaHandlers } from './billing';
import { ordersHandlers } from './areas/orders';
// HO-ORD-002 §12/§13 — this handover's handlers live under mocks/handlers/ordering/** (aliased like
// HO-ADM-002's); areas/orders.ts stays the empty stub. Registered BEFORE the inventory handlers: it
// answers the batch list for the shop mock's SKUs and falls through to inventory for any other.
import { orderingAreaHandlers, orderingCatsHandlers } from './ordering';
import { boardingHandlers } from './areas/boarding';
// HO-BRD-002 §12/§13 — this handover's handlers live under mocks/handlers/boarding/**, aliased like
// HO-ADM-002's; areas/boarding.ts stays the empty stub. Registered BEFORE the admin handlers: it answers
// the one content page `boarding-payment-instructions`, which the admin mock's generic lookup would 404.
import { boardingAreaHandlers } from './boarding';
import { subscriptionsHandlers } from './areas/subscriptions';
import { portalHandlers } from './areas/portal';
// HO-POR-002 §12/§13 — this handover's handlers live under mocks/handlers/portal/** (aliased, like
// HO-ADM-002's); areas/portal.ts stays the empty stub.
import { portalAreaHandlers } from './portal';
import { reportingHandlers } from './areas/reporting';
// HO-RPT-002 §12/§13 — this handover's handlers live under mocks/handlers/reports/**, aliased like
// HO-ADM-002's; areas/reporting.ts stays the empty stub.
import { reportsAreaHandlers } from './reports';
import { crossCuttingHandlers } from './areas/cross-cutting';
// HO-XCT-002 §12.3/§13 — this handover's handlers live under mocks/handlers/operations/** (aliased,
// like HO-ADM-002's); areas/cross-cutting.ts stays the empty stub. Registered FIRST: its erased-
// customer read falls through to the Accounts mock for every other id.
import { operationsAreaHandlers } from './operations';

/**
 * Every MSW handler, combined — one array per area (web/mocks/handlers/areas/<area>.ts),
 * collected here exactly once. Same rule as web/nav/index.ts: an area adds mock handlers by
 * populating its OWN file; this index is pre-wired for all 18 and should not need another edit.
 */
export const handlers: HttpHandler[] = [
  ...surgerySupportHandlers,
  ...operationsAreaHandlers,
  ...accountsHandlers,
  // HO-ORD-002: answers GET /api/cats only for the POS customer's owner id, else falls through.
  ...orderingCatsHandlers,
  ...catProfilesHandlers,
  ...administrationHandlers,
  ...boardingAreaHandlers,
  ...adminAreaHandlers,
  ...notificationsHandlers,
  ...notificationsAreaHandlers,
  ...appointmentsHandlers,
  ...emrHandlers,
  ...fileStorageHandlers,
  ...shopHandlers,
  ...orderingAreaHandlers,
  ...inventoryHandlers,
  ...inventoryAreaHandlers,
  ...consultationHandlers,
  ...pharmacyHandlers,
  ...pharmacyAreaHandlers,
  ...surgeryHandlers,
  ...surgeryAreaHandlers,
  ...billingHandlers,
  ...billingAreaHandlers,
  ...ordersHandlers,
  ...boardingHandlers,
  ...subscriptionsHandlers,
  ...portalHandlers,
  ...portalAreaHandlers,
  ...reportingHandlers,
  ...reportsAreaHandlers,
  ...crossCuttingHandlers,
];
