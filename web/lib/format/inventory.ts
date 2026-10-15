import { formatDateOnly } from './time';

/**
 * `lib/format/inventory.ts` — HO-IMP-002 §8.4, ADR-FE-041/047. Stock display helpers.
 *
 * Quantities arrive as strings (`"12"`, `"0.5"`) with the stock unit beside them. Nothing here does
 * arithmetic on them: trimming is string surgery, and every quantity is returned WITH its unit
 * (UI-8) — there is deliberately no helper that formats a bare number.
 */

/** `"12.000"` -> `"12"`, `"0.500"` -> `"0.5"`, `"3"` -> `"3"`. String surgery only. */
export function trimDecimalString(value: string): string {
  const trimmed = value.trim();
  if (!trimmed.includes('.')) return trimmed;
  return trimmed.replace(/0+$/, '').replace(/\.$/, '');
}

/** `formatQuantity("12.000", "bags")` -> `"12 bags"`. Always appends the unit (UI-8). */
export function formatQuantity(value: string, stockUnit: string): string {
  return `${trimDecimalString(value)} ${stockUnit}`.trim();
}

/** A ledger delta: `"+12 bags"` / `"-3 bags"`. The sign comes from the server's value. */
export function formatSignedQuantity(value: string, stockUnit: string): string {
  const trimmed = trimDecimalString(value);
  const signed = trimmed.startsWith('-') || trimmed.startsWith('+') ? trimmed : `+${trimmed}`;
  return `${signed} ${stockUnit}`;
}

/**
 * A unit cost is a rate, shown to four decimal places (ADR-106) — `Intl` is used for DISPLAY only
 * (the same allowance `formatMoney` documents); the wire value stays a string.
 */
export function formatUnitCost(cost: { amount: string; currencyCode: string }): string {
  const numeric = Number(cost.amount);
  if (!Number.isFinite(numeric)) return `${cost.amount} ${cost.currencyCode}`;
  try {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: cost.currencyCode,
      currencyDisplay: 'narrowSymbol',
      minimumFractionDigits: 4,
      maximumFractionDigits: 4,
    }).format(numeric);
  } catch {
    return `${cost.amount} ${cost.currencyCode}`;
  }
}

export type ExpirySeverity = 'none' | 'ok' | 'today' | 'expired';

export interface ExpiryDescription {
  /** The text-first statement — `"12 Oct 2026 · 47 days"`. */
  text: string;
  /** Only chooses an icon (which carries its own label); the words above carry the meaning. */
  severity: ExpirySeverity;
}

function plural(count: number, noun: string): string {
  return `${count} ${noun}${count === 1 ? '' : 's'}`;
}

/**
 * ADR-FE-047. `isExpired` / `daysToExpiry` are the SERVER's (derived in the branch's local date,
 * ADR-101) — this only words them. A batch with no expiry date says so.
 */
export function formatExpiry(
  expiresOn: string | null | undefined,
  isExpired: boolean,
  daysToExpiry: number | null | undefined,
): ExpiryDescription {
  if (!expiresOn) return { text: 'No expiry date', severity: 'none' };
  const date = formatDateOnly(expiresOn);
  if (isExpired) {
    const ago = daysToExpiry === null || daysToExpiry === undefined ? null : Math.abs(daysToExpiry);
    return {
      text: ago === null ? `${date} · expired` : `${date} · expired ${plural(ago, 'day')} ago`,
      severity: 'expired',
    };
  }
  if (daysToExpiry === 0) return { text: `${date} · expires today`, severity: 'today' };
  if (daysToExpiry === null || daysToExpiry === undefined) return { text: date, severity: 'ok' };
  return { text: `${date} · ${plural(daysToExpiry, 'day')}`, severity: 'ok' };
}

/** `"12 Oct 2026"` for a nullable `yyyy-MM-dd`, or the supplied fallback. */
export function formatDateOrDash(dateOnly: string | null | undefined, fallback = '—'): string {
  return dateOnly ? formatDateOnly(dateOnly) : fallback;
}

/**
 * A `datetime-local` value (`2026-10-07T09:30`) typed as the BRANCH's wall-clock time, as an ISO-8601
 * instant (`…Z`) — what the API takes (it normalises to UTC). The offset is found by formatting a
 * candidate instant in the branch's zone and correcting for the drift (no time-zone library).
 */
export function branchLocalToInstant(dateTimeLocal: string, timeZoneId: string): string {
  const guess = new Date(`${dateTimeLocal}:00Z`);
  if (Number.isNaN(guess.getTime())) return '';
  const parts = Object.fromEntries(
    new Intl.DateTimeFormat('en-US', {
      timeZone: timeZoneId,
      hourCycle: 'h23',
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    })
      .formatToParts(guess)
      .map((p) => [p.type, p.value]),
  );
  const asIfZoned = Date.UTC(
    Number(parts.year),
    Number(parts.month) - 1,
    Number(parts.day),
    Number(parts.hour),
    Number(parts.minute),
    Number(parts.second),
  );
  return new Date(guess.getTime() + (guess.getTime() - asIfZoned)).toISOString();
}

/** Now, to the minute, as a `datetime-local` value in the branch's zone (the latest time a breach
 *  may be observed at). */
export function nowAsBranchDateTimeLocal(timeZoneId: string): string {
  const parts = Object.fromEntries(
    new Intl.DateTimeFormat('en-GB', {
      timeZone: timeZoneId,
      hourCycle: 'h23',
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    })
      .formatToParts(new Date())
      .map((p) => [p.type, p.value]),
  );
  return `${parts.year}-${parts.month}-${parts.day}T${parts.hour}:${parts.minute}`;
}

/** A file size for display (documents are metadata only — this is not a stock quantity). */
export function formatBytes(bytes: number): string {
  if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  if (bytes >= 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${bytes} bytes`;
}
