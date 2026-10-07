import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  act,
  createEvent,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http } from 'msw';
import { server } from '../../../mocks/server';
import {
  appointmentsFixtureIds as ids,
  resetAppointmentsMockData,
} from '../../../mocks/handlers/appointments';
import { findAppointment } from '../../../mocks/handlers/appointments/store';
import { CalendarView } from '@/app/(staff)/staff/appointments/CalendarView';
import { CalendarAgenda } from '@/components/calendar/CalendarAgenda';
import { CalendarGrid } from '@/components/calendar/CalendarGrid';
import { minutesAtOffset } from '@/components/calendar/layout';
import { useFrozenWhile } from '@/lib/hooks/useFrozenWhile';
import { addDaysIso } from '@/lib/format/appointment';
import { todayInZone } from '@/lib/format/time';
import type { CalendarPayload } from '@/lib/api/staff-appointments';
import { setUrl } from '../booking/nav-mock';
import { renderWithClient } from '../booking/helpers';

vi.mock('next/navigation', () => import('../booking/nav-mock'));

/** The day the mock seeds Tiger (Dr Rahman 10:00) and Whiskers (Dr Akter 11:30) on. */
function seededDay(): string {
  let date = addDaysIso(todayInZone('Asia/Dhaka'), 1);
  while (![1, 2, 3, 4].includes(new Date(`${date}T00:00:00Z`).getUTCDay()))
    date = addDaysIso(date, 1);
  return date;
}

const PAYLOAD: CalendarPayload = {
  gridMinutes: 15,
  resources: [
    { id: 'p1', name: 'Dr Rahman' },
    { id: 'p2', name: 'Dr Akter' },
  ],
  availability: [
    {
      staffAccountId: 'p1',
      start: '2026-10-12T03:00:00Z',
      end: '2026-10-12T11:00:00Z',
      outsideOpeningHours: false,
    },
    {
      staffAccountId: 'p2',
      start: '2026-10-12T02:00:00Z',
      end: '2026-10-12T13:00:00Z',
      outsideOpeningHours: false,
    },
  ],
  closures: [
    {
      startsOn: '2026-10-12',
      endsOn: '2026-10-12',
      kind: 'PublicHoliday',
      reason: 'Public holiday',
    },
  ],
  appointments: [
    {
      id: 'a1',
      version: 2,
      catId: 'c1',
      catName: 'Tiger',
      customerId: 'u1',
      customerName: 'Rahim Uddin',
      serviceName: 'Vaccination',
      startsOn: '2026-10-12T04:00:00Z',
      endsOn: '2026-10-12T04:30:00Z',
      performerId: 'p1',
      roomId: 'r1',
      status: 'Booked',
      isWalkIn: false,
      needsRescheduling: true,
      catIsHighRisk: true,
    },
  ],
};

