import { afterEach, describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { PortalDashboard } from '@/components/portal/dashboard/PortalDashboard';
import {
  catsUnavailableDashboard,
  degradedDashboard,
  fullDashboard,
  manyCatsDashboard,
  newCustomerDashboard,
  ordersNotDeliveredDashboard,
  portalIds,
} from '@/mocks/handlers/portal/fixtures';
import {
  PORTAL_ADD_CAT,
  PORTAL_APPOINTMENTS,
  PORTAL_BILLING,
  PORTAL_CATS,
  PORTAL_ORDERS,
  PORTAL_PRESCRIPTIONS,
  bookVaccinationHref,
  portalCatVisitsHref,
  portalInvoiceHref,
  portalOrderHref,
  portalVisitHref,
} from '@/lib/routing/portal-links';
import { renderWithClient, resetPortalMock, serveDashboard } from './helpers';

afterEach(() => resetPortalMock());

const tile = (id: string) => screen.findByTestId(`tile-${id}`);

describe('Portal dashboard — cats (US-POR-01)', () => {
  it('U-A1: a new customer sees the no-cats invitation and every other tile says its true empty', async () => {
    serveDashboard(newCustomerDashboard());
    renderWithClient(<PortalDashboard />);

    const cats = await tile('cats');
    const add = within(cats).getByRole('link', { name: 'Add a cat' });
    expect(add).toHaveAttribute('href', PORTAL_ADD_CAT);
    expect(within(await tile('appointments')).getByText(/no upcoming appointments/i)).toBeVisible();
    expect(within(await tile('orders')).getByText(/haven’t placed an order/i)).toBeVisible();
    expect(within(await tile('visits')).getByText(/no visits on record/i)).toBeVisible();
    expect(within(await tile('prescriptions')).getByText(/no active prescriptions/i)).toBeVisible();
    expect(within(await tile('balances')).getByText(/nothing to pay right now/i)).toBeVisible();
  });

  it('U-A2: cats Unavailable shows the message and Try again, and NO no-cats invitation', async () => {
    serveDashboard(catsUnavailableDashboard());
    renderWithClient(<PortalDashboard />);

    const cats = await tile('cats');
    expect(within(cats).getByText(/couldn’t load your cats/i)).toBeVisible();
    expect(within(cats).getByRole('button', { name: 'Try again' })).toBeVisible();
    expect(screen.queryByTestId('no-cats')).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Add a cat' })).not.toBeInTheDocument();
  });

  it('U-A5: DueSoon and Overdue offer Book now to the booking flow; Current does not', async () => {
    serveDashboard(fullDashboard());
    renderWithClient(<PortalDashboard />);
    const cats = await tile('cats');

    expect(
      within(cats).getByRole('link', { name: 'Book a vaccination for Noodle' }),
    ).toHaveAttribute('href', bookVaccinationHref(portalIds.cats.noodle));
    expect(bookVaccinationHref(portalIds.cats.noodle)).toBe(
      `/book?catId=${portalIds.cats.noodle}&category=Vaccination`,
    );
    expect(within(cats).getByRole('link', { name: 'Book a vaccination for Pepper' })).toBeVisible();
    expect(within(cats).queryByRole('link', { name: /Book a vaccination for Tofu/ })).toBeNull();
    // status as text, with the word — not colour alone
    expect(within(cats).getByText('Due soon')).toBeVisible();
    expect(within(cats).getByText('Overdue')).toBeVisible();
    expect(within(cats).getByText('Up to date')).toBeVisible();
    // the high-risk flag travels with the cat
    expect(within(cats).getByText('High risk')).toBeVisible();
  });

  it('U-A6: a cat whose vaccination is Unavailable says so and the rest of the row renders', async () => {
    const body = fullDashboard();
    body.cats = {
      state: 'Ready',
      data: {
        hasCats: true,
        hasMore: false,
        items: [
          {
            catId: portalIds.cats.noodle,
            name: 'Noodle',
            isHighRisk: false,
            vaccinationState: 'Unavailable',
            vaccination: null,
          },
        ],
      },
    };
    serveDashboard(body);
    renderWithClient(<PortalDashboard />);
    const cats = await tile('cats');
    expect(within(cats).getByText('Vaccination status unavailable.')).toBeVisible();
    expect(within(cats).getByRole('link', { name: 'Noodle' })).toBeVisible();
    expect(within(cats).queryByRole('link', { name: /Book/ })).toBeNull();
  });

  it('U-A8: hasMore points at the full cat list', async () => {
    serveDashboard(manyCatsDashboard());
    renderWithClient(<PortalDashboard />);
    const cats = await tile('cats');
    expect(within(cats).getByRole('link', { name: 'See all cats' })).toHaveAttribute(
      'href',
      PORTAL_CATS,
    );
    expect(within(cats).getAllByRole('listitem')).toHaveLength(12);
  });

  it('U-A17: the greeting uses the customer name; a customer section that is Unavailable loses only the name', async () => {
    serveDashboard(fullDashboard());
    const first = renderWithClient(<PortalDashboard />);
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Welcome back, Sam' }),
    ).toBeVisible();
    first.unmount();

    const body = fullDashboard();
    body.customer = { state: 'Unavailable', data: null };
    serveDashboard(body);
    renderWithClient(<PortalDashboard />);
    expect(await screen.findByRole('heading', { level: 1, name: 'Welcome back' })).toBeVisible();
    expect(await tile('cats')).toBeVisible();
  });
});

