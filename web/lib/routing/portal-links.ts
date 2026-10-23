/**
 * `lib/routing/portal-links.ts` — HO-POR-002 §13, ADR-FE-121. The ONE place the portal home names
 * a page another area owns. The dashboard summarises and links out; it never re-implements those
 * pages, and no component under `components/portal/**` writes a path string of its own.
 *
 * Section 20.5: a route whose handover is not built yet still gets its constant here (recorded in
 * docs/test-cases/TC-HO-POR-002.md "Cross-area links"), so the day that page lands nothing but
 * the page itself changes. Not built when this file was written: `PORTAL_ORDERS` /
 * `portalOrderHref` and `CART` (HO-ORD-002), `PORTAL_PRESCRIPTIONS` (HO-PHM-002).
 */

import { sanitizeNextPath } from '@/lib/validation/nextPath';

/** The page a signed-in customer lands on. Section 2: "default post-sign-in landing constant".
 *  Used by `app/(auth)/sign-in/page.tsx` and `app/page.tsx` (owner, 2026-10-13: "Land on /portal"; changed to My orders by the owner 2026-10-17 with the account sidebar;
 *  HO-ACC-002 Amended row). An explicit `?next=` still wins. */
export const DEFAULT_CUSTOMER_LANDING = '/portal/orders';

/** The `?next=` paths a customer may come back to after signing in (see the sign-in page for why
 *  each one is here). Shared with the register page so "Create an account" keeps the same return. */
export const CUSTOMER_RETURN_PREFIXES = [
  '/portal/',
  '/book',
  '/shop',
  '/cart',
  '/checkout',
  '/boarding',
];

/** `path?next=...` when `next` is an allowed customer return path, otherwise just `path`
 *  (Issues.md #5: sign-in -> register -> sign-in keeps the checkout the visitor started from). */
export function withCustomerNext(path: string, next: string | null): string {
  const safe = sanitizeNextPath(next, CUSTOMER_RETURN_PREFIXES, '');
  return safe ? `${path}?next=${encodeURIComponent(safe)}` : path;
}

export const PORTAL_HOME = '/portal';
export const PORTAL_CATS = '/portal/cats';
export const PORTAL_ADD_CAT = '/portal/cats/new';
export const PORTAL_APPOINTMENTS = '/portal/appointments';
export const PORTAL_BILLING = '/portal/billing';
export const PORTAL_PRESCRIPTIONS = '/portal/prescriptions';
export const PORTAL_ORDERS = '/portal/orders';
export const BOOK_APPOINTMENT = '/book';
export const CART = '/cart';
export const SHOP = '/shop';

export const portalCatHref = (catId: string) => `${PORTAL_CATS}/${encodeURIComponent(catId)}`;
export const portalCatVisitsHref = (catId: string) => `${portalCatHref(catId)}/visits`;
export const portalAppointmentHref = (appointmentId: string) =>
  `${PORTAL_APPOINTMENTS}/${encodeURIComponent(appointmentId)}`;
export const portalInvoiceHref = (invoiceId: string) =>
  `${PORTAL_BILLING}/invoices/${encodeURIComponent(invoiceId)}`;
export const portalOrderHref = (orderId: string) =>
  `${PORTAL_ORDERS}/${encodeURIComponent(orderId)}`;
export const portalVisitHref = (visitId: string) => `/portal/visits/${encodeURIComponent(visitId)}`;

/** `/book?catId=…&category=Vaccination` — the booking flow already reads both params
 *  (HO-APT-002 `BookingFlow`). */
export function bookVaccinationHref(catId: string): string {
  const params = new URLSearchParams({ catId, category: 'Vaccination' });
  return `${BOOK_APPOINTMENT}?${params.toString()}`;
}
