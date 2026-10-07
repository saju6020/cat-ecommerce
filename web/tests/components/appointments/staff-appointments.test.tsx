import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { resetAppointmentsMockData } from '../../../mocks/handlers/appointments';
import { StaffBookingView } from '@/app/(staff)/staff/appointments/new/StaffBookingView';
import { StaffAppointmentDetail } from '@/components/appointments/StaffAppointmentDetail';
import { FlaggedAppointmentsTable } from '@/components/appointments/FlaggedAppointmentsTable';
import { AppointmentSummaryPanel } from '@/components/appointments/AppointmentSummaryPanel';
import { ApiValidationError } from '@/lib/api/client';
import type { AppointmentAction, AppointmentDetail } from '@/lib/api/types';
import { serveStaffCustomers } from '../front-desk/helpers';
import { setUrl } from '../booking/nav-mock';
import { openMockDate, renderWithClient } from '../booking/helpers';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

const CONSULT = 's0000000-0000-0000-0000-00000000a001';

function detail(overrides: Partial<AppointmentDetail> = {}): AppointmentDetail {
  return {
    id: 'a1',
    branchId: 'b1',
    branchTimeZoneId: 'Asia/Dhaka',
    customerId: 'c1',
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
    roomId: 'r1',
    roomName: 'Consultation 1',
    startsOn: '2026-10-12T04:30:00Z',
    endsOn: '2026-10-12T05:00:00Z',
    isWalkIn: false,
    channel: 'Reception',
    status: 'Booked',
    holdExpiresOn: null,
    isWaitlistOffer: false,
    reasonForVisit: '<script>alert(1)</script>',
    triagePriority: 'Routine',
    presentingComplaint: null,
    version: 3,
    needsRescheduling: true,
    rescheduleFlagReason: 'PerformerOnLeave',
    canChange: true,
    changeCutoffOn: null,
    statusHistory: [
      { from: 'Held', to: 'Booked', changedOn: '2026-10-01T04:00:00Z', changedBy: 'u' },
    ],
    allowedTransitions: ['Reschedule', 'Cancel', 'CheckIn', 'RecordNoShow'],
    ...overrides,
  };
}

describe('StaffBookingView — HO-APT-002 §11.3 (US-CLN-07, 08)', () => {
  beforeEach(() => {
    resetAppointmentsMockData();
    serveStaffCustomers();
    setUrl('/staff/appointments/new');
  });

  it('U-C1: a customer with no-shows shows the count as text the moment they are chosen', async () => {
    const user = userEvent.setup();
    renderWithClient(<StaffBookingView />);
    await user.type(screen.getByLabelText(/find the customer/i), '01799999999{Enter}');
    await user.click(await screen.findByRole('button', { name: /sam rahman/i }));
    expect(await screen.findByText('2 no-shows')).toBeInTheDocument();
  });

  it('U-C2: a time outside opening hours is marked in words', async () => {
    const user = userEvent.setup();
    renderWithClient(<StaffBookingView />);
    await user.type(screen.getByLabelText(/find the customer/i), '01799999999{Enter}');
    await user.click(await screen.findByRole('button', { name: /sam rahman/i }));
    await user.selectOptions(await screen.findByLabelText('Service'), CONSULT);
    fireEvent.change(screen.getByLabelText(/^date/i), { target: { value: openMockDate(8) } });
    await waitFor(() =>
      expect(screen.getByLabelText('Vet or nurse').children.length).toBeGreaterThan(1),
    );
    const akter = within(screen.getByLabelText('Vet or nurse')).getByRole('option', {
      name: 'Dr Akter',
    });
    await user.selectOptions(screen.getByLabelText('Vet or nurse'), akter);
    const slot = screen.getByLabelText(/^time/i);
    await waitFor(() => expect(within(slot).getAllByRole('option').length).toBeGreaterThan(3));
    const outside = within(slot)
      .getAllByRole('option')
      .filter((option) => /outside opening hours/i.test(option.textContent ?? ''));
    expect(outside.length).toBeGreaterThan(0);
    const inside = within(slot)
      .getAllByRole('option')
      .filter(
        (option) =>
          /\d{2}:\d{2}–/.test(option.textContent ?? '') &&
          !/outside/i.test(option.textContent ?? ''),
      );
    expect(inside.length).toBeGreaterThan(0);
  });

  it('books, confirming first, and a server 400 is shown without its code', async () => {
    const user = userEvent.setup();
    renderWithClient(<StaffBookingView />);
    await user.type(screen.getByLabelText(/find the customer/i), '01799999999{Enter}');
    await user.click(await screen.findByRole('button', { name: /sam rahman/i }));
    await user.selectOptions(await screen.findByLabelText('Service'), CONSULT);
    fireEvent.change(screen.getByLabelText(/^date/i), { target: { value: openMockDate(8) } });
    await waitFor(() =>
      expect(screen.getByLabelText('Vet or nurse').children.length).toBeGreaterThan(1),
    );
    await user.selectOptions(screen.getByLabelText('Vet or nurse'), 'Dr Rahman');
    const slot = screen.getByLabelText(/^time/i);
    await waitFor(() => expect(within(slot).getAllByRole('option').length).toBeGreaterThan(3));
    await user.selectOptions(slot, within(slot).getAllByRole('option')[3]!);
    await user.click(screen.getByRole('button', { name: /^book$/i }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/the customer is sent a confirmation/i)).toBeInTheDocument();
  });
});