describe('Portal dashboard — appointments, visits, prescriptions', () => {
  it('U-A7: an appointment in Europe/London is shown in the branch zone with the zone named', async () => {
    serveDashboard(fullDashboard());
    renderWithClient(<PortalDashboard />);
    const appointments = await tile('appointments');

    // 16:00Z on 22 Oct 2026 is 17:00 in London (BST); 04:30Z on 20 Oct is 10:30 in Dhaka.
    expect(within(appointments).getByText(/17:00–17:20 \(Europe\/London\)/)).toBeVisible();
    expect(within(appointments).getByText(/10:30–10:50 \(Asia\/Dhaka\)/)).toBeVisible();
    expect(within(appointments).getByText('Checked in')).toBeVisible();
    expect(within(appointments).getByText('Booked')).toBeVisible();
  });

  it('U-A13: a visit shows its follow-up date when EMR sent one, and links only when a summary exists', async () => {
    serveDashboard(fullDashboard());
    renderWithClient(<PortalDashboard />);
    const visits = await tile('visits');
    expect(within(visits).getByText(/Follow-up due by 14 Oct 2026/)).toBeVisible();
    expect(within(visits).getAllByText(/Follow-up due by/)).toHaveLength(1);
    expect(
      within(visits).getByRole('link', { name: /View discharge summary, Consultation for Noodle/ }),
    ).toHaveAttribute('href', portalVisitHref(portalIds.visits.withSummary));
    expect(within(visits).getByText(/No discharge summary was written/)).toBeVisible();
    expect(within(visits).getByRole('link', { name: 'Pepper’s visits' })).toHaveAttribute(
      'href',
      portalCatVisitsHref(portalIds.cats.pepper),
    );
  });

  it('U-A16: prescriptions show a count and an end date (or none) and never a medicine or dose', async () => {
    serveDashboard(fullDashboard());
    renderWithClient(<PortalDashboard />);
    const rx = await tile('prescriptions');
    expect(
      within(rx).getByText(/2 active prescriptions · the first ends 18 Oct 2026/),
    ).toBeVisible();
    expect(within(rx).getByText(/1 active prescription · no end date recorded/)).toBeVisible();
    expect(within(rx).getByText(/Medicines and doses are on the prescriptions page/)).toBeVisible();
    expect(within(rx).getAllByRole('link', { name: /^View / })[0]).toHaveAttribute(
      'href',
      PORTAL_PRESCRIPTIONS,
    );
  });
});

