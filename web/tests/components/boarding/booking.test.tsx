import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { CareProfileForm } from '@/components/boarding/booking/CareProfileForm';
import { CatStep } from '@/components/boarding/booking/CatStep';
import { ReservedPanel } from '@/components/boarding/booking/ReservedPanel';
import { ReviewAndReserve } from '@/components/boarding/booking/ReviewAndReserve';
import { UnitTypeResults } from '@/components/boarding/booking/UnitTypeResults';
import { VaccinationGatePanel } from '@/components/boarding/booking/VaccinationGatePanel';
import { PayByCountdown } from '@/components/boarding/shared/PayByCountdown';
import { QuoteBreakdown } from '@/components/boarding/shared/QuoteBreakdown';
import { recordServerDate, resetServerClockSamples } from '@/lib/api/client';
import type { BoardingAvailability, StayQuote } from '@/lib/api/types';
import { EMPTY_CARE_PROFILE } from '@/lib/validation/boarding';

/** HO-BRD-002 §15 U-A series: the booking components, with the server's answers as props. */

const money = (amount: string) => ({ amount, currencyCode: 'BDT' });

const quote20: StayQuote = {
  basis: 'Weekly',
  units: [
    {
      sequence: 1,
      kind: 'Week',
      fromNight: 1,
      toNight: 7,
      description: 'Week 1 (nights 1–7) — Deluxe weekly rate',
      amount: money('9000.00'),
    },
    {
      sequence: 2,
      kind: 'Week',
      fromNight: 8,
      toNight: 14,
      description: 'Week 2 (nights 8–14) — Deluxe weekly rate',
      amount: money('9000.00'),
    },
    {
      sequence: 3,
      kind: 'Night',
      fromNight: 15,
      toNight: 20,
      description: '6 nights (nights 15–20) — Deluxe nightly rate',
      amount: money('9000.00'),
    },
  ],
  discountLines: [{ description: 'Long-stay discount 10% — week 1', amount: money('-900.00') }],
  total: money('25200.00'),
};

