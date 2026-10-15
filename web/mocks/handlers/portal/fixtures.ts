import type { PortalBalances, PortalCatItem, PortalDashboard } from '@/lib/api/portal';
import type { ReorderResult } from '@/lib/api/my-orders';

/**
 * `mocks/handlers/portal/fixtures.ts` — HO-POR-002 §12. Dashboard bodies shaped exactly like
 * `PortalDashboardViewModel` on master (HO-POR-001, merged): camelCase, `yyyy-MM-dd` dates, money as
 * `{ amount: "…", currencyCode }`, `Unavailable` sections with `data: null`, and NO `stays` /
 * `subscriptions` key. No field here is one the backend does not send (HO-ACC-002 §5.1).
 */
export const portalIds = {
  cats: {
    noodle: 'a1000000-0000-0000-0000-000000000001',
    pepper: 'a1000000-0000-0000-0000-000000000002',
    tofu: 'a1000000-0000-0000-0000-000000000003',
  },
  appointments: {
    vaccination: 'b1000000-0000-0000-0000-000000000001',
    london: 'b1000000-0000-0000-0000-000000000002',
  },
  orders: {
    pending: 'c1000000-0000-0000-0000-000000000001',
    delivered: 'c1000000-0000-0000-0000-000000000002',
    cancelled: 'c1000000-0000-0000-0000-000000000003',
    mixed: 'c1000000-0000-0000-0000-000000000004',
    nothing: 'c1000000-0000-0000-0000-000000000005',
    notFound: 'c1000000-0000-0000-0000-0000000000f4',
    refused: 'c1000000-0000-0000-0000-0000000000f0',
    conflict: 'c1000000-0000-0000-0000-0000000000f9',
  },
  visits: {
    withSummary: 'd1000000-0000-0000-0000-000000000001',
    withoutSummary: 'd1000000-0000-0000-0000-000000000002',
  },
  invoices: {
    overdue: 'e1000000-0000-0000-0000-000000000001',
    open: 'e1000000-0000-0000-0000-000000000002',
    usd: 'e1000000-0000-0000-0000-000000000003',
  },
} as const;

const GENERATED_ON = '2026-10-12T04:30:00+00:00';
const ready = <T>(data: T) => ({ state: 'Ready' as const, data });
const unavailable = () => ({ state: 'Unavailable' as const, data: null });

const HOW_TO_PAY =
  'You can pay at the clinic by cash or card, or by bank transfer. Contact the clinic if you need the transfer details.';

function cat(
  catId: string,
  name: string,
  overallStatus: 'Current' | 'DueSoon' | 'Overdue',
  earliestNextDueOn: string | null,
  isHighRisk = false,
): PortalCatItem {
  return {
    catId,
    name,
    isHighRisk,
    vaccinationState: 'Ready',
    vaccination: { overallStatus, earliestNextDueOn, bookingEligible: true },
  };
}

export const fullBalances: PortalBalances = {
  hasOutstanding: true,
  outstanding: [
    { amount: '3250.00', currencyCode: 'BDT' },
    { amount: '40.00', currencyCode: 'USD' },
  ],
  overdue: [{ amount: '1250.00', currencyCode: 'BDT' }],
  openInvoiceCount: 3,
  oldestDueOn: '2026-09-16',
  invoices: [
    {
      invoiceId: portalIds.invoices.overdue,
      invoiceNumber: 'INV-2026-00318',
      issuedOn: '2026-09-02',
      dueOn: '2026-09-16',
      total: { amount: '1250.00', currencyCode: 'BDT' },
      balanceDue: { amount: '1250.00', currencyCode: 'BDT' },
      status: 'Issued',
    },
    {
      invoiceId: portalIds.invoices.open,
      invoiceNumber: 'INV-2026-00341',
      issuedOn: '2026-09-16',
      dueOn: '2026-09-30',
      total: { amount: '2500.00', currencyCode: 'BDT' },
      balanceDue: { amount: '2000.00', currencyCode: 'BDT' },
      status: 'PartPaid',
    },
    {
      invoiceId: portalIds.invoices.usd,
      invoiceNumber: 'INV-2026-00352',
      issuedOn: '2026-09-20',
      dueOn: '2026-10-07',
      total: { amount: '40.00', currencyCode: 'USD' },
      balanceDue: { amount: '40.00', currencyCode: 'USD' },
      status: 'Issued',
    },
  ],
  howToPay: HOW_TO_PAY,
};

