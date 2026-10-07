import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AppointmentDetail } from '@/components/appointments/AppointmentDetail';
import { ClaimOfferedSlot } from '@/components/appointments/ClaimOfferedSlot';
import { MyAppointmentsList } from '@/components/appointments/MyAppointmentsList';
import { WaitlistSection } from '@/components/appointments/WaitlistSection';
import { ApiValidationError } from '@/lib/api/client';
import type { MyAppointmentDetail as Detail, AppointmentSummary } from '@/lib/api/types';
import type { WaitlistEntry } from '@/lib/api/my-appointments';

const CONTACT = {
  name: 'Main clinic',
  phone: '+880 2 1234 5678',
  email: 'main@purrcare.test',
  address: '12 Gulshan Avenue, Dhaka',
};

function detail(overrides: Partial<Detail> = {}): Detail {
  return {
    id: 'a1',
    branchId: 'b1',
    branchTimeZoneId: 'Asia/Dhaka',
    catId: 'cat1',
    catName: 'Mitu',
    service: {
      name: 'Vaccination',
      category: 'Vaccination',
      durationMinutes: 30,
      priceAmount: 800,
      priceCurrencyCode: 'BDT',
    },
    performerId: 'p1',
    performerName: 'Dr Rahman',
    startsOn: '2026-10-12T04:30:00Z',
    endsOn: '2026-10-12T05:00:00Z',
    status: 'Booked',
    holdExpiresOn: null,
    isWaitlistOffer: false,
    reasonForVisit: '<b>Sneezing</b>',
    needsRescheduling: false,
    rescheduleFlagReason: null,
    canChange: true,
    changeCutoffOn: '2026-10-12T00:30:00Z',
    statusHistory: [],
    ...overrides,
  };
}

describe('AppointmentDetail — HO-APT-002 §11.2 (US-CLN-04)', () => {
  it('U-B1: with canChange it offers Reschedule and Cancel and no contact card', async () => {
    const user = userEvent.setup();
    const onCancel = vi.fn().mockResolvedValue(undefined);
    render(
      <AppointmentDetail
        detail={detail()}
        contact={CONTACT}
        onReschedule={vi.fn()}
        onCancel={onCancel}
      />,
    );
    expect(screen.getByRole('button', { name: 'Reschedule' })).toBeInTheDocument();
    expect(screen.queryByText(/can’t be changed online/i)).not.toBeInTheDocument();
    expect(screen.getByText('Mon 12 Oct, 10:30 (Asia/Dhaka)')).toBeInTheDocument();
    // The reason for visit is the customer's text, shown as text.
    expect(screen.getByText('<b>Sneezing</b>')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /cancel appointment/i }));
    const dialog = await screen.findByRole('dialog');
    expect(
      within(dialog).getByText(/will be cancelled and the time released/i),
    ).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: /^cancel appointment$/i }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it('U-B1: without canChange there are no change buttons, only the contact card with a tel: link (AC2)', () => {
    render(
      <AppointmentDetail
        detail={detail({ canChange: false })}
        contact={CONTACT}
        onReschedule={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.queryByRole('button', { name: /reschedule|cancel/i })).not.toBeInTheDocument();
    expect(screen.getByText(/less than 4 hours until your appointment/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /call/i })).toHaveAttribute(
      'href',
      'tel:+880212345678',
    );
    expect(screen.getByRole('link', { name: /email/i })).toHaveAttribute(
      'href',
      'mailto:main@purrcare.test',
    );
  });

  it('a completed appointment shows neither the buttons nor the contact card', () => {
    render(
      <AppointmentDetail
        detail={detail({ canChange: false, status: 'Completed' })}
        contact={CONTACT}
        onReschedule={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.queryByRole('button', { name: /reschedule|cancel/i })).not.toBeInTheDocument();
    expect(screen.queryByText(/can’t be changed online/i)).not.toBeInTheDocument();
  });
});

