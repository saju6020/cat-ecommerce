import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { IncidentForm } from '@/components/boarding/incidents/IncidentForm';
import { depositReceivedOf } from '@/lib/format/boarding';
import { DepositStateBadge } from '@/components/boarding/shared/StatusBadges';
import { DepositSummary } from '@/components/boarding/stay/DepositSummary';
import { GateOverrideNote } from '@/components/boarding/stay/GateOverrideNote';
import { server } from '@/mocks/server';
import { ReviewAndReserve } from '@/components/boarding/booking/ReviewAndReserve';
import { EarlyCheckOutPreview } from '@/components/boarding/stay/EarlyCheckOutPreview';
import { StayCharges } from '@/components/boarding/stay/StayCharges';
import { ATTENTION_LABEL } from '@/components/boarding/shared/labels';
import { BOARDING_VIOLATION_MESSAGES } from '@/lib/api/errors';
import { getMyStay, getMyStayDiary, getMyStaySummary, normaliseGate } from '@/lib/api/my-boarding';
import { getStaffStay, saveStaffCareProfile } from '@/lib/api/staff-boarding';
import type { CareProfile, CheckOutPreview } from '@/lib/api/types';
import built from './built-wire.json';

/**
 * HO-BRD-002 gate-fix round (ARCHITECTURE H1/M1/M2, CODE_REVIEW M5, SECURITY lows): a value the backend does not
 * send is shown as absent and never invented; the fc6ef889 alignments; the preview's error and late states.
 */

const API = '*/api';
const money = (amount: string) => ({ amount, currencyCode: 'BDT' });

describe('H1: the absent rule', () => {
  it('keeps a null balanceDue null on the stay view (an Enquiry has no quote)', async () => {
    server.use(
      http.get(`${API}/customers/me/boarding/stays/s1`, () =>
        HttpResponse.json({
          ...built.customerStay,
          status: 'Enquiry',
          quote: null,
          balanceDue: null,
        }),
      ),
    );
    const stay = await getMyStay('s1');
    expect(stay.balanceDue).toBeNull();
    expect(stay.quote).toBeNull();
  });

  it('draws no balance line when there is none, and never prints 0.00 for it', () => {
    render(<StayCharges charges={[]} invoices={[]} balanceDue={null} />);
    expect(screen.queryByText('Balance due')).toBeNull();
    expect(screen.queryByText(/0\.00/)).toBeNull();
  });

  it('shows the summary under the server’s own totals, not an invented balance', async () => {
    server.use(
      http.get(`${API}/customers/me/boarding/stays/s1/summary`, () =>
        HttpResponse.json({
          ...built.summary,
          billing: {
            billedTotal: money('3000.00'),
            unbilledTotal: money('0.00'),
            invoicedOutstanding: money('2100.00'),
            invoiceIds: ['inv-1'],
          },
        }),
      ),
    );
    const summary = await getMyStaySummary('s1');
    expect('balanceDue' in summary).toBe(false);
    expect(summary.billing?.invoicedOutstanding.amount).toBe('2100.00');
    expect(summary.invoices).toEqual([{ id: 'inv-1' }]);
    render(<StayCharges charges={[]} invoices={[]} billing={summary.billing} />);
    expect(screen.getByText('Invoiced, still to pay').nextSibling?.textContent).toMatch(/2,100/);
    expect(screen.queryByText('Balance due')).toBeNull();
  });

  it('does not assume a signature method the report does not carry', async () => {
    const cat = built.summary.cats[0]!;
    server.use(
      http.get(`${API}/customers/me/boarding/stays/s1/summary`, () =>
        HttpResponse.json({
          ...built.summary,
          cats: [
            { ...cat, checkOutSignerName: 'Sam Rahman', checkOutSignedOn: '2026-10-18T09:00:00Z' },
          ],
        }),
      ),
    );
    const summary = await getMyStaySummary('s1');
    expect(summary.cats[0]?.checkOutSignature).toMatchObject({
      signerName: 'Sam Rahman',
      method: null,
    });
  });

  it('does not invent who made a referral', async () => {
    server.use(
      http.get(`${API}/staff/boarding/stays/s1`, () => HttpResponse.json(built.staffStay)),
    );
    const stay = await getStaffStay('s1');
    expect(stay.referrals[0]).toBeDefined();
    expect(stay.referrals[0]?.referredBy ?? null).toBeNull();
  });
});

