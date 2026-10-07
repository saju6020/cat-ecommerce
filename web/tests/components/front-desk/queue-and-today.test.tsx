import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ClinicQueue } from '@/components/front-desk/ClinicQueue';
import { TodayList } from '@/components/front-desk/TodayList';
import type { ClinicQueue as QueueData, QueueEntry } from '@/lib/api/front-desk';
import type { CalendarAppointment } from '@/lib/api/staff-appointments';

function entry(overrides: Partial<QueueEntry>): QueueEntry {
  return {
    appointmentId: 'a',
    position: 1,
    catName: 'Mitu',
    catIsHighRisk: false,
    customerName: 'Rahim Uddin',
    service: 'General consultation',
    triagePriority: 'Routine',
    isWalkIn: true,
    dueOn: '2026-10-12T04:00:00Z',
    checkedInOn: '2026-10-12T04:05:00Z',
    status: 'CheckedIn',
    performerName: null,
    estimatedWaitMinutes: 35,
    ...overrides,
  };
}

const QUEUE: QueueData = {
  generatedOn: '2026-10-12T04:42:00Z',
  performersOnShift: 2,
  entries: [
    entry({
      appointmentId: 'e1',
      position: 1,
      catName: 'Tiger',
      triagePriority: 'Emergency',
      catIsHighRisk: true,
      estimatedWaitMinutes: 0,
    }),
    entry({
      appointmentId: 'e2',
      position: 2,
      catName: 'Biscuit',
      triagePriority: 'Urgent',
      estimatedWaitMinutes: 15,
    }),
    entry({
      appointmentId: 'e3',
      position: 3,
      catName: 'Whiskers',
      isWalkIn: false,
      dueOn: '2026-10-12T04:15:00Z',
      estimatedWaitMinutes: 35,
    }),
  ],
};

const noop = vi.fn();

function renderQueue(allowed: Record<string, never[] | string[] | undefined>, queue = QUEUE) {
  return render(
    <ClinicQueue
      queue={queue}
      zone="Asia/Dhaka"
      isLoading={false}
      isError={false}
      onRetry={noop}
      allowedByAppointmentId={allowed as never}
      onChangePriority={noop}
      onStart={noop}
      onComplete={noop}
    />,
  );
}

