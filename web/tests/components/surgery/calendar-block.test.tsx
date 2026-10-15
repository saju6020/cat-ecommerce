import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { CalendarGrid } from '@/components/calendar/CalendarGrid';
import type { CalendarPayload } from '@/lib/api/staff-appointments';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);

const PAYLOAD: CalendarPayload = {
  gridMinutes: 15,
  resources: [
    { id: 'p1', name: 'Dr Rahman' },
    { id: 'p2', name: 'Nurse Tanvir' },
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
      start: '2026-10-12T03:00:00Z',
      end: '2026-10-12T11:00:00Z',
      outsideOpeningHours: false,
    },
  ],
  closures: [],
  appointments: [
    {
      id: 'appt-surgery',
      version: 1,
      catId: 'c1',
      catName: 'Pepper',
      customerId: 'u1',
      customerName: 'Farhana Yasmin',
      serviceName: 'Spay',
      startsOn: '2026-10-12T04:00:00Z',
      endsOn: '2026-10-12T05:00:00Z',
      performerId: 'p1',
      roomId: 'r1',
      status: 'Booked',
      isWalkIn: false,
      needsRescheduling: false,
      catIsHighRisk: false,
      surgeryId: 'srg-1',
      assistantId: 'p2',
    },
  ],
} as unknown as CalendarPayload;

function grid(onDrop = vi.fn(), onMove = vi.fn()) {
  render(
    <CalendarGrid
      payload={PAYLOAD}
      view="performer"
      dates={['2026-10-12']}
      zone="Asia/Dhaka"
      pending={null}
      onDropAppointment={onDrop}
      onMoveDialog={onMove}
      onNewAt={vi.fn()}
    />,
  );
  return { onDrop, onMove };
}

describe('U-X3 — a surgery block on the calendar', () => {
  it('opens the surgery page, says Surgery, and names the assistant', () => {
    grid();
    const links = screen.getAllByRole('link', { name: 'Pepper' });
    expect(links.length).toBe(2); // the surgeon's column and the assistant's
    for (const link of links) expect(link).toHaveAttribute('href', '/staff/surgeries/srg-1');
    expect(screen.getAllByText(/Surgery · Spay · Booked/).length).toBe(2);
    expect(screen.getAllByText('with Nurse Tanvir').length).toBeGreaterThan(0);
  });

  it('has no Move… button and cannot be dragged', () => {
    const { onDrop, onMove } = grid();
    expect(screen.queryByRole('button', { name: /Move/ })).not.toBeInTheDocument();
    const block = screen.getAllByRole('group', { name: /Pepper, Spay/ })[0]!;
    fireEvent.pointerDown(block, { clientX: 10, clientY: 10, pointerId: 1, button: 0 });
    fireEvent.pointerMove(window, { clientX: 10, clientY: 200, pointerId: 1 });
    fireEvent.pointerUp(window, { clientX: 10, clientY: 200, pointerId: 1 });
    expect(onDrop).not.toHaveBeenCalled();
    expect(onMove).not.toHaveBeenCalled();
    expect(within(block).queryByRole('button')).not.toBeInTheDocument();
  });
});
