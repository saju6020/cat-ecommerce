import { useCalendar } from './useStaffAppointments';

/** The vets and rooms a Move… dialog offers for a day: the calendar's own resource lists for that
 *  branch (read when the dialog is wanted, not polled). The server still judges the choice. */
export function useMoveChoices(date: string, enabled: boolean) {
  const performers = useCalendar(
    { from: date, to: date, view: 'performer' },
    { enabled, poll: false },
  );
  const rooms = useCalendar({ from: date, to: date, view: 'room' }, { enabled, poll: false });
  return {
    performers: performers.data?.resources ?? [],
    rooms: rooms.data?.resources ?? [],
    gridMinutes: performers.data?.gridMinutes ?? 15,
  };
}
