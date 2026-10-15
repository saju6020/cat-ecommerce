import { formatMoney } from './money';
import type { ReportCell, ReportColumn, ReportMoneyCell } from '@/lib/api/reports';

/**
 * `lib/format/report.ts` — HO-RPT-002 §8.4, ADR-FE-131. `formatCell` is the SINGLE rendering path
 * for a report or dashboard value (the report table, the card list, the totals row, the chart's
 * hidden table and every dashboard tile all call it). It only ever formats what the server sent:
 *
 *  - money is rendered through `formatMoney` from the `{ amount, currencyCode }` string pair;
 *  - percent, decimal and integer text is grouped and suffixed as TEXT — nothing is rounded,
 *    summed or converted;
 *  - a date is formatted from its `yyyy-MM-dd` text with no `Date` object at all, so no zone can
 *    shift it by a day (HO-CAT-002 §9);
 *  - an instant is rendered in the zone the payload names;
 *  - `null` is "not recorded" and is never a zero (HO-ACC-002 ADR-FE-012).
 */
const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** The text for a missing value. A component adds the visually hidden "Not recorded". */
export const NOT_RECORDED_TEXT = '—';

export function isMoneyCell(value: unknown): value is ReportMoneyCell {
  return (
    typeof value === 'object' &&
    value !== null &&
    typeof (value as ReportMoneyCell).amount === 'string' &&
    typeof (value as ReportMoneyCell).currencyCode === 'string'
  );
}

/** `yyyy-MM-dd` -> `dd MMM yyyy` by string slicing only. Anything else is returned untouched. */
export function formatReportDate(value: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  if (!match) return value;
  const month = MONTHS[Number(match[2]) - 1];
  return month ? `${match[3]} ${month} ${match[1]}` : value;
}

/** Groups the integer digits of a numeric TEXT ("1234567.50" -> "1,234,567.50") without rounding. */
export function groupDigits(text: string): string {
  const match = /^(-?)(\d+)(\.\d+)?$/.exec(text.trim());
  if (!match) return text;
  const whole = (match[2] ?? '').replace(/\B(?=(\d{3})+(?!\d))/g, ',');
  return `${match[1]}${whole}${match[3] ?? ''}`;
}

/** `dd MMM yyyy HH:mm` of an instant in the given IANA zone (the zone is named by the caller). */
export function formatInstantInZone(value: string, timeZoneId: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  const parts = new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone: timeZoneId,
  }).formatToParts(date);
  const pick = (type: string) => parts.find((part) => part.type === type)?.value ?? '';
  const hour = pick('hour') === '24' ? '00' : pick('hour');
  const month = MONTHS[Number(pick('month')) - 1] ?? pick('month');
  return `${pick('day')} ${month} ${pick('year')} ${hour}:${pick('minute')}`;
}

/** A schedule instant with a weekday: "Mon 5 Oct 07:00" (the caller names the zone once). */
export function formatRunInstant(value: string, timeZoneId: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  const parts = new Intl.DateTimeFormat('en-GB', {
    weekday: 'short',
    day: 'numeric',
    month: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone: timeZoneId,
  }).formatToParts(date);
  const pick = (type: string) => parts.find((part) => part.type === type)?.value ?? '';
  const hour = pick('hour') === '24' ? '00' : pick('hour');
  const month = MONTHS[Number(pick('month')) - 1] ?? pick('month');
  return `${pick('weekday')} ${pick('day')} ${month} ${hour}:${pick('minute')}`;
}

/**
 * Renders one cell. `column.type` picks the rule; an unknown type falls back to plain text so a
 * column the server adds next year never crashes the page.
 */
export function formatCell(
  value: ReportCell | undefined,
  column: Pick<ReportColumn, 'type'>,
  timeZoneId: string,
): string {
  if (value === null || value === undefined) return NOT_RECORDED_TEXT;
  if (isMoneyCell(value)) return formatMoney(value);

  switch (column.type) {
    case 'Percent':
      return `${groupDigits(String(value))}%`;
    case 'Decimal':
    case 'Integer':
      return groupDigits(String(value));
    case 'Date':
      return formatReportDate(String(value));
    case 'Instant':
      return formatInstantInZone(String(value), timeZoneId);
    case 'Boolean':
      return value === true || value === 'true' ? 'Yes' : 'No';
    default:
      return String(value);
  }
}

/** A dashboard figure has a `type` and an optional `unit` ("h", "min"); same rules, plus the unit. */
export function formatFigure(
  value: ReportCell | undefined,
  type: string,
  unit: string | null | undefined,
  timeZoneId: string,
): string {
  const text = formatCell(value, { type }, timeZoneId);
  if (text === NOT_RECORDED_TEXT || !unit) return text;
  return `${text} ${unit}`;
}

/**
 * The Text column the server sends right after `variance` in R13 and R14 (HO-RPT-001-FU1): "Over",
 * "Short", "Matched", or `null` when there is nothing to compare (never "Matched"). The page shows
 * this word and NEVER reads the sign of `variance`: the sign convention differs even within R13.
 */
export const VARIANCE_DIRECTION_KEY = 'varianceDirection';

export const VARIANCE_DIRECTIONS = ['Over', 'Short', 'Matched'] as const;
export type VarianceDirection = (typeof VARIANCE_DIRECTIONS)[number];

export function asVarianceDirection(value: unknown): VarianceDirection | null {
  return VARIANCE_DIRECTIONS.find((direction) => direction === value) ?? null;
}

/**
 * A number for the CHART ONLY (a plotted point needs a number). The table and the chart's hidden
 * data table both render the server's text through `formatCell`; this value is never shown, summed
 * or sent anywhere (ADR-FE-134).
 */