describe('CalendarGrid and CalendarAgenda — HO-APT-002 §11.6', () => {
  it('U-E6: off-shift time and closures are distinguishable without colour — pattern plus words', () => {
    const { container } = render(
      <CalendarGrid
        payload={PAYLOAD}
        view="performer"
        dates={['2026-10-12']}
        zone="Asia/Dhaka"
        pending={null}
        onDropAppointment={vi.fn()}
        onMoveDialog={vi.fn()}
        onNewAt={vi.fn()}
      />,
    );
    const offShift = container.querySelectorAll('[data-off-shift]');
    expect(offShift.length).toBeGreaterThan(0);
    offShift.forEach((el) => {
      expect((el as HTMLElement).style.backgroundImage).toContain('repeating-linear-gradient');
      expect(el.textContent).toBe('Off shift');
    });
    const closures = container.querySelectorAll('[data-closure]');
    expect(closures.length).toBe(2);
    expect(closures[0]!.textContent).toBe('Closed — Public holiday');
    expect((closures[0] as HTMLElement).style.backgroundImage).toContain(
      'repeating-linear-gradient',
    );
  });

  it('shows the flag and high-risk markers in words on the block, and a Move… button', () => {
    render(
      <CalendarGrid
        payload={PAYLOAD}
        view="performer"
        dates={['2026-10-12']}
        zone="Asia/Dhaka"
        pending={null}
        onDropAppointment={vi.fn()}
        onMoveDialog={vi.fn()}
        onNewAt={vi.fn()}
      />,
    );
    const block = screen.getByRole('group', { name: /tiger, vaccination, 10:00/i });
    expect(within(block).getByText('Needs a new time')).toBeInTheDocument();
    expect(within(block).getByText('High risk')).toBeInTheDocument();
    expect(within(block).getByRole('button', { name: /move tiger/i })).toBeInTheDocument();
  });

  it('shows "Moving…" at the target while a move is pending', () => {
    render(
      <CalendarGrid
        payload={PAYLOAD}
        view="performer"
        dates={['2026-10-12']}
        zone="Asia/Dhaka"
        pending={{
          appointmentId: 'a1',
          target: { resourceId: 'p2', dateIso: '2026-10-12', minutes: 11 * 60 },
        }}
        onDropAppointment={vi.fn()}
        onMoveDialog={vi.fn()}
        onNewAt={vi.fn()}
      />,
    );
    expect(screen.getByText('Moving…')).toBeInTheDocument();
  });

  it('U-E5: the agenda is a time-ordered list with Move… on every item, closures as text, no grid', async () => {
    const user = userEvent.setup();
    const onMove = vi.fn();
    const { container } = render(
      <CalendarAgenda
        payload={PAYLOAD}
        dates={['2026-10-12']}
        zone="Asia/Dhaka"
        onMoveDialog={onMove}
      />,
    );
    expect(container.querySelector('table')).toBeNull();
    expect(container.querySelector('[data-calendar-column]')).toBeNull();
    expect(screen.getByText('Closed — Public holiday')).toBeInTheDocument();
    expect(screen.getByText('10:00–10:30')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /move tiger/i }));
    expect(onMove).toHaveBeenCalledWith(expect.objectContaining({ id: 'a1' }));
  });

  it('U-E3: a value that changes during a drag is applied after the drop, not during', () => {
    function Probe({ value, frozen }: { value: string; frozen: boolean }) {
      return <p>{useFrozenWhile(value, frozen)}</p>;
    }
    const { rerender } = render(<Probe value="v1" frozen={false} />);
    rerender(<Probe value="v1" frozen />);
    rerender(<Probe value="v2-from-the-poll" frozen />);
    expect(screen.getByText('v1')).toBeInTheDocument();
    rerender(<Probe value="v2-from-the-poll" frozen={false} />);
    expect(screen.getByText('v2-from-the-poll')).toBeInTheDocument();
  });
});

