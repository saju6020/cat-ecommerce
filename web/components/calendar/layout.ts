import { localDateOf } from '@/lib/format/time';
import type { CalendarPayload } from '@/lib/api/staff-appointments';

/**
 * `components/calendar/layout.ts` — pure geometry for the calendar grid (HO-APT-002 §11.6). Nothing
 * here decides whether a time is *available*: it only turns instants into minutes-from-midnight in
 * the branch zone, and minutes into pixel offsets on the slot grid. The server answers every
 * availability question.
 */

/** Pixels per slot-grid row (one `gridMinutes` step). */
export const ROW_PX = 20;

const MINUTES_PER_DAY = 24 * 60;

/** Minutes after local midnight of an instant, in the branch zone. */
export function minutesOfDay(instant: string, zone: string): number {
  const parts = Object.fromEntries(
    new Intl.DateTimeFormat('en-GB', {
      hour: '2-digit',
      minute: '2-digit',
      hourCycle: 'h23',
      timeZone: zone,
    })
      .formatToParts(new Date(instant))
      .map((part) => [part.type, part.value]),
  );
  return Number(parts.hour) * 60 + Number(parts.minute);
}

export interface VisibleRange {
  startMinutes: number;
  endMinutes: number;
}

/**
 * OQ-2: the earliest shift start to the latest shift end of the payload, ±1 h, rounded out to whole
 * hours; 08:00–19:00 when nobody is rostered. Appointments outside it widen it, so none is hidden.
 */
export function visibleRange(payload: CalendarPayload, zone: string): VisibleRange {
  const starts: number[] = [];
  const ends: number[] = [];
  for (const interval of payload.availability) {
    starts.push(minutesOfDay(interval.start, zone));
    ends.push(minutesOfDay(interval.end, zone) || MINUTES_PER_DAY);
  }
  for (const appointment of payload.appointments) {
    if (appointment.startsOn && appointment.endsOn) {
      starts.push(minutesOfDay(appointment.startsOn, zone));
      ends.push(minutesOfDay(appointment.endsOn, zone) || MINUTES_PER_DAY);
    }
  }
  if (starts.length === 0) return { startMinutes: 8 * 60, endMinutes: 19 * 60 };
  return {
    startMinutes: Math.max(0, Math.floor((Math.min(...starts) - 60) / 60) * 60),
    endMinutes: Math.min(MINUTES_PER_DAY, Math.ceil((Math.max(...ends) + 60) / 60) * 60),
  };
}

export function rowsIn(range: VisibleRange, gridMinutes: number): number {
  return Math.ceil((range.endMinutes - range.startMinutes) / gridMinutes);
}

/** Vertical position and height (px) of a block between two minutes-of-day. */
export function blockBox(
  startMinutes: number,
  endMinutes: number,
  range: VisibleRange,
  gridMinutes: number,
): { top: number; height: number } {
  const top = ((startMinutes - range.startMinutes) / gridMinutes) * ROW_PX;
  const height = Math.max(((endMinutes - startMinutes) / gridMinutes) * ROW_PX, ROW_PX * 1.5);
  return { top, height };
}

/** The parts of the visible day a performer is NOT on shift: the complement of their intervals. */
export function offShiftSegments(
  payload: CalendarPayload,
  performerId: string,
  dateIso: string,
  zone: string,
  range: VisibleRange,
): Array<{ startMinutes: number; endMinutes: number }> {
  const worked = payload.availability
    .filter((a) => a.staffAccountId === performerId && localDateOf(a.start, zone) === dateIso)
    .map((a) => ({
      start: minutesOfDay(a.start, zone),
      end: minutesOfDay(a.end, zone) || MINUTES_PER_DAY,
    }))
    .sort((a, b) => a.start - b.start);
  const segments: Array<{ startMinutes: number; endMinutes: number }> = [];
  let cursor = range.startMinutes;
  for (const interval of worked) {
    if (interval.start > cursor) {
      segments.push({
        startMinutes: cursor,
        endMinutes: Math.min(interval.start, range.endMinutes),
      });
    }
    cursor = Math.max(cursor, interval.end);
  }
  if (cursor < range.endMinutes)
    segments.push({ startMinutes: cursor, endMinutes: range.endMinutes });
  return segments.filter((s) => s.endMinutes > s.startMinutes);
}

export function hhmm(minutes: number): string {
  return `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`;
}

/** The pointer's vertical offset inside a column -> minutes-of-day snapped DOWN to the grid. */
export function minutesAtOffset(offsetY: number, range: VisibleRange, gridMinutes: number): number {
  const rows = Math.max(0, Math.floor(offsetY / ROW_PX));
  return Math.min(range.endMinutes - gridMinutes, range.startMinutes + rows * gridMinutes);
}
