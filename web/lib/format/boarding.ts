import { timeOnlyInZone } from './time';

/**
 * `lib/format/boarding.ts` — HO-BRD-002 §8.4 additions. Every Boarding time is the **branch's**
 * time (ADR-FE-014/-015); every date chosen anywhere is a branch-local `yyyy-MM-dd` string, never a
 * `Date` (a phone in another zone must not shift the day). Nothing here computes money, overdue
 * state or availability (ADR-FE-105) — `formatOverdue` formats the minutes the SERVER sent.
 */

function parts(dateIso: string): [number, number, number] {
  const [y, m, d] = dateIso.split('-').map(Number);
  return [y ?? NaN, m ?? NaN, d ?? NaN];
}

function utcDate(dateIso: string): Date | null {
  const [y, m, d] = parts(dateIso);
  if ([y, m, d].some((n) => !Number.isFinite(n))) return null;
  return new Date(Date.UTC(y, m - 1, d));
}

/** Whole nights between two branch-local dates — calendar arithmetic on date strings, shown before
 *  the server has answered (the search step). The price and availability are never derived from it. */
/** The deposit received, or undefined for an Enquiry (the API sends 0.00 before the stay is reserved, which is not
 *  "nothing received" - the deposit does not exist yet). */
export function depositReceivedOf<T extends { status: string; deposit: { accepted: B } }, B>(
  stay: T,
): B | undefined {
  return stay.status === 'Enquiry' ? undefined : stay.deposit.accepted;
}

export function nightsBetween(arrivalOn: string, departureOn: string): number {
  const a = utcDate(arrivalOn);
  const b = utcDate(departureOn);
  if (!a || !b) return 0;
  return Math.round((b.getTime() - a.getTime()) / 86_400_000);
}

function fmt(date: Date, options: Intl.DateTimeFormatOptions): string {
  return new Intl.DateTimeFormat('en-GB', { ...options, timeZone: 'UTC' }).format(date);
}

/** `12–19 Oct 2026 · 7 nights`; `28 Oct – 3 Nov 2026 · 6 nights` across a month; one night is
 *  singular. `nights` is the server's value when it has one. A `DateOnly` has no zone to convert. */
export function formatStayDates(arrivalOn: string, departureOn: string, nights?: number): string {
  const a = utcDate(arrivalOn);
  const b = utcDate(departureOn);
  if (!a || !b) return `${arrivalOn} – ${departureOn}`;
  const count = nights ?? nightsBetween(arrivalOn, departureOn);
  const noun = count === 1 ? 'night' : 'nights';
  const sameMonth =
    a.getUTCMonth() === b.getUTCMonth() && a.getUTCFullYear() === b.getUTCFullYear();
  const range = sameMonth
    ? `${fmt(a, { day: 'numeric' })}–${fmt(b, { day: 'numeric', month: 'short', year: 'numeric' })}`
    : `${fmt(a, { day: 'numeric', month: 'short' })} – ${fmt(b, {
        day: 'numeric',
        month: 'short',
        year: 'numeric',
      })}`;
  return `${range} · ${count} ${noun}`;
}

/** `Mon 12 Oct` for a branch-local `yyyy-MM-dd`. */
export function formatShortDate(dateIso: string): string {
  const d = utcDate(dateIso);
  if (!d) return dateIso;
  return fmt(d, { weekday: 'short', day: 'numeric', month: 'short' });
}

/** `Mon 12 Oct 2026`. */
export function formatFullDate(dateIso: string): string {
  const d = utcDate(dateIso);
  if (!d) return dateIso;
  return fmt(d, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' });
}

/** `09:00` for a task's due instant, in the branch zone. */
export function formatDueTime(dueOn: string, zone: string): string {
  return timeOnlyInZone(dueOn, zone);
}

/** `25 min late`, `1 h 5 min late`. The minutes come from the server's `minutesLate` (ADR-FE-105);
 *  without them the chip still says the task is overdue. */
export function formatOverdue(minutesLate: number | null | undefined): string {
  if (minutesLate == null || minutesLate <= 0) return 'Overdue';
  if (minutesLate < 60) return `${minutesLate} min late`;
  const hours = Math.floor(minutesLate / 60);
  const rest = minutesLate % 60;
  return rest === 0 ? `${hours} h late` : `${hours} h ${rest} min late`;
}

/** `Check-in 14:00–19:00 (Asia/Dhaka)` for a branch-local wall-clock window. */
export function formatCheckInWindow(window: { start: string; end: string }, zone: string): string {
  return `Check-in ${window.start}–${window.end} (${zone})`;
}

/** `Mon 12 Oct, 21:14` for an instant, in the branch zone. */
export function formatBranchInstant(instant: string, zone: string): string {
  const date = new Date(instant);
  if (Number.isNaN(date.getTime())) return '—';
  const p = Object.fromEntries(
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
  return `${p.weekday} ${p.day} ${p.month}, ${p.hour}:${p.minute}`;
}

/** `10:42` — the dashboard's *updated* stamp, in the branch zone. */
export function formatUpdatedAt(instant: string, zone: string): string {
  return timeOnlyInZone(instant, zone);
}

/** `1 d 3 h`, `2 h 5 min`, `04:12` — what is left to pay in, from a remaining duration in ms. The
 *  duration is measured against the SERVER's clock (`useServerNow`), never the device's. */
export function formatTimeLeft(remainingMs: number): string {
  if (remainingMs <= 0) return 'time is up';
  const totalMinutes = Math.floor(remainingMs / 60_000);
  if (totalMinutes >= 24 * 60) {
    const days = Math.floor(totalMinutes / (24 * 60));
    const hours = Math.floor((totalMinutes % (24 * 60)) / 60);
    return hours > 0 ? `${days} d ${hours} h left` : `${days} d left`;
  }
  if (totalMinutes >= 60) {
    return `${Math.floor(totalMinutes / 60)} h ${totalMinutes % 60} min left`;
  }
  const seconds = Math.ceil(remainingMs / 1000);
  const mm = String(Math.floor(seconds / 60)).padStart(2, '0');
  const ss = String(seconds % 60).padStart(2, '0');
  return `${mm}:${ss} left`;
}