describe('M1: the BRD-001 fix round (fc6ef889)', () => {
  const profile: CareProfile = {
    feedings: [],
    medications: [],
    litterType: 'Clumping',
    litterNote: null,
    vetVisitAuthorised: true,
    vetSpendingLimit: money('2500.00'),
    emergencyContact: { name: 'Sam', phone: '01711000000' },
    notes: null,
  };

  it('sends vetAuthorityChangeReason only when staff give one', async () => {
    const bodies: Array<Record<string, unknown>> = [];
    server.use(
      http.put(`${API}/staff/boarding/stays/s1/cats/c1/care-profile`, async ({ request }) => {
        bodies.push((await request.json()) as Record<string, unknown>);
        return new HttpResponse(null, { status: 204 });
      }),
    );
    await saveStaffCareProfile('s1', 'c1', profile, 'Owner phoned: raise the limit');
    await saveStaffCareProfile('s1', 'c1', profile);
    expect(bodies[0]?.vetAuthorityChangeReason).toBe('Owner phoned: raise the limit');
    expect(bodies[0]?.vetSpendingLimit).toBe('2500.00');
    expect(bodies[1]).not.toHaveProperty('vetAuthorityChangeReason');
  });

  it('names VetAuthorityReasonRequired and the new attention reason in words', () => {
    expect(BOARDING_VIOLATION_MESSAGES.VetAuthorityReasonRequired).toMatch(/reason/i);
    expect(ATTENTION_LABEL.VaccinationGateBlocked).toMatch(/vaccination/i);
  });

  it('shows an override from gate.overridden and its reason (the wire has no overriddenBy)', async () => {
    const cat = built.staffStay.cats[0]!;
    server.use(
      http.get(`${API}/staff/boarding/stays/s1`, () =>
        HttpResponse.json({
          ...built.staffStay,
          cats: [
            {
              ...cat,
              gate: {
                ...cat.gate,
                status: 'Blocked',
                overridden: true,
                overrideReason: 'Titre test seen',
              },
            },
          ],
        }),
      ),
    );
    const stay = await getStaffStay('s1');
    expect(stay.cats[0]?.gateStatus).toBe('Overridden');
    expect(stay.cats[0]?.gateOverrideReason).toBe('Titre test seen');
  });

  it('rewrites every gate the API sends, not only the enquiry’s', () => {
    const gate = normaliseGate({
      passed: false,
      blockedCats: [
        {
          catId: 'c',
          catName: 'Tiger',
          bookingLink: 'https://x.example/book?catId=c',
          reasons: [],
        },
      ],
    });
    expect(gate.blockedCats[0]?.bookingLink).toBe('/book?catId=c');
  });
});