describe('Portal dashboard — balances (ADR-FE-122: no payment surface)', () => {
  it('U-A3: balances Unavailable shows no amount and no "Nothing to pay"; Try again refetches the dashboard', async () => {
    let degraded = true;
    const calls = serveDashboard(() => (degraded ? degradedDashboard() : fullDashboard()));
    renderWithClient(<PortalDashboard />);

    const balances = await tile('balances');
    expect(within(balances).getByText(/couldn’t load your balances/i)).toBeVisible();
    expect(within(balances).queryByText(/nothing to pay/i)).toBeNull();
    expect(within(balances).queryByText(/৳|BDT|USD/)).toBeNull();
    // visits is degraded too; the other tiles are intact
    expect(within(await tile('cats')).getByRole('link', { name: 'Noodle' })).toBeVisible();

    degraded = false;
    await userEvent.setup().click(within(balances).getByRole('button', { name: 'Try again' }));
    await waitFor(() => expect(calls.n).toBe(2));
    expect(await screen.findByTestId('outstanding-BDT')).toBeVisible();
    expect(screen.queryByTestId('tile-balances-unavailable')).toBeNull();
  });

  it('U-A4: BDT and USD are two amounts with no total; how to pay and clinic contacts show; no pay control exists', async () => {
    serveDashboard(fullDashboard());
    server.use(
      http.get('*/api/branches', () =>
        HttpResponse.json([
          {
            id: 'b0000000-0000-0000-0000-000000000001',
            name: 'Gulshan',
            addressLine: null,
            phoneNumber: '+880 2 5504 1122',
            email: null,
            timeZoneId: 'Asia/Dhaka',
            weeklyHours: [],
          },
        ]),
      ),
    );
    renderWithClient(<PortalDashboard />);
    const balances = await tile('balances');

    expect(within(balances).getByTestId('outstanding-BDT')).toHaveTextContent('3,250');
    expect(within(balances).getByTestId('outstanding-USD')).toHaveTextContent('$40.00');
    expect(within(balances).getByText(/never added together/i)).toBeVisible();
    expect(within(balances).getByText(/cash or card, or by bank transfer/i)).toBeVisible();
    expect(await within(balances).findByText(/\+880 2 5504 1122/)).toBeVisible();

    // ADR-FE-122: nothing whose name contains "pay" is a button or a link in the whole dashboard
    const controls = [...screen.queryAllByRole('button'), ...screen.queryAllByRole('link')];
    for (const control of controls) {
      expect(control.getAttribute('aria-label') ?? control.textContent ?? '').not.toMatch(/\bpay/i);
    }
    // the only links out of the tile go to invoices (and the billing list)
    for (const link of within(balances).getAllByRole('link')) {
      expect(link.getAttribute('href')).toMatch(new RegExp(`^${PORTAL_BILLING}`));
    }
    expect(within(balances).getByRole('link', { name: 'INV-2026-00318' })).toHaveAttribute(
      'href',
      portalInvoiceHref(portalIds.invoices.overdue),
    );
    expect(within(balances).getByText('Part paid')).toBeVisible();
  });

  it('U-A15: an open-invoice count at the 500 cap reads "500 or more"', async () => {
    const body = fullDashboard();
    if (body.balances?.data) body.balances.data.openInvoiceCount = 500;
    serveDashboard(body);
    renderWithClient(<PortalDashboard />);
    expect(await screen.findByText('500 or more open invoices')).toBeVisible();
  });

  it('U-A15b: a count below the cap is exact', async () => {
    serveDashboard(fullDashboard());
    renderWithClient(<PortalDashboard />);
    expect(await screen.findByText('3 open invoices · oldest due first')).toBeVisible();
  });
});

describe('Portal dashboard — orders', () => {
  it('U-A14: the tracking number shows when present, and the order links to the order page', async () => {
    serveDashboard(fullDashboard());
    renderWithClient(<PortalDashboard />);
    const orders = await tile('orders');
    expect(within(orders).getByText(/Tracking DHK-88213-BD/)).toBeVisible();
    expect(within(orders).getAllByText(/Tracking/)).toHaveLength(1);
    expect(within(orders).getByRole('link', { name: 'SO-2026-00042' })).toHaveAttribute(
      'href',
      portalOrderHref(portalIds.orders.delivered),
    );
  });

  it('U-B7: the order again button follows the server canReorder flag, with the reason when it is false', async () => {
    serveDashboard(fullDashboard());
    renderWithClient(<PortalDashboard />);
    const orders = await tile('orders');
    const pending = within(orders).getByRole('button', { name: 'Order SO-2026-00061 again' });
    expect(pending).toBeDisabled();
    expect(pending).toHaveAccessibleDescription(/once this order has been delivered or closed/i);
    expect(within(orders).getByRole('button', { name: 'Order SO-2026-00042 again' })).toBeEnabled();
  });
});