describe('CalendarView — moves are the server’s decision (US-CLN-07)', () => {
  const movePosts: Array<Record<string, unknown>> = [];

  beforeEach(() => {
    resetAppointmentsMockData();
    movePosts.length = 0;
    setUrl(`/staff/appointments?date=${seededDay()}`);
    server.use(
      http.post('*/api/staff/appointments/:id/move', async ({ request }) => {
        movePosts.push((await request.clone().json()) as Record<string, unknown>);
      }),
    );
  });

  async function dragTigerOntoAkterAt1130() {
    const block = await screen.findByRole('group', { name: /tiger, general consultation, 10:00/i });
    const akterColumn = document.querySelector(
      `[data-calendar-column][data-resource-id="${ids.vetAkterId}"]`,
    ) as HTMLElement;
    expect(akterColumn).toBeTruthy();
    document.elementsFromPoint = () => [akterColumn];
    return { block };
  }

  it('U-E1: a drop onto a busy time is refused by the server; the block snaps back and the reason is read out', async () => {
    const user = userEvent.setup();
    renderWithClient(<CalendarView />);
    const { block } = await dragTigerOntoAkterAt1130();

    // Work out the pointer offset for 11:30 from the rendered hour labels.
    const first = screen.getAllByText(/^\d{2}:00$/)[0]!.textContent!;
    const startMinutes = Number(first.slice(0, 2)) * 60;
    const y = ((11 * 60 + 30 - startMinutes) / 15) * 20 + 2;
    expect(minutesAtOffset(y, { startMinutes, endMinutes: 24 * 60 }, 15)).toBe(11 * 60 + 30);

    const down = createEvent.pointerDown(block, { button: 0, clientX: 5, clientY: 5 });
    fireEvent(block, down);
    const moveEvent = new MouseEvent('pointermove', { clientX: 60, clientY: y, bubbles: true });
    const upEvent = new MouseEvent('pointerup', { clientX: 60, clientY: y, bubbles: true });
    await act(async () => {
      window.dispatchEvent(moveEvent);
    });
    await act(async () => {
      window.dispatchEvent(upEvent);
    });

    // Same confirmation as the dialog: old and new are stated.
    const dialog = await screen.findByRole('dialog');
    expect(
      within(dialog).getByText(
        /move tiger’s general consultation from 10:00 with dr rahman to 11:30 with dr akter/i,
      ),
    ).toBeInTheDocument();

    server.resetHandlers();
    await user.click(within(dialog).getByRole('button', { name: /move appointment/i }));
    await waitFor(() =>
      expect(
        screen.getAllByText(/dr akter already has an appointment 11:30–11:50/i).length,
      ).toBeGreaterThan(0),
    );
    // Snapped back: nothing is left "Moving…", Tiger is still at 10:00 in Dr Rahman's column.
    expect(screen.queryByText('Moving…')).not.toBeInTheDocument();
    expect(
      screen.getByRole('group', { name: /tiger, general consultation, 10:00/i }),
    ).toBeInTheDocument();
    // The live region carries the same reason (US-CLN-07 AC2).
    const live = screen
      .getAllByRole('status')
      .find((el) => /not moved/i.test(el.textContent ?? ''));
    expect(live).toBeTruthy();
  });

  /** Drops Tiger on Dr Akter (11:30 is busy; 15:00 is free) and returns once the confirmation opens. */
  async function dropTigerOnAkter(atMinutes = 11 * 60 + 30) {
    const { block } = await dragTigerOntoAkterAt1130();
    const first = screen.getAllByText(/^\d{2}:00$/)[0]!.textContent!;
    const startMinutes = Number(first.slice(0, 2)) * 60;
    const y = ((atMinutes - startMinutes) / 15) * 20 + 2;
    fireEvent(block, createEvent.pointerDown(block, { button: 0, clientX: 5, clientY: 5 }));
    await act(async () => {
      window.dispatchEvent(
        new MouseEvent('pointermove', { clientX: 60, clientY: y, bubbles: true }),
      );
    });
    await act(async () => {
      window.dispatchEvent(new MouseEvent('pointerup', { clientX: 60, clientY: y, bubbles: true }));
    });
    return screen.findByRole('dialog');
  }

  function tigerRow() {
    const row = findAppointment(ids.movableAppointmentId);
    expect(row).toBeTruthy();
    return row!;
  }

  const STALE_MESSAGE = 'This appointment changed since you loaded the calendar.';

  it('U-E2 / W6: a stale version (409) says so, refetches, and the retry resends the FRESH version', async () => {
    const user = userEvent.setup();
    let calendarGets = 0;
    server.use(
      http.get('*/api/staff/appointments/calendar', () => {
        calendarGets += 1;
      }),
    );
    const { client } = renderWithClient(<CalendarView />);
    await user.click((await screen.findAllByRole('button', { name: /move tiger/i }))[0]!);
    const dialog = await screen.findByRole('dialog');
    await waitFor(() =>
      expect(within(dialog).getByLabelText('Vet or nurse').children.length).toBeGreaterThan(1),
    );

    // Someone else changes Tiger while the dialog is open: the server is now at version 5.
    tigerRow().version = 5;
    await user.clear(within(dialog).getByLabelText(/^time/i));
    await user.type(within(dialog).getByLabelText(/^time/i), '15:00');
    await user.click(within(dialog).getByRole('button', { name: /review move/i }));
    const getsBefore = calendarGets;
    await user.click(within(dialog).getByRole('button', { name: /move appointment/i }));
    expect(await within(dialog).findByText(STALE_MESSAGE)).toBeInTheDocument();
    expect(movePosts[0]).toMatchObject({ expectedVersion: 1 });

    // The calendar was refetched ...
    await waitFor(() => expect(calendarGets).toBeGreaterThan(getsBefore));
    await waitFor(() => expect(client.isFetching()).toBe(0));

    // ... and the second attempt from the SAME dialog sends the fresh version and succeeds.
    await user.click(within(dialog).getByRole('button', { name: /review move/i }));
    await user.click(within(dialog).getByRole('button', { name: /move appointment/i }));
    await waitFor(() => expect(movePosts).toHaveLength(2));
    expect(movePosts[1]).toMatchObject({ expectedVersion: 5 });
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(tigerRow().version).toBe(6);
  });

  it('W6 (drop path): a change another staff member made, brought in by a poll while the confirmation is open, is NOT sent silently', async () => {
    const user = userEvent.setup();
    const { client } = renderWithClient(<CalendarView />);
    const dialog = await dropTigerOnAkter(15 * 60);
    tigerRow().version = 5;
    await client.invalidateQueries({ queryKey: ['staff', 'calendar'] });
    await waitFor(() => expect(client.isFetching()).toBe(0));

    await user.click(within(dialog).getByRole('button', { name: /move appointment/i }));
    await waitFor(() => expect(movePosts).toHaveLength(1));
    // The version the drop was made with: the server refuses it, so the concurrency check works.
    expect(movePosts[0]).toMatchObject({ expectedVersion: 1 });
    await waitFor(() =>
      expect(screen.getAllByText(new RegExp(STALE_MESSAGE)).length).toBeGreaterThan(0),
    );
    expect(tigerRow().version).toBe(5);
  });

  it('W6 (drop path): a stale drop is refused in the server’s words, snaps back, and the next drop sends the fresh version', async () => {
    const user = userEvent.setup();
    const { client } = renderWithClient(<CalendarView />);
    const dialog = await dropTigerOnAkter(15 * 60);
    tigerRow().version = 5; // changed elsewhere, and this client has not heard yet
    await user.click(within(dialog).getByRole('button', { name: /move appointment/i }));
    await waitFor(() =>
      expect(screen.getAllByText(new RegExp(STALE_MESSAGE)).length).toBeGreaterThan(0),
    );
    expect(movePosts[0]).toMatchObject({ expectedVersion: 1 });
    expect(screen.queryByText('Moving…')).not.toBeInTheDocument();
    await waitFor(() => expect(client.isFetching()).toBe(0));

    const second = await dropTigerOnAkter(15 * 60);
    await user.click(within(second).getByRole('button', { name: /move appointment/i }));
    await waitFor(() => expect(movePosts).toHaveLength(2));
    expect(movePosts[1]).toMatchObject({ expectedVersion: 5 });
    await waitFor(() => expect(tigerRow().version).toBe(6));
  });

  it('W15: "Moving…" is shown at the target the user CONFIRMED, not the one dropped on', async () => {
    const user = userEvent.setup();
    server.use(
      http.post('*/api/staff/appointments/:id/move', async () => {
        await delay('infinite');
      }),
    );
    renderWithClient(<CalendarView />);
    const dialog = await dropTigerOnAkter();
    await user.click(within(dialog).getByRole('button', { name: /^back$/i }));
    await waitFor(() =>
      expect(within(dialog).getByLabelText('Vet or nurse').children.length).toBeGreaterThan(1),
    );
    await user.selectOptions(within(dialog).getByLabelText('Vet or nurse'), ids.vetRahmanId);
    await user.click(within(dialog).getByRole('button', { name: /review move/i }));
    await user.click(within(dialog).getByRole('button', { name: /move appointment/i }));

    const column = (id: string) =>
      document.querySelector(`[data-calendar-column][data-resource-id="${id}"]`) as HTMLElement;
    await waitFor(() => expect(within(column(ids.vetRahmanId)).getByText('Moving…')).toBeVisible());
    expect(within(column(ids.vetAkterId)).queryByText('Moving…')).not.toBeInTheDocument();
  });

  it('W15: before the room list loads the confirmation leaves the vet/room clauses out instead of "the same"', async () => {
    server.use(
      http.get('*/api/staff/appointments/calendar', async ({ request }) => {
        if (new URL(request.url).searchParams.get('view') === 'room') await delay('infinite');
      }),
    );
    renderWithClient(<CalendarView />);
    const dialog = await dropTigerOnAkter();
    const sentence = within(dialog).getByText(/^move tiger’s general consultation from/i);
    expect(sentence.textContent).not.toMatch(/the same/i);
    expect(sentence.textContent).toMatch(/from 10:00 with dr rahman to 11:30 with dr akter on /i);
    expect(sentence.textContent).not.toMatch(/ in /);
  });

  it('U-E4: Move… in the keyboard dialog sends the request a drop would (version, start, vet, room)', async () => {
    const user = userEvent.setup();
    renderWithClient(<CalendarView />);
    await user.click((await screen.findAllByRole('button', { name: /move tiger/i }))[0]!);
    const dialog = await screen.findByRole('dialog');
    await waitFor(() =>
      expect(within(dialog).getByLabelText('Vet or nurse').children.length).toBeGreaterThan(1),
    );
    await user.clear(within(dialog).getByLabelText(/^time/i));
    await user.type(within(dialog).getByLabelText(/^time/i), '15:00');
    await user.selectOptions(within(dialog).getByLabelText('Vet or nurse'), ids.vetAkterId);
    await user.click(within(dialog).getByRole('button', { name: /review move/i }));
    expect(within(dialog).getByText(/to 15:00 with dr akter/i)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: /move appointment/i }));
    await waitFor(() => expect(movePosts).toHaveLength(1));
    expect(movePosts[0]).toMatchObject({
      expectedVersion: 1,
      performerId: ids.vetAkterId,
      roomId: ids.roomAId,
    });
    expect(String(movePosts[0]!.startsOn)).toMatch(/T09:00:00/); // 15:00 in Dhaka is 09:00Z
  });
});
