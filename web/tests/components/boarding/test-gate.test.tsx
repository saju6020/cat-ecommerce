import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { http, HttpResponse } from 'msw';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { server } from '@/mocks/server';
import { BookingFlow } from '@/components/boarding/booking/BookingFlow';
import { CareProfileForm } from '@/components/boarding/booking/CareProfileForm';
import { ReservedPanel } from '@/components/boarding/booking/ReservedPanel';
import { CheckInWizard } from '@/components/boarding/stay/CheckInWizard';
import { CheckOutWizard } from '@/components/boarding/stay/CheckOutWizard';
import { createEnquiry, saveCareProfile } from '@/lib/api/my-boarding';
import { getStaffStay } from '@/lib/api/staff-boarding';
import { addDaysIso } from '@/lib/format/appointment';
import { todayInZone } from '@/lib/format/time';
import { boardingFixtureIds as ids, resetBoardingMockData } from '@/mocks/handlers/boarding';
import { EMPTY_CARE_PROFILE, formToCareProfile } from '@/lib/validation/boarding';
import built from './built-wire.json';

/**
 * HO-BRD-002 TEST gate (2026-10-15). Mutation spot-checks showed that the vitest tier did not pin: the form's
 * reason requirement for a vet-authority change (M2), a null balance on the reserved panel (M1c), the quote
 * re-fetch on a reload at the review step (M7), the server deciding early/late in the check-out wizard (M4), and
 * the owner's signature not being kept in the mutation cache after a check-out (M5b). Each test below fails
 * against the matching mutant (recorded in `docs/test-cases/TC-HO-BRD-002.md`, T-M rows).
 */

vi.mock('@/lib/hooks/useBranchScope', () => ({
  useBranchScope: () => ({ branchId: 'b1', ready: true }),
}));
const nav = vi.hoisted(() => ({ query: '' }));
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: () => undefined, push: () => undefined, back: () => undefined }),
  usePathname: () => '/boarding',
  useSearchParams: () => new URLSearchParams(nav.query),
}));

const API = '*/api';
const money = (amount: string) => ({ amount, currencyCode: 'BDT' });
const T = todayInZone('Asia/Dhaka');
const at = (days: number) => addDaysIso(T, days);

function wrap(ui: ReactElement, client?: QueryClient) {
  const queryClient = client ?? new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return {
    queryClient,
    ...render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>),
  };
}

beforeEach(() => {
  nav.query = '';
  resetBoardingMockData();
});
afterEach(() => vi.useRealTimers());

describe('M2: a staff change to the vet authorisation or limit needs a reason', () => {
  const initial = {
    ...EMPTY_CARE_PROFILE,
    feedings: [
      {
        localTime: '08:00',
        portion: '⅓ cup',
        foodSource: 'OwnFood' as const,
        foodDescription: 'Dry',
      },
    ],
    vetVisitAuthorised: 'yes' as const,
    vetSpendingLimit: '2000',
    emergencyName: 'Sam',
    emergencyPhone: '0171',
  };

  it('refuses the save without a reason, then sends the reason with the save', async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    render(
      <CareProfileForm
        catId="c1"
        catName="Mitu"
        currencyCode="BDT"
        onSave={onSave}
        initial={initial}
        requireAuthorityReason
      />,
    );
    // no reason box until the limit (or the authorisation) differs
    expect(screen.queryByLabelText(/reason for changing the vet/i)).toBeNull();
    const limit = screen.getByLabelText(/veterinary spending limit/i);
    await userEvent.clear(limit);
    await userEvent.type(limit, '2500');
    const save = screen.getByRole('button', { name: /save mitu’s care profile/i });
    await userEvent.click(save);
    expect(await screen.findByText(/give a reason for changing the vet/i)).toBeInTheDocument();
    expect(onSave).not.toHaveBeenCalled();

    await userEvent.type(screen.getByLabelText(/reason for changing the vet/i), 'Owner phoned');
    await userEvent.click(save);
    await waitFor(() => expect(onSave).toHaveBeenCalledTimes(1));
    expect(onSave.mock.calls[0]![2]).toEqual({ vetAuthorityChangeReason: 'Owner phoned' });
    expect(onSave.mock.calls[0]![1].vetSpendingLimit).toEqual(money('2500.00'));
  });

  it('asks for nothing and sends no reason when neither value changed', async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    render(
      <CareProfileForm
        catId="c1"
        catName="Mitu"
        currencyCode="BDT"
        onSave={onSave}
        initial={initial}
        requireAuthorityReason
      />,
    );
    await userEvent.type(screen.getByLabelText('Notes (optional)'), 'Shy');
    await userEvent.click(screen.getByRole('button', { name: /save mitu’s care profile/i }));
    await waitFor(() => expect(onSave).toHaveBeenCalledTimes(1));
    expect(onSave.mock.calls[0]![2]).toBeUndefined();
  });
});