describe('Portal dashboard — states', () => {
  it('U-A9: pending shows a fixed-height skeleton, then the tiles', async () => {
    serveDashboard(fullDashboard());
    renderWithClient(<PortalDashboard />);
    expect(screen.getByTestId('dashboard-skeleton')).toBeInTheDocument();
    expect(await tile('cats')).toBeVisible();
    expect(screen.queryByTestId('dashboard-skeleton')).toBeNull();
  });

  it('U-A11b: the dashboard never shows a stays or subscriptions tile, nor a placeholder for them', async () => {
    serveDashboard(fullDashboard());
    renderWithClient(<PortalDashboard />);
    await tile('cats');
    expect(screen.queryByRole('heading', { name: /stay|boarding|subscription/i })).toBeNull();
    expect(screen.queryByTestId('tile-stays')).toBeNull();
    expect(screen.queryByTestId('tile-subscriptions')).toBeNull();
  });

  it('U-A12: a delivered section whose key is absent reads "Not available yet", not empty and not an error', async () => {
    serveDashboard(ordersNotDeliveredDashboard());
    renderWithClient(<PortalDashboard />);
    const orders = await tile('orders');
    expect(within(orders).getByText(/Not available yet/)).toBeVisible();
    expect(within(orders).queryByText(/haven’t placed/i)).toBeNull();
    expect(within(orders).queryByRole('button', { name: 'Try again' })).toBeNull();
    expect(screen.queryByTestId('tile-orders-unavailable')).toBeNull();
    expect(within(await tile('cats')).getByRole('link', { name: 'Noodle' })).toBeVisible();
  });

  it('a whole-request failure is an error with a retry, never an empty dashboard', async () => {
    let fail = true;
    server.use(
      http.get('*/api/portal/dashboard', () =>
        fail ? new HttpResponse(null, { status: 500 }) : HttpResponse.json(fullDashboard()),
      ),
    );
    renderWithClient(<PortalDashboard />);
    expect(
      await screen.findByRole('heading', { name: /couldn’t load your home page/i }),
    ).toBeVisible();
    expect(screen.queryByTestId('no-cats')).toBeNull();
    fail = false;
    await userEvent.setup().click(screen.getByRole('button', { name: 'Try again' }));
    expect(await tile('cats')).toBeVisible();
  });

  it('U-A10: the header links of the tiles are the portal-links constants', async () => {
    serveDashboard(fullDashboard());
    renderWithClient(<PortalDashboard />);
    await tile('cats');
    expect(within(await tile('cats')).getByRole('link', { name: /All cats/ })).toHaveAttribute(
      'href',
      PORTAL_CATS,
    );
    expect(
      within(await tile('appointments')).getByRole('link', { name: /All appointments/ }),
    ).toHaveAttribute('href', PORTAL_APPOINTMENTS);
    expect(within(await tile('orders')).getByRole('link', { name: /All orders/ })).toHaveAttribute(
      'href',
      PORTAL_ORDERS,
    );
    expect(
      within(await tile('balances')).getByRole('link', { name: /All invoices/ }),
    ).toHaveAttribute('href', PORTAL_BILLING);
    expect(
      within(await tile('prescriptions')).getByRole('link', { name: /All prescriptions/ }),
    ).toHaveAttribute('href', PORTAL_PRESCRIPTIONS);
  });

  it('U-X1: every enumeration is shown through a label map — no identifier reaches the DOM', async () => {
    serveDashboard(fullDashboard());
    renderWithClient(<PortalDashboard />);
    await tile('orders');
    const text = document.body.textContent ?? '';
    for (const identifier of [
      'PendingPayment',
      'DueSoon',
      'CheckedIn',
      'PartPaid',
      'InConsultation',
      'Overdue Overdue',
    ]) {
      expect(text).not.toContain(identifier);
    }
    expect(text).toContain('Awaiting payment');
    expect(text).toContain('Part paid');
    expect(text).toContain('Checked in');
  });

  it('an unknown status from the wire gets a neutral label and is never echoed', async () => {
    const body = fullDashboard();
    if (body.orders?.data) body.orders.data.items[0]!.status = 'SomethingNew';
    serveDashboard(body);
    renderWithClient(<PortalDashboard />);
    await tile('orders');
    expect(document.body.textContent).not.toContain('SomethingNew');
    expect(screen.getAllByText('Status unavailable').length).toBeGreaterThan(0);
  });
});

describe('Portal dashboard — order dates', () => {
  it('an order placed at 20:00Z on 9 Oct is shown as 10 Oct (Asia/Dhaka), whatever the browser zone', async () => {
    const body = fullDashboard();
    body.orders!.data!.items[0]!.placedOn = '2026-10-09T20:00:00+00:00';
    body.orders!.data!.items[1]!.placedOn = '2026-10-09T17:59:00+00:00';
    serveDashboard(body);
    renderWithClient(<PortalDashboard />);
    const orders = await tile('orders');
    expect(within(orders).getByText(/Placed 10 Oct 2026/)).toBeVisible();
    expect(within(orders).getByText(/Placed 9 Oct 2026/)).toBeVisible();
  });
});
