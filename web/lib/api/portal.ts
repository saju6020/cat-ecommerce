import { apiFetch } from './client';

/**
 * `lib/api/portal.ts` — HO-POR-002 §12, built against the MERGED HO-POR-001 backend
 * (`PortalController`, `PortalDashboardViewModels.cs`), not the handover's §12.4 draft. Where they
 * differ the merged code wins — recorded in HO-POR-002 §1 Amended 2026-10-13 and ADR-FE-126.
 *
 * Differences that shape these types:
 *  - the dashboard has NO `stays` and NO `subscriptions` key (owner skipped BRD and SUB). Every
 *    section key is optional here because a missing key means "not delivered yet" — never empty,
 *    never unavailable;
 *  - `Unavailable` carries `data: null`;
 *  - re-order is Orders' endpoint, not the portal's, so `reorderOrder` is here only because
 *    HO-ORD-002's `lib/api/my-orders.ts` does not exist yet; it is the same call (ADR-FE-123).
 */

export type PortalSectionState = 'Ready' | 'Unavailable';

export interface PortalSection<T> {
  state: PortalSectionState;
  /** `null` exactly when `state` is `Unavailable`. */
  data: T | null;
}

export interface PortalMoney {
  /** A decimal string; never summed across currencies. */
  amount: string;
  currencyCode: string;
}

export interface PortalCustomer {
  fullName: string;
}

export type VaccinationOverallStatus = 'Current' | 'DueSoon' | 'Overdue';

export interface PortalVaccination {
  overallStatus: VaccinationOverallStatus;
  earliestNextDueOn: string | null;
  bookingEligible: boolean;
}

export interface PortalCatItem {
  catId: string;
  name: string;
  isHighRisk: boolean;
  /** Per-cat degradation: `Unavailable` means this cat's vaccination could not be read. */
  vaccinationState: PortalSectionState;
  vaccination: PortalVaccination | null;
}

export interface PortalCats {
  hasCats: boolean;
  hasMore: boolean;
  items: PortalCatItem[];
}

export interface PortalAppointmentItem {
  appointmentId: string;
  catId: string;
  catName: string;
  serviceName: string;
  startsOn: string | null;
  endsOn: string | null;
  performerName: string | null;
  branchName: string;
  branchTimeZoneId: string;
  status: string;
}

export interface PortalAppointments {
  items: PortalAppointmentItem[];
}

export interface PortalVisitItem {
  visitId: string;
  catId: string;
  catName: string;
  visitedOn: string;
  serviceName: string;
  vetName: string;
  hasDischargeSummary: boolean;
  followUpOn: string | null;
}

export interface PortalVisits {
  items: PortalVisitItem[];
}

export interface PortalPrescriptionCat {
  catId: string;
  catName: string;
  activeCount: number;
  earliestEndsOn: string | null;
}

export interface PortalPrescriptions {
  byCat: PortalPrescriptionCat[];
}

export interface PortalOrderItem {
  orderId: string;
  orderNumber: string;
  placedOn: string;
  status: string;
  total: PortalMoney;
  itemCount: number;
  canReorder: boolean;
  trackingNumber: string | null;
}

export interface PortalOrders {
  items: PortalOrderItem[];
}

export interface PortalInvoiceItem {
  invoiceId: string;
  invoiceNumber: string | null;
  issuedOn: string | null;
  dueOn: string | null;
  total: PortalMoney;
  balanceDue: PortalMoney;
  status: string;
}

export interface PortalBalances {
  hasOutstanding: boolean;
  outstanding: PortalMoney[];
  overdue: PortalMoney[];
  /** Counted over at most the newest 500 issued invoices (HO-POR-001 §1 Amended 2026-10-10 (7)). */
  openInvoiceCount: number;
  oldestDueOn: string | null;
  invoices: PortalInvoiceItem[];
  /** Guidance text only. No payment URL or action field exists. */
  howToPay: string;
}

/** `GET /api/portal/dashboard` — a section key that is absent was not delivered by the backend. */
export interface PortalDashboard {
  generatedOn: string;
  customer?: PortalSection<PortalCustomer>;
  cats?: PortalSection<PortalCats>;
  appointments?: PortalSection<PortalAppointments>;
  balances?: PortalSection<PortalBalances>;
  visits?: PortalSection<PortalVisits>;
  prescriptions?: PortalSection<PortalPrescriptions>;
  orders?: PortalSection<PortalOrders>;
}

export function getPortalDashboard(): Promise<PortalDashboard> {
  return apiFetch<PortalDashboard>('/api/portal/dashboard');
}