/** Two cats (one DueSoon, one Overdue and high risk), appointments in two zones, orders in four
 *  states, balances in BDT and USD, visits with and without a summary and with a follow-up date,
 *  prescriptions. */
export function fullDashboard(): PortalDashboard {
  return {
    generatedOn: GENERATED_ON,
    customer: ready({ fullName: 'Sam Rahman' }),
    cats: ready({
      hasCats: true,
      hasMore: false,
      items: [
        cat(portalIds.cats.noodle, 'Noodle', 'DueSoon', '2026-10-30'),
        cat(portalIds.cats.pepper, 'Pepper', 'Overdue', '2026-09-12', true),
        cat(portalIds.cats.tofu, 'Tofu', 'Current', '2027-03-01'),
      ],
    }),
    appointments: ready({
      items: [
        {
          appointmentId: portalIds.appointments.vaccination,
          catId: portalIds.cats.noodle,
          catName: 'Noodle',
          serviceName: 'Vaccination',
          startsOn: '2026-10-20T04:30:00+00:00',
          endsOn: '2026-10-20T04:50:00+00:00',
          performerName: 'Dr Rahman',
          branchName: 'Gulshan branch',
          branchTimeZoneId: 'Asia/Dhaka',
          status: 'Booked',
        },
        {
          appointmentId: portalIds.appointments.london,
          catId: portalIds.cats.pepper,
          catName: 'Pepper',
          serviceName: 'Consultation',
          startsOn: '2026-10-22T16:00:00+00:00',
          endsOn: '2026-10-22T16:20:00+00:00',
          performerName: null,
          branchName: 'London branch',
          branchTimeZoneId: 'Europe/London',
          status: 'CheckedIn',
        },
      ],
    }),
    balances: ready(structuredClone(fullBalances)),
    visits: ready({
      items: [
        {
          visitId: portalIds.visits.withSummary,
          catId: portalIds.cats.noodle,
          catName: 'Noodle',
          visitedOn: '2026-09-30',
          serviceName: 'Consultation',
          vetName: 'Dr Rahman',
          hasDischargeSummary: true,
          followUpOn: '2026-10-14',
        },
        {
          visitId: portalIds.visits.withoutSummary,
          catId: portalIds.cats.pepper,
          catName: 'Pepper',
          visitedOn: '2026-08-28',
          serviceName: 'Vaccination',
          vetName: 'Dr Chowdhury',
          hasDischargeSummary: false,
          followUpOn: null,
        },
      ],
    }),
    prescriptions: ready({
      byCat: [
        {
          catId: portalIds.cats.noodle,
          catName: 'Noodle',
          activeCount: 2,
          earliestEndsOn: '2026-10-18',
        },
        { catId: portalIds.cats.pepper, catName: 'Pepper', activeCount: 1, earliestEndsOn: null },
      ],
    }),
    orders: ready({
      items: [
        {
          orderId: portalIds.orders.pending,
          orderNumber: 'SO-2026-00061',
          placedOn: '2026-10-09T05:00:00+00:00',
          status: 'PendingPayment',
          total: { amount: '1840.00', currencyCode: 'BDT' },
          itemCount: 2,
          canReorder: false,
          trackingNumber: null,
        },
        {
          orderId: portalIds.orders.delivered,
          orderNumber: 'SO-2026-00042',
          placedOn: '2026-09-02T05:00:00+00:00',
          status: 'Delivered',
          total: { amount: '3450.00', currencyCode: 'BDT' },
          itemCount: 3,
          canReorder: true,
          trackingNumber: 'DHK-88213-BD',
        },
        {
          orderId: portalIds.orders.cancelled,
          orderNumber: 'SO-2026-00031',
          placedOn: '2026-08-14T05:00:00+00:00',
          status: 'Cancelled',
          total: { amount: '980.00', currencyCode: 'BDT' },
          itemCount: 1,
          canReorder: true,
          trackingNumber: null,
        },
        {
          orderId: portalIds.orders.mixed,
          orderNumber: 'SO-2026-00027',
          placedOn: '2026-07-20T05:00:00+00:00',
          status: 'Delivered',
          total: { amount: '5210.00', currencyCode: 'BDT' },
          itemCount: 6,
          canReorder: true,
          trackingNumber: null,
        },
      ],
    }),
  };
}