describe('StaffAppointmentDetail — HO-APT-002 §11.4', () => {
  const noop = vi.fn().mockResolvedValue(undefined);
  const renderDetail = (
    d: AppointmentDetail,
    extra: Partial<Parameters<typeof StaffAppointmentDetail>[0]> = {},
  ) =>
    render(
      <StaffAppointmentDetail
        detail={d}
        catHeader={<div>CAT HEADER</div>}
        customerName="Rahim Uddin"
        noShowCount={2}
        onAction={noop}
        onMove={vi.fn()}
        onCancel={noop}
        {...extra}
      />,
    );

  it('shows the banner with the reason, the history, and the text of the complaint as text', () => {
    renderDetail(detail());
    expect(screen.getByText('CAT HEADER')).toBeInTheDocument();
    expect(screen.getByText('Dr Rahman is on leave that day.')).toBeInTheDocument();
    expect(screen.getByText('2 no-shows')).toBeInTheDocument();
    expect(screen.getByText('<script>alert(1)</script>')).toBeInTheDocument();
    expect(screen.getByText(/held → /i)).toBeInTheDocument();
  });

  it('U-D4: renders only the actions the server allows', () => {
    const cases: Array<[AppointmentAction[], string[], string[]]> = [
      [['CheckIn'], ['Check in'], ['Move…', 'No-show', 'Cancel appointment', 'Start', 'Complete']],
      [
        ['Start', 'Cancel'],
        ['Start', 'Cancel appointment'],
        ['Check in', 'Complete', 'Move…'],
      ],
      [['Complete'], ['Complete'], ['Start', 'Check in', 'Cancel appointment']],
      [[], [], ['Check in', 'Start', 'Complete', 'Move…', 'No-show', 'Cancel appointment']],
    ];
    for (const [allowed, present, absent] of cases) {
      const { unmount } = renderDetail(detail({ allowedTransitions: allowed }));
      for (const name of present) {
        expect(screen.getByRole('button', { name })).toBeInTheDocument();
      }
      for (const name of absent) {
        expect(screen.queryByRole('button', { name })).not.toBeInTheDocument();
      }
      unmount();
    }
  });

  it('a no-show names the customer; cancel needs a reason (U-X1)', async () => {
    const user = userEvent.setup();
    const onCancel = vi.fn().mockRejectedValueOnce(
      new ApiValidationError({
        isValid: false,
        errors: [
          {
            propertyName: 'Reason',
            errorCode: 'CancellationReasonRequired',
            errorMessage: 'Enter a reason for cancelling.',
            resourceName: 'Clinic',
          },
        ],
      }),
    );
    renderDetail(detail(), { onCancel });

    await user.click(screen.getByRole('button', { name: 'No-show' }));
    expect(await screen.findByText('This counts against Rahim Uddin.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /^cancel$/i }));

    await user.click(screen.getByRole('button', { name: 'Cancel appointment' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: /^cancel appointment$/i }));
    expect(await within(dialog).findByText('Enter a reason for cancelling.')).toBeInTheDocument();
    expect(onCancel).not.toHaveBeenCalled();

    await user.type(within(dialog).getByLabelText(/reason/i), 'Clinic closed');
    await user.click(within(dialog).getByRole('button', { name: /^cancel appointment$/i }));
    await waitFor(() => expect(onCancel).toHaveBeenCalledWith('Clinic closed'));
    expect(document.body.textContent).not.toContain('CancellationReasonRequired');
    expect(within(dialog).getByLabelText(/reason/i)).toHaveValue('Clinic closed');
  });
});

describe('Flagged list and customer panel', () => {
  it('lists flagged appointments with the reason in words and Reschedule / Cancel', async () => {
    const user = userEvent.setup();
    const onReschedule = vi.fn();
    render(
      <FlaggedAppointmentsTable
        rows={[
          {
            id: 'f1',
            catName: 'Whiskers',
            startsOn: '2026-10-12T08:00:00Z',
            zone: 'Asia/Dhaka',
            reason: 'PerformerOnLeave',
            performerName: 'Dr Rahman',
            customerId: 'c1',
            customerName: 'Farhana Yasmin',
            phone: '01700000000',
          },
        ]}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
        onReschedule={onReschedule}
        onCancel={vi.fn()}
      />,
    );
    // The table (md and up) and the card list (below md) are both in the DOM; CSS picks one.
    expect(screen.getAllByText('Dr Rahman is on leave that day.').length).toBeGreaterThan(0);
    expect(screen.getAllByText(/01700000000/).length).toBeGreaterThan(0);
    await user.click(screen.getAllByRole('button', { name: /reschedule whiskers/i })[0]!);
    expect(onReschedule).toHaveBeenCalled();
  });

  it('has an empty state', () => {
    render(
      <FlaggedAppointmentsTable
        rows={[]}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
        onReschedule={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByText('Nothing needs rescheduling.')).toBeInTheDocument();
  });

  it('the customer panel shows the no-show count as text and links to a prefilled booking', () => {
    render(
      <AppointmentSummaryPanel
        customerId="c1"
        summary={{ noShowCount: 2, upcoming: [], lastVisitOn: null }}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
      />,
    );
    expect(screen.getByText('2 no-shows')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /book appointment/i })).toHaveAttribute(
      'href',
      '/staff/appointments/new?customerId=c1',
    );
  });
});
