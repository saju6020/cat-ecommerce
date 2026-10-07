import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '../../../mocks/server';
import { resetAppointmentsMockData } from '../../../mocks/handlers/appointments';
import { BookingFlow } from '@/app/(portal)/book/BookingFlow';
import { setUrl, currentUrl } from './nav-mock';
import { openMockDate, renderWithClient, serveSingleCatAndBranch } from './helpers';

vi.mock('next/navigation', () => import('./nav-mock'));

const CONSULT = 's0000000-0000-0000-0000-00000000a001';
const MAIN = 'b0000000-0000-0000-0000-00000000a001';
const MOMO = '20000000-0000-0000-0000-000000000001'; // Whiskers in the cat-profiles mock

function flowUrl(extra: Record<string, string> = {}) {
  const params = new URLSearchParams({
    catId: MOMO,
    serviceId: CONSULT,
    branchId: MAIN,
    date: openMockDate(),
    ...extra,
  });
  return `/book?${params.toString()}`;
}

describe('BookingFlow — HO-APT-002 §11.1 (US-CLN-02, 03)', () => {
  beforeEach(() => {
    resetAppointmentsMockData();
    setUrl('/book');
  });

  it('U-A1: with one cat and one branch the cat and clinic steps are skipped', async () => {
    serveSingleCatAndBranch();
    renderWithClient(<BookingFlow />);

    expect(await screen.findByRole('heading', { name: /choose a service/i })).toBeInTheDocument();
    const steps = screen.getByRole('list', { name: /booking steps/i });
    expect(within(steps).queryByText(/cat$/i)).not.toBeInTheDocument();
    expect(within(steps).queryByText(/clinic/i)).not.toBeInTheDocument();
    expect(within(steps).getByText(/service/i)).toBeInTheDocument();
  });

  it('U-A2: a reminder deep link with a category lands on the service step filtered to it', async () => {
    serveSingleCatAndBranch();
    server.use(
      http.get('*/api/services', () =>
        HttpResponse.json([
          {
            id: CONSULT,
            name: 'General consultation',
            category: 'Consultation',
            durationMinutes: 20,
            isBookableOnline: true,
            currentPrice: { amount: '1500.00', currencyCode: 'BDT' },
          },
          {
            id: 's0000000-0000-0000-0000-00000000a0aa',
            name: 'FVRCP booster',
            category: 'Vaccination',
            durationMinutes: 30,
            isBookableOnline: true,
            currentPrice: { amount: '800.00', currencyCode: 'BDT' },
          },
        ]),
      ),
    );
    setUrl(`/book?catId=${MOMO}&category=Vaccination`);
    renderWithClient(<BookingFlow />);

    expect(await screen.findByText('FVRCP booster', { selector: 'p' })).toBeInTheDocument();
    expect(screen.queryByText('General consultation', { selector: 'p' })).not.toBeInTheDocument();
    expect(screen.getAllByText(/pay at the clinic/i).length).toBeGreaterThan(0);
  });

  it('U-A5: a lost race shows the message, refetches the slots and returns focus to the list', async () => {
    let slotRequests = 0;
    server.use(
      // Counts, then falls through (a resolver that returns nothing passes to the next handler).
      http.get('*/api/booking/slots', () => {
        slotRequests += 1;
      }),
    );
    const user = userEvent.setup();
    setUrl(flowUrl());
    renderWithClient(<BookingFlow />);

    const raced = await screen.findByRole('button', { name: /^10:30/ });
    const before = slotRequests;
    await user.click(raced);

    expect(
      await screen.findByText('Someone has just taken that time. Please choose another.'),
    ).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('group', { name: /times on/i })).toHaveFocus());
    expect(slotRequests).toBeGreaterThan(before);
    // The raced time is gone from the refreshed list; another time is still there.
    await waitFor(() =>
      expect(screen.queryByRole('button', { name: /^10:30 · Dr Rahman/ })).not.toBeInTheDocument(),
    );
  });

  it('U-A9: Back from the confirm step releases the hold (DELETE), and so does leaving the flow', async () => {
    const deletes: string[] = [];
    server.use(
      // Records, then falls through to the mock (which really releases the hold).
      http.delete('*/api/customers/me/appointment-holds/:id', ({ params }) => {
        deletes.push(params.id as string);
      }),
    );
    const user = userEvent.setup();
    setUrl(flowUrl());
    const { unmount } = renderWithClient(<BookingFlow />);

    await user.click(await screen.findByRole('button', { name: /^11:00/ }));
    expect(await screen.findByRole('button', { name: /confirm booking/i })).toBeInTheDocument();
    expect(screen.getByRole('timer')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /^back$/i }));
    await waitFor(() => expect(deletes).toHaveLength(1));
    expect(await screen.findByRole('group', { name: /times on/i })).toBeInTheDocument();

    // Hold again, then leave the route (unmount): released again.
    await user.click(await screen.findByRole('button', { name: /^11:15/ }));
    expect(await screen.findByRole('button', { name: /confirm booking/i })).toBeInTheDocument();
    unmount();
    await waitFor(() => expect(deletes).toHaveLength(2));
  });

  it('U-A8: when the countdown reaches 0 the customer is back at the slots with the expiry message', async () => {
    // A hold that lasts only ~2.5 s, so the real countdown reaches 0 within the test.
    server.use(
      http.post('*/api/customers/me/appointment-holds', async ({ request }) => {
        const body = (await request.json()) as { startsOn: string };
        return HttpResponse.json(
          {
            appointmentId: 'hold-short',
            startsOn: body.startsOn,
            endsOn: body.startsOn,
            performerId: null,
            roomId: null,
            holdExpiresOn: new Date(Date.now() + 2500).toISOString(),
          },
          { status: 201 },
        );
      }),
    );
    const user = userEvent.setup();
    setUrl(flowUrl());
    renderWithClient(<BookingFlow />);

    await user.click(await screen.findByRole('button', { name: /^11:00/ }));
    expect(await screen.findByRole('button', { name: /confirm booking/i })).toBeInTheDocument();

    expect(
      await screen.findByText(
        'Your hold has ended — please choose a time again.',
        {},
        { timeout: 6000 },
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /confirm booking/i })).not.toBeInTheDocument();
    expect(await screen.findByRole('group', { name: /times on/i })).toBeInTheDocument();
  });

  it('W8: the typed reason for visit is still there when the customer holds a new time after the hold ended', async () => {
    server.use(
      http.post('*/api/customers/me/appointment-holds', async ({ request }) => {
        const body = (await request.json()) as { startsOn: string };
        return HttpResponse.json(
          {
            appointmentId: 'hold-short',
            startsOn: body.startsOn,
            endsOn: body.startsOn,
            performerId: null,
            roomId: null,
            holdExpiresOn: new Date(Date.now() + 2500).toISOString(),
          },
          { status: 201 },
        );
      }),
    );
    const user = userEvent.setup();
    setUrl(flowUrl());
    renderWithClient(<BookingFlow />);

    await user.click(await screen.findByRole('button', { name: /^11:00/ }));
    await user.type(await screen.findByLabelText(/reason for visit/i), 'Sneezing a lot');
    expect(
      await screen.findByText(
        'Your hold has ended — please choose a time again.',
        {},
        { timeout: 6000 },
      ),
    ).toBeInTheDocument();

    await user.click(await screen.findByRole('button', { name: /^11:15/ }));
    expect(await screen.findByLabelText(/reason for visit/i)).toHaveValue('Sneezing a lot');
  });

  it('U-A10: the price is information only — "Pay at the clinic" — and nothing in the flow takes payment', async () => {
    const user = userEvent.setup();
    setUrl(flowUrl());
    renderWithClient(<BookingFlow />);

    await user.click(await screen.findByRole('button', { name: /^11:00/ }));
    await screen.findByRole('button', { name: /confirm booking/i });
    expect(screen.getByText(/pay at the clinic/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /pay( now)?\b|checkout|deposit/i })).toBeNull();
    expect(screen.queryByLabelText(/card number|cvv|expiry|payment/i)).toBeNull();
    expect(currentUrl()).not.toMatch(/pay/i);
  });

  it('a confirmed booking ends on "Booked." with Add to calendar', async () => {
    const user = userEvent.setup();
    setUrl(flowUrl());
    renderWithClient(<BookingFlow />);

    await user.click(await screen.findByRole('button', { name: /^11:00/ }));
    await user.type(await screen.findByLabelText(/reason for visit/i), 'Sneezing');
    await user.click(screen.getByRole('button', { name: /confirm booking/i }));

    expect(await screen.findByRole('heading', { name: 'Booked.' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /add to calendar/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /view appointment/i })).toBeInTheDocument();
  });

  it('U-A4: a full day offers the waitlist and the next day with times', async () => {
    const user = userEvent.setup();
    const { specialDays } = await import('../../../mocks/handlers/appointments');
    setUrl(flowUrl({ date: specialDays().fullDay }));
    renderWithClient(<BookingFlow />);

    expect(await screen.findByText(/no times left on/i)).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: /join the waitlist/i })).toBeInTheDocument();
    expect(await screen.findByText(/the next day with times is/i)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /join the waitlist/i }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: /join the waitlist/i }));
    expect(await within(dialog).findByText(/30 minutes to claim it/i)).toBeInTheDocument();
  });
});