describe('CODE_REVIEW M5: the check-out preview', () => {
  const preview = (over: Partial<CheckOutPreview> = {}): CheckOutPreview => ({
    isEarly: true,
    isLate: false,
    nightsBooked: 3,
    nightsCharged: 1,
    quote: null,
    lines: [
      { lineRef: 'a', description: 'Nights 1-3', net: money('3000.00'), action: 'Void' },
      { lineRef: 'b', description: 'Night 1', net: money('1000.00'), action: 'Record' },
    ],
    credit: money('2000.00'),
    creditOutcome: 'Credit',
    today: '2026-10-15',
    bookedDepartureOn: '2026-10-18',
    chargedDepartureOn: '2026-10-16',
    unitFreeFrom: '2026-10-15T08:00:00Z',
    previousTotal: money('3000.00'),
    newTotal: money('1000.00'),
    ...over,
  });

  it('says so, and offers a retry, when the preview cannot be loaded', () => {
    let retried = 0;
    render(
      <EarlyCheckOutPreview
        query={{ data: undefined, isPending: false, isError: true }}
        onRetry={() => (retried += 1)}
      />,
    );
    expect(screen.getByRole('alert')).toHaveTextContent(/couldn.t load/i);
    screen.getByRole('button', { name: 'Try again' }).click();
    expect(retried).toBe(1);
  });

  it('shows the credit from the server’s creditOutcome, and nothing when it is None', () => {
    const { unmount } = render(
      <EarlyCheckOutPreview query={{ data: preview(), isPending: false, isError: false }} />,
    );
    expect(screen.getByRole('status')).toHaveTextContent(/credit/i);
    unmount();
    render(
      <EarlyCheckOutPreview
        query={{
          data: preview({ credit: money('0.00'), creditOutcome: 'None' }),
          isPending: false,
          isError: false,
        }}
      />,
    );
    expect(screen.queryByRole('status')).toBeNull();
  });

  it('explains a late check-out (isLate) instead of rendering nothing', () => {
    render(
      <EarlyCheckOutPreview
        query={{
          data: preview({ isEarly: false, isLate: true, nightsCharged: 4, nightsBooked: 3 }),
          isPending: false,
          isError: false,
        }}
      />,
    );
    expect(screen.getByText(/past its booked departure/i)).toBeInTheDocument();
  });
});

describe('CODE_REVIEW M1: Reserve waits for the quote', () => {
  const stay = {
    arrivalOn: '2026-10-20',
    departureOn: '2026-10-22',
    nights: 2,
    requestedUnitType: 'Standard' as const,
    cats: [],
    quote: null,
  };
  it('is disabled while the price is fetched again, and while there is none', () => {
    const { rerender } = render(
      <ReviewAndReserve
        stay={stay}
        quotePending
        onBack={() => undefined}
        onReserve={async () => undefined}
      />,
    );
    expect(screen.getByRole('button', { name: /loading the price/i })).toBeDisabled();
    rerender(
      <ReviewAndReserve stay={stay} onBack={() => undefined} onReserve={async () => undefined} />,
    );
    expect(screen.getByRole('button', { name: 'Reserve' })).toBeDisabled();
  });
});

describe('re-check fixes: UnitOverstayed and the late preview', () => {
  it('labels UnitOverstayed so the banner is never empty', () => {
    expect(ATTENTION_LABEL.UnitOverstayed).toMatch(/previous cat.*move this booking/i);
  });

  it('does not describe a past unitFreeFrom as "free from"', () => {
    render(
      <EarlyCheckOutPreview
        query={{
          data: {
            isEarly: false,
            isLate: true,
            nightsBooked: 3,
            nightsCharged: 4,
            quote: null,
            lines: [],
            credit: money('0.00'),
            creditOutcome: 'None',
            today: '2026-10-20',
            bookedDepartureOn: '2026-10-18',
            chargedDepartureOn: '2026-10-19',
            unitFreeFrom: '2026-01-19T08:00:00Z',
            previousTotal: money('3000.00'),
            newTotal: money('4000.00'),
          },
          isPending: false,
          isError: false,
        }}
      />,
    );
    expect(screen.getByText(/the unit is free now/i)).toBeInTheDocument();
    expect(screen.queryByText(/free from/i)).toBeNull();
  });
});