function wrap(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const availability = (free: number[]): BoardingAvailability => ({
  branchTimeZoneId: 'Asia/Dhaka',
  nights: 5,
  occupiedFrom: '2026-10-20T08:00:00Z',
  occupiedTo: '2026-10-25T08:00:00Z',
  waitlistAvailable: free.every((n) => n === 0),
  alternatives: free.every((n) => n === 0)
    ? [
        {
          arrivalOn: '2026-10-27',
          departureOn: '2026-11-01',
          unitType: 'Standard',
          total: money('5000.00'),
        },
      ]
    : [],
  unitTypes: [
    {
      unitType: 'Standard',
      capacityCats: 1,
      freeUnitCount: free[0]!,
      quote: { ...quote20, basis: 'Nightly', discountLines: [] },
    },
    { unitType: 'Deluxe', capacityCats: 2, freeUnitCount: free[1]!, quote: quote20 },
    {
      unitType: 'Family',
      capacityCats: 3,
      freeUnitCount: free[2]!,
      quote: { ...quote20, basis: 'Nightly', discountLines: [] },
    },
  ],
});

describe('U-A1/A2/A3: results and the quote', () => {
  it('renders one card per type with capacity, free count and total; the discount is its own line', () => {
    render(
      <UnitTypeResults
        availability={availability([2, 1, 1])}
        catCount={1}
        onChoose={vi.fn()}
        onPickAlternative={vi.fn()}
        onJoinWaitlist={vi.fn()}
      />,
    );
    const types = screen.getByRole('list', { name: 'Unit types' });
    expect(
      within(types)
        .getAllByRole('heading', { level: 3 })
        .map((h) => h.textContent),
    ).toEqual(['Standard', 'Deluxe', 'Family']);
    expect(within(types).getByText(/Holds up to 2 cats/)).toBeInTheDocument();
    expect(within(types).getAllByText('1 free')).toHaveLength(2);
    expect(within(types).getByText('Long-stay discount 10% — week 1')).toBeInTheDocument();
  });

  it('QuoteBreakdown shows the basis label, each unit, the discount apart, and the total as sent', () => {
    render(<QuoteBreakdown quote={quote20} />);
    expect(screen.getByText('Weekly rate')).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')).toHaveLength(4);
    expect(screen.getByText('Total').parentElement).toHaveTextContent('৳25,200.00');
  });

  it('a full range shows the nearest dates (each a one-tap search) and the waitlist', async () => {
    const onPick = vi.fn();
    const onJoin = vi.fn();
    render(
      <UnitTypeResults
        availability={availability([0, 0, 0])}
        catCount={1}
        onChoose={vi.fn()}
        onPickAlternative={onPick}
        onJoinWaitlist={onJoin}
      />,
    );
    expect(screen.getByText('No units free for those dates.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /search 27[–-]1|search/i }));
    expect(onPick).toHaveBeenCalledWith(expect.objectContaining({ arrivalOn: '2026-10-27' }));
    await userEvent.click(screen.getByRole('button', { name: 'Join the waitlist' }));
    expect(onJoin).toHaveBeenCalled();
  });

  it('disables a type that is too small, with the reason', () => {
    render(
      <UnitTypeResults
        availability={availability([1, 1, 1])}
        catCount={3}
        onChoose={vi.fn()}
        onPickAlternative={vi.fn()}
        onJoinWaitlist={vi.fn()}
      />,
    );
    const standard = screen.getByRole('button', { name: /choose standard/i });
    expect(standard).toBeDisabled();
    expect(screen.getByText(/holds up to 1 cat; you are booking 3/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /choose family/i })).toBeEnabled();
  });
});

describe('U-A4: capacity', () => {
  const cats = ['a', 'b', 'c'].map((id) => ({
    id,
    ownerId: 'o',
    name: id.toUpperCase(),
    species: 'Cat',
    breed: null,
    sex: 'Female' as const,
    isHighRisk: false,
    handlingNote: null,
    lifeStatus: 'Alive' as const,
  }));

  it('blocks Continue with the capacity stated, and shows the server message verbatim', () => {
    render(
      <CatStep
        cats={cats}
        selectedIds={['a', 'b', 'c']}
        onToggle={vi.fn()}
        capacity={2}
        unitLabel="Deluxe"
        onContinue={vi.fn()}
        onBack={vi.fn()}
        pending={false}
        serverError="A Deluxe unit holds up to 2 cats. You have chosen 3."
      />,
    );
    expect(
      screen.getAllByText(/holds up to 2 cats\. you have chosen 3/i).length,
    ).toBeGreaterThanOrEqual(1);
    expect(screen.getByRole('button', { name: 'Continue' })).toBeDisabled();
  });
});

describe('U-A5: the vaccination gate', () => {
  it('explains each blocked cat in plain language and links to the vaccination booking', () => {
    render(
      <VaccinationGatePanel
        resuming={false}
        onResume={vi.fn()}
        gate={{
          passed: false,
          blockedCats: [
            {
              catId: 'c1',
              catName: 'Mitu',
              bookingLink: '/book?catId=c1&category=Vaccination',
              reasons: [
                { reason: 'DueBeforeDeparture', vaccineName: 'Rabies', dueOn: '2026-10-14' },
              ],
            },
          ],
        }}
      />,
    );
    expect(
      screen.getByText(
        /Mitu’s rabies vaccination vaccination is due on|Mitu’s rabies vaccination is due on 14 Oct 2026/i,
      ),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /book a vaccination for mitu/i })).toHaveAttribute(
      'href',
      '/book?catId=c1&category=Vaccination',
    );
    expect(screen.getByText(/we’ve kept your booking/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Resume' })).toBeInTheDocument();
  });

  it('never follows an off-site booking link', () => {
    render(
      <VaccinationGatePanel
        resuming={false}
        onResume={vi.fn()}
        gate={{
          passed: false,
          blockedCats: [
            { catId: 'c1', catName: 'Mitu', bookingLink: 'https://evil.example/x', reasons: [] },
          ],
        }}
      />,
    );
    expect(screen.queryByRole('link')).toBeNull();
  });
});

describe('U-A7: the care profile keeps values and names what is wrong', () => {
  const sevenFeedings = Array.from({ length: 7 }, (_, i) => ({
    localTime: `0${i + 1}:00`,
    portion: 'x',
    foodSource: 'OwnFood' as const,
    foodDescription: 'y',
  }));

  it('shows field errors for a missing limit, seven feedings and a medicine without a time, and keeps what was typed', async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    render(
      <CareProfileForm
        catId="c1"
        catName="Mitu"
        currencyCode="BDT"
        onSave={onSave}
        initial={{
          ...EMPTY_CARE_PROFILE,
          feedings: sevenFeedings,
          medications: [
            {
              name: 'Metacam',
              dose: '0.5 ml',
              route: 'Oral',
              times: '',
              instructions: '',
              fromOn: '',
              toOn: '',
            },
          ],
          vetVisitAuthorised: 'yes',
          emergencyName: 'Sam',
          emergencyPhone: '0171',
          notes: 'Shy at first',
        }}
      />,
    );
    await userEvent.click(screen.getByRole('button', { name: /save mitu’s care profile/i }));
    expect(await screen.findByText(/enter a spending limit/i)).toBeInTheDocument();
    expect(
      screen.getByText(/up to 6 feedings a day\./i, { selector: 'p[role="alert"]' }),
    ).toBeInTheDocument();
    expect(screen.getByText(/add at least one time/i)).toBeInTheDocument();
    expect(screen.getByLabelText('Notes (optional)')).toHaveValue('Shy at first');
    expect(onSave).not.toHaveBeenCalled();
  });

  it('saves a complete profile with the limit as text', async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    render(
      <CareProfileForm
        catId="c1"
        catName="Mitu"
        currencyCode="BDT"
        onSave={onSave}
        initial={{
          ...EMPTY_CARE_PROFILE,
          feedings: [
            { localTime: '08:00', portion: '⅓ cup', foodSource: 'OwnFood', foodDescription: 'Dry' },
          ],
          vetVisitAuthorised: 'yes',
          vetSpendingLimit: '2000',
          emergencyName: 'Sam',
          emergencyPhone: '0171',
        }}
      />,
    );
    await userEvent.click(screen.getByRole('button', { name: /save mitu’s care profile/i }));
    expect(await screen.findByText(/care profile is saved/i)).toBeInTheDocument();
    expect(onSave.mock.calls[0]![1].vetSpendingLimit).toEqual({
      amount: '2000.00',
      currencyCode: 'BDT',
    });
  });
});