describe('ClinicQueue — HO-APT-002 §11.5 (US-CLN-05, 10)', () => {
  it('U-D1: shows the entries in the server’s order with priority as text, estimates and the header', () => {
    renderQueue({ e1: [], e2: [], e3: [] });
    const cards = screen.getAllByRole('listitem');
    expect(cards).toHaveLength(3);
    // The order is exactly the payload's — an Emergency first — and priority is a word.
    expect(within(cards[0]!).getByText('Emergency')).toBeInTheDocument();
    expect(within(cards[0]!).getByText('Tiger')).toBeInTheDocument();
    expect(within(cards[0]!).getByText(/high risk/i)).toBeInTheDocument();
    expect(within(cards[1]!).getByText('Urgent')).toBeInTheDocument();
    expect(within(cards[2]!).getByText('Routine')).toBeInTheDocument();
    expect(within(cards[2]!).getByText(/booked 10:15/i)).toBeInTheDocument();
    expect(within(cards[0]!).getByText(/walk-in/i)).toBeInTheDocument();
    expect(within(cards[2]!).getByText('About 35 min')).toBeInTheDocument();
    expect(within(cards[2]!).getByText(/how is this worked out/i)).toBeInTheDocument();
    expect(screen.getByText(/2 vets on shift · updated 10:42/)).toBeInTheDocument();
  });

  it('U-D4: renders only the actions the server allows for each entry', () => {
    renderQueue({
      e1: ['SetTriagePriority', 'Start', 'Cancel'],
      e2: ['Complete'],
      e3: [], // nothing allowed
    });
    const [first, second, third] = screen.getAllByRole('listitem');
    expect(within(first!).getByRole('combobox', { name: 'Priority' })).toBeInTheDocument();
    expect(within(first!).getByRole('button', { name: /^start/i })).toBeInTheDocument();
    expect(within(first!).queryByRole('button', { name: /complete/i })).not.toBeInTheDocument();
    expect(within(second!).getByRole('button', { name: /^complete/i })).toBeInTheDocument();
    expect(within(second!).queryByRole('button', { name: /^start/i })).not.toBeInTheDocument();
    expect(within(second!).queryByRole('combobox')).not.toBeInTheDocument();
    expect(within(third!).queryByRole('button')).not.toBeInTheDocument();
  });

  it('U-D4: while the allowed actions are still loading it offers none and says so', () => {
    renderQueue({ e1: undefined, e2: undefined, e3: undefined });
    expect(screen.queryByRole('button', { name: /^start|complete/i })).not.toBeInTheDocument();
    expect(screen.getAllByText(/checking what can be done/i).length).toBe(3);
  });

  it('calls the handlers with the entry', async () => {
    const user = userEvent.setup();
    const onStart = vi.fn();
    const onChangePriority = vi.fn();
    render(
      <ClinicQueue
        queue={QUEUE}
        zone="Asia/Dhaka"
        isLoading={false}
        isError={false}
        onRetry={noop}
        allowedByAppointmentId={{ e1: ['SetTriagePriority', 'Start'] }}
        onChangePriority={onChangePriority}
        onStart={onStart}
        onComplete={noop}
      />,
    );
    await user.click(screen.getByRole('button', { name: /^start/i }));
    expect(onStart).toHaveBeenCalledWith(expect.objectContaining({ appointmentId: 'e1' }));
    await user.selectOptions(screen.getByRole('combobox', { name: 'Priority' }), 'Urgent');
    expect(onChangePriority).toHaveBeenCalledWith(
      expect.objectContaining({ appointmentId: 'e1' }),
      'Urgent',
    );
  });

  it('has a distinct empty and error state', () => {
    const { rerender } = renderQueue({}, { ...QUEUE, entries: [] });
    expect(screen.getByText('Nobody is waiting.')).toBeInTheDocument();
    rerender(
      <ClinicQueue
        queue={undefined}
        zone="Asia/Dhaka"
        isLoading={false}
        isError
        onRetry={noop}
        allowedByAppointmentId={{}}
        onChangePriority={noop}
        onStart={noop}
        onComplete={noop}
      />,
    );
    expect(screen.getByText(/couldn’t load the queue/i)).toBeInTheDocument();
  });
});

describe('TodayList — HO-APT-002 §11.5 (US-CLN-05, 08)', () => {
  const row = (id: string, status: CalendarAppointment['status']): CalendarAppointment => ({
    id,
    version: 1,
    catId: 'c',
    catName: `Cat ${id}`,
    customerId: 'u',
    customerName: 'Rahim Uddin',
    serviceName: 'General consultation',
    startsOn: '2026-10-12T04:30:00Z',
    endsOn: '2026-10-12T04:50:00Z',
    performerId: 'p1',
    roomId: 'r1',
    status,
    isWalkIn: false,
    needsRescheduling: false,
    catIsHighRisk: false,
  });

  it('U-D4: Check in and No-show appear only when allowed', () => {
    render(
      <TodayList
        appointments={[row('a', 'Booked'), row('b', 'Booked'), row('c', 'Completed')]}
        zone="Asia/Dhaka"
        isLoading={false}
        isError={false}
        onRetry={noop}
        performerNames={{ p1: 'Dr Rahman' }}
        allowedByAppointmentId={{
          a: ['CheckIn', 'RecordNoShow', 'Cancel'],
          b: ['CheckIn'],
          c: [],
        }}
        onCheckIn={noop}
        onNoShow={noop}
      />,
    );
    expect(screen.getAllByRole('button', { name: /check in/i })).toHaveLength(2);
    expect(screen.getAllByRole('button', { name: /no-show/i })).toHaveLength(1);
    expect(screen.getAllByText(/Dr Rahman/).length).toBe(3);
    expect(screen.getAllByText('10:30').length).toBe(3);
  });
});