describe('TEST gate fixes: photo links, the Enquiry deposit, override time, incident time', () => {
  it('anchors a diary photo link on the API origin, not the web origin (row 76)', async () => {
    const day = built.diary.days[0]!;
    server.use(
      http.get(`${API}/customers/me/boarding/stays/s1/diary`, () =>
        HttpResponse.json({
          ...built.diary,
          days: [
            {
              ...day,
              media: [
                {
                  id: 'm1',
                  catId: null,
                  caption: 'Nap',
                  status: 'Visible',
                  file: { fileId: 'f1', url: '/api/files/f1/content?exp=1&sig=abc' },
                },
                {
                  id: 'm2',
                  catId: null,
                  caption: 'Bad',
                  status: 'Visible',
                  file: { fileId: 'f2', url: '//evil.example/x' },
                },
              ],
            },
          ],
        }),
      ),
    );
    const diary = await getMyStayDiary('s1');
    const [good, bad] = diary.days[0]!.media;
    expect(good!.fileUrl).toBe('https://localhost:7099/api/files/f1/content?exp=1&sig=abc');
    expect(bad!.fileUrl).toBeNull();
  });

  it('does not say "No deposit needed" for an Enquiry (row 77)', () => {
    render(
      <DepositSummary
        status="Enquiry"
        deposit={{
          required: money('0.00'),
          accepted: money('0.00'),
          outstanding: money('0.00'),
          state: 'NotRequired',
          payBy: null,
        }}
        balanceDue={null}
        zone="Asia/Dhaka"
      />,
    );
    expect(screen.getByText(/worked out when you reserve/i)).toBeInTheDocument();
    expect(screen.queryByText(/no deposit needed/i)).toBeNull();
    expect(screen.queryByText(/nothing to pay up front/i)).toBeNull();
  });

  it('shows when a gate override was made, when the wire sends it (row 81)', () => {
    render(
      <GateOverrideNote reason="Titre test seen" on="2026-10-15T06:00:00Z" zone="Asia/Dhaka" />,
    );
    expect(screen.getByText(/titre test seen/i)).toHaveTextContent(/15 Oct, 12:00/);
  });

  it('refuses an incident time in the future and sends nothing (row 80)', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    let posted = 0;
    server.use(
      http.post(`${API}/staff/boarding/stays/s1/incidents`, () => {
        posted += 1;
        return HttpResponse.json({ incidentId: 'i' }, { status: 201 });
      }),
    );
    render(
      <QueryClientProvider client={client}>
        <IncidentForm
          open
          onOpenChange={() => undefined}
          stayId="s1"
          cats={[{ catId: 'c1', catName: 'Mitu' }]}
        />
      </QueryClientProvider>,
    );
    await userEvent.type(screen.getByLabelText(/what happened/i), 'Sneezing twice');
    await userEvent.type(screen.getByLabelText(/done straight away/i), 'Monitored');
    fireEvent.change(screen.getByLabelText(/date it happened/i), {
      target: { value: '2999-01-01' },
    });
    await userEvent.click(screen.getByRole('button', { name: 'Record incident' }));
    expect(await screen.findByText(/in the future/i)).toBeInTheDocument();
    expect(posted).toBe(0);
  });
});

describe('ACCEPTANCE row 86: staff screens do not say "No deposit needed" for an Enquiry', () => {
  it('shows "Deposit set at reserve" for an Enquiry and the real state otherwise', () => {
    const { rerender } = render(<DepositStateBadge state="NotRequired" stayStatus="Enquiry" />);
    expect(screen.getByText('Deposit set at reserve')).toBeInTheDocument();
    expect(screen.queryByText(/no deposit needed/i)).toBeNull();
    rerender(<DepositStateBadge state="NotRequired" stayStatus="Confirmed" />);
    expect(screen.getByText('No deposit needed')).toBeInTheDocument();
  });
});

describe('ACCEPTANCE row 96: no "Deposit received 0.00" for an Enquiry', () => {
  it('passes the received deposit only past Enquiry, and the charges draw nothing without it', () => {
    const deposit = { accepted: money('0.00') };
    expect(depositReceivedOf({ status: 'Enquiry', deposit })).toBeUndefined();
    expect(depositReceivedOf({ status: 'Reserved', deposit })).toEqual(money('0.00'));
    render(
      <StayCharges
        charges={[]}
        invoices={[]}
        depositReceived={depositReceivedOf({ status: 'Enquiry', deposit })}
      />,
    );
    expect(screen.queryByText('Deposit received')).toBeNull();
  });
});
