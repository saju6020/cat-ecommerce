/**
 * `lib/format/time.ts` — HO-ADM-002 §8.4, ADR-FE-015 (branch time is rendered with
 * `Intl.DateTimeFormat` in the branch's IANA zone; no time-zone library).
 *
 * Two different kinds of "time" appear in this area, and they are formatted differently on
 * purpose:
 *  - a real **instant** (`timestamptz` on the wire — an audit-log row, a calendar open interval,
 *    a price/rate `effectiveFrom`) genuinely depends on which zone you view it in: `formatInZone`
 *    converts it with `Intl.DateTimeFormat`'s own `timeZone` option.
 *  - a branch's **wall-clock** value (an opening-hours `Opens`/`Closes`, a shift `Starts`/`Ends`)
 *    is already local to the branch — there is nothing to convert, only to format and label so it
 *    is never mistaken for the browser's own zone. `zonedLocalToLabel` does that.
 *
 * Every time label this area renders ends with the zone name (§8.4) — `timeFieldLabel` is the one
 * place that builds that suffix so it reads identically everywhere.
 */

/** `Intl.supportedValuesOf('timeZone')` is available on every runtime this app targets (Node 20 /
 *  evergreen browsers); this is the one, single call site for it (`BranchDetailsForm`'s `<select>`,
 *  ADR-FE-015) rather than a hand-maintained IANA list. */
export function timeZoneOptions(): string[] {
  if (typeof Intl.supportedValuesOf === 'function') {
    return Intl.supportedValuesOf('timeZone');
  }
  // Extremely defensive fallback — every real target runtime has `supportedValuesOf`; this only
  // guards a test environment that doesn't, so the branch's own already-selected zone is not lost.
  return ['Asia/Dhaka', 'Europe/London', 'UTC'];
}

/** Parses a `sep`-delimited numeric string (`"2026-10-01"`, `"09:30"`) into a fixed-length tuple
 *  of `number`s, `NaN`-filled on a short/malformed input — `noUncheckedIndexedAccess` makes a bare
 *  `.split(sep).map(Number)` destructure `number | undefined` per element even though the caller
 *  knows the expected length, so every date/time parse in this module goes through this once. */
function parseIntParts(value: string, sep: string, count: number): number[] {
  const parts = value.split(sep);
  return Array.from({ length: count }, (_, i) => {
    const part = parts[i];
    return part === undefined ? NaN : Number(part);
  });
}

/** A field label with its time zone named, e.g. `"Opens (Asia/Dhaka)"` (§8.4). */
export function timeFieldLabel(label: string, timeZoneId: string): string {
  return `${label} (${timeZoneId})`;
}

/**
 * Formats a genuine instant (ISO-8601 with offset, or a `Date`) in the given IANA zone.
 * `options` defaults to a medium date + short time; pass `{ timeStyle: 'short' }` alone for a
 * time-only display (e.g. a calendar interval already grouped by day).
 */
export function formatInZone(
  instant: string | Date,
  timeZoneId: string,
  options: Intl.DateTimeFormatOptions = { dateStyle: 'medium', timeStyle: 'short' },
): string {
  const date = typeof instant === 'string' ? new Date(instant) : instant;
  if (Number.isNaN(date.getTime())) return '—';
  const formatted = new Intl.DateTimeFormat('en-GB', { ...options, timeZone: timeZoneId }).format(
    date,
  );
  return `${formatted} (${timeZoneId})`;
}

/** A plain date (no zone suffix), in the browser's own zone, for content/policy timestamps that
 *  are not branch-specific (content, policies and their publish dates apply everywhere, unlike a
 *  branch's opening hours) — ADR-FE-015 only requires the zone to be named for branch-local times. */
export function formatInstantDate(instant: string): string {
  const date = new Date(instant);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium' }).format(date);
}

/** The `HH:mm` portion only, in the given zone, with no `(zone)` suffix — for a roster/calendar
 *  cell that already states its zone once, in a heading, rather than repeating it per interval. */
export function timeOnlyInZone(instant: string, timeZoneId: string): string {
  const date = new Date(instant);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('en-GB', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone: timeZoneId,
  }).format(date);
}

/** The `yyyy-MM-dd` local calendar date an instant falls on on the given zone — used to place a
 *  roster/availability interval (an instant) under the right day column, since intervals never
 *  cross midnight in this domain (HO-ADM-001 §8.2/§8.4). */
export function localDateOf(instant: string, timeZoneId: string): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: timeZoneId }).format(new Date(instant));
}

/**
 * A branch-local wall-clock date (`yyyy-MM-dd`) and time (`HH:mm`) pair, formatted for display and
 * labelled with the zone it is already expressed in — never converted, since the pair is already
 * local to `timeZoneId` (ADR-FE-015). Building a `Date` from the components as if they were UTC,
 * then formatting with `timeZone: 'UTC'`, is a formatting trick only — it never claims the
 * instant this produces is a real UTC instant, it just reuses `Intl.DateTimeFormat`'s calendar
 * math for text layout without a second, unwanted zone conversion.
 */
export function zonedLocalToLabel(dateIso: string, time: string, timeZoneId: string): string {
  const [year, month, day] = parseIntParts(dateIso, '-', 3) as [number, number, number];
  const [hour, minute] = parseIntParts(time, ':', 2) as [number, number];
  if ([year, month, day, hour, minute].some((n) => !Number.isFinite(n))) return '—';
  const asUtc = new Date(Date.UTC(year, month - 1, day, hour, minute));
  const formatted = new Intl.DateTimeFormat('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone: 'UTC',
  }).format(asUtc);
  return `${formatted} (${timeZoneId})`;
}

/** `yyyy-MM-dd` formatted for display, with no time-of-day component — closures, leave spans,
 *  effective dates. Never zone-converted (a `DateOnly` on the wire has no time to convert). */
export function formatDateOnly(dateIso: string): string {
  const [year, month, day] = parseIntParts(dateIso, '-', 3) as [number, number, number];
  if ([year, month, day].some((n) => !Number.isFinite(n))) return dateIso;
  const asUtc = new Date(Date.UTC(year, month - 1, day));
  return new Intl.DateTimeFormat('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(asUtc);
}

/** Today's date as `yyyy-MM-dd` in the given IANA zone — used to bound "must be in the future in
 *  branch time" inputs (closures, roster pattern effective dates, leave, price/rate scheduling)
 *  without ever comparing raw browser-local `Date` objects across zones. */
export function todayInZone(timeZoneId: string): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: timeZoneId }).format(new Date()); // en-CA => yyyy-MM-dd
}

/** Now, truncated to the minute, as a local `yyyy-MM-ddTHH:mm` value suitable for a
 *  `datetime-local` input's `min` — used by `ScheduleChangeDialog` to disable past effective
 *  dates/times (ADR-FE-019, "never offer an edit the server refuses"). */
export function nowAsDateTimeLocal(): string {
  const now = new Date();
  now.setSeconds(0, 0);
  const offsetMs = now.getTimezoneOffset() * 60_000;
  return new Date(now.getTime() - offsetMs).toISOString().slice(0, 16);
}