/** A brand-new customer: every section `Ready` and empty, `hasCats: false`. */
export function newCustomerDashboard(): PortalDashboard {
  return {
    generatedOn: GENERATED_ON,
    customer: ready({ fullName: 'Nusrat Jahan' }),
    cats: ready({ hasCats: false, hasMore: false, items: [] }),
    appointments: ready({ items: [] }),
    balances: ready({
      hasOutstanding: false,
      outstanding: [],
      overdue: [],
      openInvoiceCount: 0,
      oldestDueOn: null,
      invoices: [],
      howToPay: HOW_TO_PAY,
    }),
    visits: ready({ items: [] }),
    prescriptions: ready({ byCat: [] }),
    orders: ready({ items: [] }),
  };
}

/** `balances` and `visits` Unavailable (and nothing else lost). */
export function degradedDashboard(): PortalDashboard {
  return { ...fullDashboard(), balances: unavailable(), visits: unavailable() };
}

/** `cats` Unavailable — the no-cats invitation must NOT show. */
export function catsUnavailableDashboard(): PortalDashboard {
  return { ...fullDashboard(), cats: unavailable() };
}

/** 13 cats: the backend shows 12 and says `hasMore`. */
export function manyCatsDashboard(): PortalDashboard {
  const items = Array.from({ length: 12 }, (_, index) =>
    cat(
      `a2000000-0000-0000-0000-0000000000${String(index + 10)}`,
      `Cat ${index + 1}`,
      'Current',
      '2027-01-01',
    ),
  );
  return {
    ...fullDashboard(),
    cats: ready({ hasCats: true, hasMore: true, items }),
  };
}

/** The `orders` key absent: "not delivered yet" (HO-POR-001 §1 Amended 2026-10-09). */
export function ordersNotDeliveredDashboard(): PortalDashboard {
  const { orders: _omitted, ...rest } = fullDashboard();
  void _omitted;
  return rest;
}

export const reorderResults = {
  allAdded: { addedCount: 3, unavailable: [] } satisfies ReorderResult,
  mixed: {
    addedCount: 3,
    unavailable: [
      { productName: 'Feliway Classic diffuser refill', reason: 'OutOfStock' },
      { productName: 'Catit Creamy lick treats', reason: 'NotPurchasable' },
      { productName: 'Hill’s c/d Urinary Care', reason: 'PrescriptionNotAuthorised' },
      { productName: 'Tasty Bites chicken', reason: 'CartFull' },
      { productName: 'Mystery item', reason: 'SomethingTheClientHasNeverHeardOf' },
    ],
  } satisfies ReorderResult,
  nothing: {
    addedCount: 0,
    unavailable: [
      { productName: 'Royal Canin Renal', reason: 'OutOfStock' },
      { productName: 'Hill’s z/d Skin', reason: 'PrescriptionNotAuthorised' },
    ],
  } satisfies ReorderResult,
};

export type PortalScenario =
  'full' | 'new' | 'degraded' | 'catsUnavailable' | 'manyCats' | 'ordersNotDelivered' | 'failed';

export function dashboardFor(scenario: PortalScenario): PortalDashboard {
  switch (scenario) {
    case 'new':
      return newCustomerDashboard();
    case 'degraded':
      return degradedDashboard();
    case 'catsUnavailable':
      return catsUnavailableDashboard();
    case 'manyCats':
      return manyCatsDashboard();
    case 'ordersNotDelivered':
      return ordersNotDeliveredDashboard();
    default:
      return fullDashboard();
  }
}
