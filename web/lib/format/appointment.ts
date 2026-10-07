import { timeOnlyInZone } from './time';

/**
 * `lib/format/appointment.ts` — HO-APT-002 §8.4 additions. Every time the Appointments screens
 * show is the **branch's** time (ADR-FE-014/-015): instants are formatted with `Intl` in
 * `branchTimeZoneId`, and a date the customer or receptionist chooses is a branch-local
 * `yyyy-MM-dd` string — never a browser `Date` (a phone in another zone must not shift the day).
 */

function parts3(dateIso: string): [number, number, number] {
  const [y, m, d] = dateIso.split('-').map(Number);
  return [y ?? NaN, m ?? NaN, d ?? NaN];
}

/** `10:30–11:00`, both ends in the branch zone. */
export function formatSlot(startsOn: string, endsOn: string, zone: string): string {
  return `${timeOnlyInZone(startsOn, zone)}–${timeOnlyInZone(endsOn, zone)}`;
}

/** `Mon 12 Oct, 10:30` in the branch zone. */
export function formatAppointmentWhen(startsOn: string, zone: string): string {
  const date = new Date(startsOn);
  if (Number.isNaN(date.getTime())) return '—';
  const parts = Object.fromEntries(
    new Intl.DateTimeFormat('en-GB', {
      weekday: 'short',
      day: 'numeric',
      month: 'short',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
      timeZone: zone,
    })
      .formatToParts(date)
      .map((part) => [part.type, part.value]),
  );
  return `${parts.weekday} ${parts.day} ${parts.month}, ${parts.hour}:${parts.minute}`;
}

/** `Mon 12 Oct, 10:30–11:00 (Asia/Dhaka)` — the full label with the zone named once. */
export function formatAppointmentRange(startsOn: string, endsOn: string, zone: string): string {
  return `${formatAppointmentWhen(startsOn, zone)}–${timeOnlyInZone(endsOn, zone)} (${zone})`;
}

/** `Thursday 24 September` for a branch-local `yyyy-MM-dd`. */
export function formatBranchDateLong(dateIso: string): string {
  const [y, m, d] = parts3(dateIso);
  if ([y, m, d].some((n) => !Number.isFinite(n))) return dateIso;
  return new Intl.DateTimeFormat('en-GB', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    timeZone: 'UTC',
  }).format(new Date(Date.UTC(y, m - 1, d)));
}

/** Short pieces for a date-strip cell. */
export function dateStripParts(dateIso: string): { weekday: string; day: string; month: string } {
  const [y, m, d] = parts3(dateIso);
  const date = new Date(Date.UTC(y, m - 1, d));
  const part = (options: Intl.DateTimeFormatOptions) =>
    new Intl.DateTimeFormat('en-GB', { ...options, timeZone: 'UTC' }).format(date);
  return {
    weekday: part({ weekday: 'short' }),
    day: part({ day: 'numeric' }),
    month: part({ month: 'short' }),
  };
}

/** `yyyy-MM-dd` plus `days`, calendar arithmetic in UTC (no DST, no browser zone). */
export function addDaysIso(dateIso: string, days: number): string {
  const [y, m, d] = parts3(dateIso);
  const next = new Date(Date.UTC(y, m - 1, d + days));
  return next.toISOString().slice(0, 10);
}

/** Monday of the week containing the branch-local `yyyy-MM-dd`. */
export function startOfWeekIso(dateIso: string): string {
  const [y, m, d] = parts3(dateIso);
  const weekday = new Date(Date.UTC(y, m - 1, d)).getUTCDay(); // 0 = Sunday
  return addDaysIso(dateIso, -((weekday + 6) % 7));
}

/** `count` consecutive branch-local dates starting at `fromIso`. */
export function dateRange(fromIso: string, count: number): string[] {
  return Array.from({ length: count }, (_, i) => addDaysIso(fromIso, i));
}

export type DayPeriod = 'morning' | 'afternoon' | 'evening';

/** Grouping for the slot list (§11.1 SlotStep): before 12:00, 12:00–16:59, 17:00 on — branch time. */
export function dayPeriodOf(startsOn: string, zone: string): DayPeriod {
  const hour = Number(timeOnlyInZone(startsOn, zone).slice(0, 2));
  if (hour < 12) return 'morning';
  if (hour < 17) return 'afternoon';
  return 'evening';
}

/**
 * A branch-local date + `HH:mm` -> the real instant (UTC ISO string). The standard "format a
 * guess in the target zone, measure the drift, correct for it" technique (same as the mocks'
 * `zonedTimeToUtcIso`) — `Intl` only, no time-zone library (ADR-FE-015). Used to turn a drop
 * target or a typed time into the `startsOn` the move endpoint wants; the server still validates it.
 */
export function zonedLocalToInstant(dateIso: string, time: string, zone: string): string {
  const wall = new Date(`${dateIso}T${time}:00Z`).getTime();
  // Zone offset (zone wall clock minus UTC, ms) in force at the instant `at`.
  const offsetAt = (at: number): number => {
    const parts = Object.fromEntries(
      new Intl.DateTimeFormat('en-US', {
        timeZone: zone,
        hourCycle: 'h23',
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
      })
        .formatToParts(new Date(at))
        .map((part) => [part.type, part.value]),
    );
    return (
      Date.UTC(
        Number(parts.year),
        Number(parts.month) - 1,
        Number(parts.day),
        Number(parts.hour),
        Number(parts.minute),
        Number(parts.second),
      ) - at
    );
  };
  // A single correction is wrong when the guess and the answer sit on opposite sides of a
  // transition, so re-measure at the corrected instant and, if the offset differs, correct again.
  const first = wall - offsetAt(wall);
  const second = wall - offsetAt(first);
  const instant = second === first ? first : wall - offsetAt(second) === second ? second : first;
  return new Date(instant).toISOString();
}

/** `mm:ss` for a countdown; never negative. */
export function formatCountdown(remainingMs: number): string {
  const totalSeconds = Math.max(0, Math.ceil(remainingMs / 1000));
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
}

/** "About 35 min" for a queue estimate (5-minute steps from the server, §8.7). */
export function formatEstimatedWait(minutes: number | null): string {
  if (minutes === null) return 'Wait not estimated';
  if (minutes <= 0) return 'Next';
  return `About ${minutes} min`;
}

/**
 * The reason an appointment is flagged for rescheduling (`RescheduleFlagReason`, HO-APT-001
 * §8.10) as a sentence. The wire carries only the enum name, so the vet's name — when the detail
 * has it — is added by the caller.
 */
export function describeRescheduleFlag(
  reason: string | null,
  performerName?: string | null,
): string {
  switch (reason) {
    case 'PerformerOnLeave':
      return `${performerName ?? 'The assigned vet'} is on leave that day.`;
    case 'BranchClosed':
      return 'The clinic is closed at that time.';
    case 'CatDeceased':
      return 'The cat has died, so this appointment is no longer needed.';
    case 'CatOwnerChanged':
      return 'The cat has a new owner, so check who should be told.';
    default:
      return 'This appointment needs a new time.';
  }
}
