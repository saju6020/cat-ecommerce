/**
 * `lib/routing/boarding-links.ts` — HO-BRD-002 §13. The Boarding paths in one place, so no component
 * writes a path string of its own. `BOARDING_NEXT_PREFIX` is the entry HO-BRD-002 §8.1 asks to be
 * added to the sign-in `?next=` allow-list (`['/portal/', '/book', '/shop', …]`), so a search that
 * was interrupted by sign-in comes back with its parameters.
 */
export const BOARDING_NEXT_PREFIX = '/boarding';

export const BOARDING_BOOK = '/boarding';
export const BOARDING_DAYCARE = '/boarding/daycare';
export const PORTAL_BOARDING = '/portal/boarding';
export const STAFF_BOARDING = '/staff/boarding';
export const STAFF_BOARDING_BOARD = '/staff/boarding/board';
export const STAFF_BOARDING_STAYS = '/staff/boarding/stays';
export const STAFF_BOARDING_NEW_STAY = '/staff/boarding/stays/new';
export const STAFF_BOARDING_TASKS = '/staff/boarding/tasks';
export const STAFF_BOARDING_INCIDENTS = '/staff/boarding/incidents';
export const STAFF_BOARDING_DAYCARE = '/staff/boarding/daycare';
export const STAFF_ADMIN_BOARDING_UNITS = '/staff/admin/boarding/units';
export const STAFF_ADMIN_BOARDING_RATE_CARDS = '/staff/admin/boarding/rate-cards';
export const STAFF_ADMIN_BOARDING_ROUTINE = '/staff/admin/boarding/care-routine';
/** The HO-ADM-002 content page the reserved response points at (HO-BRD-001 §8.10). */
export const BOARDING_PAYMENT_INSTRUCTIONS = '/pages/boarding-payment-instructions';
export const BOARDING_PAYMENT_INSTRUCTIONS_SLUG = 'boarding-payment-instructions';

const enc = encodeURIComponent;

export const portalStayHref = (stayId: string) => `${PORTAL_BOARDING}/${enc(stayId)}`;
export const portalStayDiaryHref = (stayId: string) => `${portalStayHref(stayId)}/diary`;
export const portalStaySummaryHref = (stayId: string) => `${portalStayHref(stayId)}/summary`;
export const portalIncidentHref = (stayId: string, incidentId: string) =>
  `${portalStayHref(stayId)}/incidents/${enc(incidentId)}`;
export const portalApprovalHref = (stayId: string, approvalId: string) =>
  `${portalStayHref(stayId)}/approvals/${enc(approvalId)}`;
export const staffStayHref = (stayId: string, tab?: string) =>
  `${STAFF_BOARDING_STAYS}/${enc(stayId)}${tab ? `?tab=${enc(tab)}` : ''}`;

export interface BoardingSearchParams {
  arrivalOn?: string;
  departureOn?: string;
  catCount?: number;
  unitType?: string;
  stayId?: string;
}

/** `/boarding?arrivalOn=…&departureOn=…&catCount=2&unitType=Family` (US-BRD-02 deep link). */
export function boardingSearchHref(params: BoardingSearchParams, base = BOARDING_BOOK): string {
  const search = new URLSearchParams();
  if (params.arrivalOn) search.set('arrivalOn', params.arrivalOn);
  if (params.departureOn) search.set('departureOn', params.departureOn);
  if (params.catCount) search.set('catCount', String(params.catCount));
  if (params.unitType) search.set('unitType', params.unitType);
  if (params.stayId) search.set('stayId', params.stayId);
  const query = search.toString();
  return query ? `${base}?${query}` : base;
}