describe('ClaimOfferedSlot — HO-APT-002 §11.2 (US-CLN-09)', () => {
  beforeEach(() => vi.useFakeTimers({ now: new Date('2026-10-12T04:00:00Z') }));
  afterEach(() => vi.useRealTimers());

  const offer = (expiresInMs: number, status: Detail['status'] = 'Held') =>
    detail({
      status,
      isWaitlistOffer: true,
      holdExpiresOn: new Date(Date.now() + expiresInMs).toISOString(),
    });

  it('U-B3: a live offer shows the countdown and Confirm booking', async () => {
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    render(<ClaimOfferedSlot detail={offer(120_000)} onConfirm={onConfirm} />);
    expect(screen.getByRole('timer')).toHaveTextContent('02:00');
    await act(async () => {
      screen.getByRole('button', { name: /confirm booking/i }).click();
    });
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it('U-B3: when the countdown ends the offer-ended text replaces the confirm button', () => {
    render(<ClaimOfferedSlot detail={offer(3_000)} onConfirm={vi.fn()} />);
    act(() => {
      vi.advanceTimersByTime(4_000);
    });
    expect(screen.getByText('This offer has ended.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /confirm booking/i })).not.toBeInTheDocument();
  });

  it('U-B3: an already-ended offer, and a server SlotHoldExpired, both read "This offer has ended."', async () => {
    const { unmount } = render(
      <ClaimOfferedSlot detail={offer(-1000, 'HoldExpired')} onConfirm={vi.fn()} />,
    );
    expect(screen.getByText('This offer has ended.')).toBeInTheDocument();
    unmount();

    const onConfirm = vi.fn().mockRejectedValue(
      new ApiValidationError({
        isValid: false,
        errors: [
          {
            propertyName: 'AppointmentId',
            errorCode: 'SlotHoldExpired',
            errorMessage: 'x',
            resourceName: 'Clinic',
          },
        ],
      }),
    );
    render(<ClaimOfferedSlot detail={offer(120_000)} onConfirm={onConfirm} />);
    await act(async () => {
      screen.getByRole('button', { name: /confirm booking/i }).click();
    });
    expect(screen.getByText('This offer has ended.')).toBeInTheDocument();
  });
});

describe('MyAppointmentsList and WaitlistSection — HO-APT-002 §11.2', () => {
  const summary: AppointmentSummary = {
    id: 'a1',
    catId: 'c1',
    catName: 'Mitu',
    service: detail().service,
    startsOn: '2026-10-12T04:30:00Z',
    endsOn: '2026-10-12T05:00:00Z',
    performerName: 'Dr Rahman',
    branch: { id: 'b1', name: 'Main clinic', phoneNumber: null },
    branchTimeZoneId: 'Asia/Dhaka',
    status: 'Booked',
    canChange: true,
    changeCutoffOn: null,
  };

  it('lists appointments with status as text and the branch zone named', () => {
    render(
      <MyAppointmentsList
        scope="upcoming"
        onScopeChange={vi.fn()}
        appointments={[summary]}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
      />,
    );
    expect(screen.getByText('Booked')).toBeInTheDocument();
    expect(screen.getByText('Mon 12 Oct, 10:30 (Asia/Dhaka)')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /book another/i })).toHaveAttribute('href', '/book');
  });

  it('has distinct loading, empty and error states', async () => {
    const { rerender } = render(
      <MyAppointmentsList
        scope="past"
        onScopeChange={vi.fn()}
        appointments={undefined}
        isLoading
        isError={false}
        onRetry={vi.fn()}
      />,
    );
    expect(screen.getByRole('status')).toHaveTextContent(/loading appointments/i);
    rerender(
      <MyAppointmentsList
        scope="past"
        onScopeChange={vi.fn()}
        appointments={[]}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
      />,
    );
    expect(screen.getByText('No past appointments yet.')).toBeInTheDocument();
    const onRetry = vi.fn();
    rerender(
      <MyAppointmentsList
        scope="past"
        onScopeChange={vi.fn()}
        appointments={undefined}
        isLoading={false}
        isError
        onRetry={onRetry}
      />,
    );
    await userEvent.click(screen.getByRole('button', { name: /try again/i }));
    expect(onRetry).toHaveBeenCalled();
  });

  it('waitlist: Waiting, Offered (claim-by time) and Lapsed, with Withdraw on the live ones', async () => {
    const user = userEvent.setup();
    const onWithdraw = vi.fn();
    const entry = (status: WaitlistEntry['status'], id: string): WaitlistEntry => ({
      id,
      branchId: 'b1',
      catId: 'c1',
      catName: 'Mitu',
      serviceId: 's1',
      serviceName: 'Vaccination',
      localDate: '2026-10-12',
      status,
      offeredAppointmentId: status === 'Offered' ? 'a9' : null,
      offerExpiresOn: status === 'Offered' ? '2026-10-12T04:45:00Z' : null,
      createdOn: '2026-10-01T00:00:00Z',
    });
    render(
      <WaitlistSection
        entries={[
          entry('Waiting', 'w1'),
          entry('Offered', 'w2'),
          entry('Lapsed', 'w3'),
          entry('Claimed', 'w4'),
        ]}
        zoneByBranchId={{ b1: 'Asia/Dhaka' }}
        onWithdraw={onWithdraw}
        withdrawingId={null}
      />,
    );
    expect(screen.getByText(/claim by 10:45/i)).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /withdraw/i })).toHaveLength(2);
    expect(screen.getByText('Offer lapsed')).toBeInTheDocument();
    expect(screen.queryByText('Claimed')).not.toBeInTheDocument();
    await user.click(screen.getAllByRole('button', { name: /withdraw/i })[0]!);
    expect(onWithdraw).toHaveBeenCalledWith(expect.objectContaining({ id: 'w1' }));
  });
});
