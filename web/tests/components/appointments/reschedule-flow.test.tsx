import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  resetAppointmentsMockData,
  appointmentsFixtureIds,
} from '../../../mocks/handlers/appointments';
import { MyAppointmentView } from '@/app/(portal)/portal/appointments/[appointmentId]/MyAppointmentView';
import { formatBranchDateLong } from '@/lib/format/appointment';
import { openMockDate, renderWithClient } from '../booking/helpers';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

describe('MyAppointmentView — HO-APT-002 §11.2 reschedule and cancel (US-CLN-04)', () => {
  beforeEach(() => resetAppointmentsMockData());

  it('U-B2: a lost race on reschedule explains it and leaves the current time unchanged', async () => {
    const user = userEvent.setup();
    renderWithClient(
      <MyAppointmentView appointmentId={appointmentsFixtureIds.changeableAppointmentId} />,
    );

    await user.click(await screen.findByRole('button', { name: 'Reschedule' }));
    expect(await screen.findByRole('heading', { name: /choose a new time/i })).toBeInTheDocument();
    const currentText = screen.getByText(/your appointment is currently/i).textContent;

    // Pick a Monday–Thursday on the strip so the mock's 10:30 race slot exists.
    await user.click(
      await screen.findByRole('button', {
        name: new RegExp(formatBranchDateLong(openMockDate(1))),
      }),
    );
    await user.click(await screen.findByRole('button', { name: /^10:30/ }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/your current time will be released/i)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: /move appointment/i }));

    expect(await screen.findByText(/someone has just taken that time/i)).toBeInTheDocument();
    expect(screen.getByText(/your current appointment is unchanged/i)).toBeInTheDocument();
    expect(screen.getByText(/your appointment is currently/i).textContent).toBe(currentText);
    await waitFor(() => expect(screen.getByRole('group', { name: /times on/i })).toHaveFocus());
  });

  it('moves the appointment when the new time is free', async () => {
    const user = userEvent.setup();
    renderWithClient(
      <MyAppointmentView appointmentId={appointmentsFixtureIds.changeableAppointmentId} />,
    );

    await user.click(await screen.findByRole('button', { name: 'Reschedule' }));
    await user.click(
      await screen.findByRole('button', {
        name: new RegExp(formatBranchDateLong(openMockDate(1))),
      }),
    );
    await user.click(await screen.findByRole('button', { name: /^14:00/ }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: /move appointment/i }),
    );
    expect(await screen.findByRole('button', { name: 'Reschedule' })).toBeInTheDocument();
    expect(screen.getByText(/14:00/)).toBeInTheDocument();
  });

  it('cancels before the cut-off, then shows the status as Cancelled', async () => {
    const user = userEvent.setup();
    renderWithClient(
      <MyAppointmentView appointmentId={appointmentsFixtureIds.changeableAppointmentId} />,
    );

    await user.click(await screen.findByRole('button', { name: /cancel appointment/i }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', {
        name: /^cancel appointment$/i,
      }),
    );
    expect(await screen.findByText('Cancelled')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reschedule' })).not.toBeInTheDocument();
  });

  it('U-B1: an appointment past its cut-off shows the clinic contact card instead of change buttons', async () => {
    renderWithClient(
      <MyAppointmentView appointmentId={appointmentsFixtureIds.lockedAppointmentId} />,
    );
    expect(
      await screen.findByRole('heading', { name: /can’t be changed online/i }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /reschedule|cancel/i })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: /call/i })).toHaveAttribute(
      'href',
      expect.stringMatching(/^tel:/),
    );
  });

  it('a waitlist offer opens as a claimable held time, and confirming books it (U-B3)', async () => {
    const user = userEvent.setup();
    renderWithClient(
      <MyAppointmentView appointmentId={appointmentsFixtureIds.offerAppointmentId} />,
    );
    expect(await screen.findByRole('timer')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /confirm booking/i }));
    expect(await screen.findByText('Booked')).toBeInTheDocument();
  });

  it('someone else’s appointment is "not found"', async () => {
    renderWithClient(<MyAppointmentView appointmentId="00000000-0000-0000-0000-00000000dead" />);
    expect(await screen.findByText(/couldn’t find that appointment/i)).toBeInTheDocument();
  });
});