export function toChartNumber(cell: ReportCell | undefined): number | null {
  if (cell === null || cell === undefined || typeof cell === 'boolean') return null;
  const text = isMoneyCell(cell) ? cell.amount : cell;
  const parsed = typeof text === 'number' ? text : Number.parseFloat(text);
  return Number.isFinite(parsed) ? parsed : null;
}

/**
 * A chart Y-axis tick as a COMPACT label with the currency sign ("৳12.5K"). Display only: the tick
 * value is an axis position chosen by the chart, not a report figure, and the table and the hidden
 * data table carry the server's exact text (ADR-FE-134).
 */
export function formatAxisTick(value: number, currencyCode?: string): string {
  const base = { notation: 'compact', maximumFractionDigits: 1 } as const;
  try {
    return new Intl.NumberFormat(
      'en-US',
      currencyCode
        ? { ...base, style: 'currency', currency: currencyCode, currencyDisplay: 'narrowSymbol' }
        : base,
    ).format(value);
  } catch {
    return String(value);
  }
}

// ---------------------------------------------------------------------------------------------
// Plain-language labels for the server's enumerations.
// ---------------------------------------------------------------------------------------------

export const CATEGORY_ORDER = [
  'ClinicOperations',
  'Clinical',
  'Boarding',
  'Stock',
  'Sales',
  'Financial',
  'Growth',
] as const;

export const CATEGORY_LABELS: Readonly<Record<string, string>> = {
  ClinicOperations: 'Clinic operations',
  Clinical: 'Clinical',
  Boarding: 'Boarding',
  Stock: 'Stock',
  Sales: 'Sales',
  Financial: 'Financial',
  Growth: 'Growth',
};

export function categoryLabel(category: string): string {
  return CATEGORY_LABELS[category] ?? category;
}

export const PERIOD_LABELS: Readonly<Record<string, { label: string; hint: string }>> = {
  Today: { label: 'Today', hint: 'Covers the day the report is sent.' },
  PreviousDay: { label: 'Previous day', hint: 'Covers the day before the report is sent.' },
  PreviousWeek: { label: 'Previous week', hint: 'Covers the previous calendar week.' },
  PreviousMonth: { label: 'Previous month', hint: 'Covers the previous calendar month.' },
  MonthToDate: { label: 'Month to date', hint: 'Covers the month so far.' },
  Last7Days: { label: 'Last 7 days', hint: 'Covers the seven days up to the send time.' },
  Last30Days: { label: 'Last 30 days', hint: 'Covers the thirty days up to the send time.' },
  Next30Days: { label: 'Next 30 days', hint: 'Looks ahead thirty days from the send time.' },
  Next90Days: { label: 'Next 90 days', hint: 'Looks ahead ninety days from the send time.' },
};

export function periodLabel(period: string): string {
  return PERIOD_LABELS[period]?.label ?? period;
}

export function periodHint(period: string): string {
  return PERIOD_LABELS[period]?.hint ?? '';
}

/** All periods a schedule can use, in the order the editor lists them. */
export const SCHEDULE_PERIODS = Object.keys(PERIOD_LABELS);

const FORWARD_PERIODS = ['Next30Days', 'Next90Days'];

/**
 * Which relative periods the editor OFFERS for a report. This mirrors `ReportSchedulePeriods.
 * IsCompatible` (HO-RPT-001 backlog 20) as a convenience so a choice the server will refuse is not
 * offered (U-E1); the server still decides, and `InvalidReportSchedulePeriod` is mapped if the two
 * ever disagree. It reads only the catalogue's `kind` and `defaultPeriod` — it computes no date.
 */
export function compatiblePeriods(report: { kind: string; defaultPeriod: string }): string[] {
  const definitionForward = FORWARD_PERIODS.includes(report.defaultPeriod);
  return SCHEDULE_PERIODS.filter((period) => {
    const forward = FORWARD_PERIODS.includes(period);
    if (forward || definitionForward) return forward && definitionForward;
    if (report.kind === 'AsAt') return period === 'Today' || period === report.defaultPeriod;
    if (period === 'Today') return report.defaultPeriod === 'Today';
    return true;
  });
}

/** "412 KB" style size for a run history row. */
export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/** A failed scheduled run's `failureCode`, as a sentence that says what to change (U-E7). The code is never shown. */
export function describeRunFailure(code: string | null | undefined): string {
  switch (code) {
    case 'ReportTimedOut':
      return 'The report took too long to run. Choose a shorter period or a single branch.';
    case 'ReportTooLargeForPdf':
      return 'Too many rows for a PDF. Send CSV instead, or choose a shorter period.';
    case 'ReportTooLargeForCsv':
      return 'Too many rows for a CSV. Choose a shorter period or a single branch.';
    case 'ReportFileNotStored':
      return 'The file could not be stored, so nothing was sent. Run it again.';
    case 'ClinicalAccessLogFailed':
      return 'The access record for clinical data could not be written, so nothing was sent. Run it again.';
    case 'UnknownReport':
      return 'The report no longer exists. Cancel this schedule and create a new one.';
    default:
      return 'The report could not be generated. Try again, or choose a shorter period.';
  }
}

/** "Daily at 07:00 (Asia/Dhaka)" from the list's `frequencyLabel`, `sendAtLocal` and zone. */
export function scheduleTimingLabel(
  frequencyLabel: string,
  sendAtLocal: string,
  timeZoneId: string,
): string {
  return `${frequencyLabel} at ${sendAtLocal} (${timeZoneId})`;
}