describe('M1c: the reserved panel draws no balance when the server sent none', () => {
  it('prints neither a balance line nor a made-up zero', () => {
    wrap(
      <ReservedPanel
        stay={{
          id: 's1',
          branchTimeZoneId: 'Asia/Dhaka',
          balanceDue: null,
          deposit: {
            required: money('2700.00'),
            accepted: money('0.00'),
            outstanding: money('2700.00'),
            state: 'Awaiting',
            payBy: '2999-01-01T10:00:00Z',
          },
        }}
      />,
    );
    expect(screen.queryByText(/balance/i)).toBeNull();
    expect(screen.getByText('Deposit to pay')).toBeInTheDocument();
    expect(screen.queryByText('৳0.00')).toBeNull();
  });
});

describe('M7: a reload at the review step asks the server for the quote again', () => {
  it('shows the price breakdown and enables Reserve for an Enquiry whose stay view has no quote', async () => {
    const profile = formToCareProfile(
      {
        ...EMPTY_CARE_PROFILE,
        feedings: [
          { localTime: '08:00', portion: '¼ cup', foodSource: 'OwnFood', foodDescription: 'Dry' },
        ],
        vetVisitAuthorised: 'yes',
        vetSpendingLimit: '2000',
        emergencyName: 'Sam Rahman',
        emergencyPhone: '01711000000',
      },
      'BDT',
    );
    const created = await createEnquiry({
      branchId: ids.mainBranchId,
      arrivalOn: at(60),
      departureOn: at(62),
      unitType: 'Deluxe',
      catIds: [ids.catMittensId],
    });
    await saveCareProfile(created.stayId, ids.catMittensId, profile);
    nav.query = `stayId=${created.stayId}`;

    wrap(<BookingFlow />);

    expect(await screen.findByRole('heading', { name: 'Review and reserve' })).toBeInTheDocument();
    expect(await screen.findByLabelText('Price breakdown')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reserve' })).toBeEnabled();
  });
});

describe('M4/M5b: the check-out wizard follows the server, and does not keep the signature', () => {
  const preview = (over: Record<string, unknown>) => ({
    ...built.preview,
    isEarly: false,
    isLate: false,
    lines: [],
    credit: money('0.00'),
    creditOutcome: 'None',
    ...over,
  });

  async function stayWith(departureOn: string) {
    server.use(
      http.get(`${API}/staff/boarding/stays/s1`, () =>
        HttpResponse.json({ ...built.staffStay, departureOn }),
      ),
    );
    return getStaffStay('s1');
  }

  it('is not early or late because of the browser’s clock: only the preview says so', async () => {
    // booked departure far in the future: a browser-side comparison would call this an early check-out
    const stay = await stayWith('2999-01-01');
    let served = 0;
    server.use(
      http.get(`${API}/staff/boarding/stays/:id/check-out-preview`, () => {
        served += 1;
        return HttpResponse.json(preview({}));
      }),
    );
    const { unmount } = wrap(<CheckOutWizard stay={stay} onDone={() => undefined} />);
    await waitFor(() => expect(served).toBe(1));
    await new Promise((resolve) => setTimeout(resolve, 100)); // let the answer reach the component
    expect(screen.getByRole('heading', { name: 'Check out' })).toBeInTheDocument();
    expect(screen.queryByRole('note')).toBeNull();
    unmount();

    // and the other way: the server says late, so the note says the stay is past its departure
    server.use(
      http.get(`${API}/staff/boarding/stays/:id/check-out-preview`, () =>
        HttpResponse.json(preview({ isLate: true, nightsCharged: 4 })),
      ),
    );
    wrap(<CheckOutWizard stay={stay} onDone={() => undefined} />);
    expect(await screen.findByRole('note', {}, { timeout: 5000 })).toHaveTextContent(
      /past its booked departure/i,
    );
  });

  it('keeps no signature in the mutation cache once the check-out is recorded', async () => {
    const stay = await stayWith('2999-01-01');
    server.use(
      http.get(`${API}/staff/boarding/stays/:id/check-out-preview`, () =>
        HttpResponse.json(preview({})),
      ),
      http.post(`${API}/staff/boarding/stays/:id/check-out`, () =>
        HttpResponse.json({ stay: { ...built.staffStay, status: 'CheckedOut' } }),
      ),
    );
    const { queryClient } = wrap(<CheckOutWizard stay={stay} onDone={() => undefined} />);
    for (let i = 0; i < stay.cats.length; i += 1) {
      await userEvent.type(await screen.findByLabelText(/^weight/i), '4.2');
      await userEvent.click(screen.getByLabelText(/type your name instead/i));
      await userEvent.type(screen.getByLabelText(/name of the person signing/i), 'Sam Rahman');
      await userEvent.click(screen.getByLabelText(/i confirm i have collected/i));
      await userEvent.click(screen.getByRole('button', { name: /^(Next cat|Review)$/ }));
    }
    await userEvent.click(await screen.findByRole('button', { name: 'Check out' }));
    expect(await screen.findByRole('heading', { name: 'Checked out' })).toBeInTheDocument();
    const kept = queryClient
      .getMutationCache()
      .getAll()
      .filter((m) => JSON.stringify(m.state.variables ?? null).includes('Sam Rahman'));
    expect(kept).toEqual([]);
  });

  it('keeps no signature after a check-in either: not in the summary, not in the mutation cache', async () => {
    const stay = await stayWith('2999-01-01');
    server.use(
      http.post(`${API}/staff/boarding/stays/:id/check-in`, () =>
        HttpResponse.json({ stay: { ...built.staffStay, status: 'CheckedIn' } }),
      ),
    );
    const { queryClient } = wrap(<CheckInWizard stay={stay} onDone={() => undefined} />);
    for (let i = 0; i < stay.cats.length; i += 1) {
      await userEvent.click(await screen.findByLabelText(/i have confirmed this is/i));
      await userEvent.click(screen.getByLabelText(/i have seen proof of the vaccinations/i));
      await userEvent.type(screen.getByLabelText(/^weight/i), '4.2');
      await userEvent.type(screen.getByLabelText(/arrival health observation/i), 'Bright');
      await userEvent.click(screen.getByLabelText(/no belongings/i));
      await userEvent.click(screen.getByLabelText(/type your name instead/i));
      await userEvent.type(screen.getByLabelText(/name of the person signing/i), 'Sam Rahman');
      await userEvent.click(screen.getByLabelText(/i confirm this is my signature/i));
      await userEvent.click(screen.getByRole('button', { name: /^(Next cat|Review)$/ }));
    }
    expect(await screen.findByText(/signed by Sam Rahman/i)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /^check in$/i }));
    // once recorded, the wizard no longer holds the owner's signature anywhere
    await waitFor(() => expect(screen.queryByText(/signed by Sam Rahman/i)).toBeNull());
    const kept = queryClient
      .getMutationCache()
      .getAll()
      .filter((m) => JSON.stringify(m.state.variables ?? null).includes('Sam Rahman'));
    expect(kept).toEqual([]);
  });
});