describe('U-A9/A10: reserved, the countdown and no payment control', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: false });
    resetServerClockSamples();
  });
  afterEach(() => {
    vi.useRealTimers();
    resetServerClockSamples();
  });

  it('counts to the server’s time when the device clock is 90 s behind, and announces at 24 h, 1 h and expiry', () => {
    vi.setSystemTime(new Date('2026-10-14T10:00:00Z'));
    for (let i = 0; i < 5; i += 1) recordServerDate(new Date(Date.now() + 90_000).toUTCString());
    // The server says it is 10:01:30; pay-by is 11:02:30 -> 1 h 1 min on the server clock (not on the device's).
    render(<PayByCountdown payBy="2026-10-14T11:02:30Z" zone="Asia/Dhaka" />);
    expect(screen.getByTestId('pay-by-left')).toHaveTextContent(/1 h 1 min left/);
    expect(screen.getByRole('status')).toBeEmptyDOMElement();
    act(() => {
      vi.advanceTimersByTime(61_000);
    });
    expect(screen.getByRole('status')).toHaveTextContent(/less than 1 hour left/i);
    act(() => {
      vi.advanceTimersByTime(3_600_000);
    });
    expect(screen.getByRole('status')).toHaveTextContent(/time to pay the deposit has ended/i);
    expect(screen.getByTestId('pay-by-left')).toHaveTextContent('time is up');
  });

  it('the reserved panel states not-yet-confirmed and has no payment control', () => {
    vi.setSystemTime(new Date('2026-10-14T10:00:00Z'));
    wrap(
      <ReservedPanel
        stay={{
          id: 's1',
          branchTimeZoneId: 'Asia/Dhaka',
          balanceDue: money('9000.00'),
          deposit: {
            required: money('2700.00'),
            accepted: money('0.00'),
            outstanding: money('2700.00'),
            state: 'Awaiting',
            payBy: '2026-10-16T10:00:00Z',
          },
        }}
      />,
    );
    expect(
      screen.getByRole('heading', { name: /reserved — not yet confirmed/i }),
    ).toBeInTheDocument();
    expect(screen.getByText('৳2,700.00')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /pay/i })).toBeNull();
    expect(document.querySelector('input[autocomplete^="cc-"]')).toBeNull();
  });
});

describe('UI-3: reserve names what is affected', () => {
  it('opens a confirm with the unit, cats, dates and total, and only then reserves', async () => {
    const onReserve = vi.fn().mockResolvedValue(undefined);
    render(
      <ReviewAndReserve
        onBack={vi.fn()}
        onReserve={onReserve}
        stay={{
          arrivalOn: '2026-10-12',
          departureOn: '2026-10-19',
          nights: 7,
          requestedUnitType: 'Family',
          quote: { ...quote20, total: money('9000.00') },
          cats: [
            {
              catId: 'c1',
              catName: 'Mitu',
              catIsHighRisk: false,
              careProfile: null,
              careProfileComplete: false,
              gateStatus: 'Passed',
              gateReasons: [],
              suspectedInfectious: false,
            },
          ],
        }}
      />,
    );
    await userEvent.click(screen.getByRole('button', { name: 'Reserve' }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent(
      /reserve family for mitu, 12–19 oct 2026 · 7 nights, ৳9,000\.00/i,
    );
    expect(onReserve).not.toHaveBeenCalled();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Reserve' }));
    expect(onReserve).toHaveBeenCalledTimes(1);
  });
});
