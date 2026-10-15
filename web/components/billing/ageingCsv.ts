import type { AgeingRow } from '@/lib/api/billing-staff';

/**
 * `components/billing/ageingCsv.ts` — HO-BIL-002 §11.5, U-F2. The ageing export built **from the same
 * rows as text**: every bucket amount is the server's string, copied unchanged — no calculation, no
 * parsing. A text cell starting with `= + - @` is prefixed with `'` (spreadsheet formula injection),
 * as the server's own CSV does; amounts are plain strings and are never prefixed.
 */
export const AGEING_CSV_HEADER = [
  'customerId',
  'customerName',
  'currency',
  'current',
  'days1To30',
  'days31To60',
  'days61To90',
  'over90',
  'total',
  'oldestDueOn',
] as const;

function textCell(value: string): string {
  const guarded = /^[=+\-@\t\r]/.test(value) ? `'${value}` : value;
  return /[",\n\r]/.test(guarded) ? `"${guarded.replace(/"/g, '""')}"` : guarded;
}

export function buildAgeingCsv(rows: AgeingRow[]): string {
  const lines = [AGEING_CSV_HEADER.join(',')];
  for (const row of rows) {
    lines.push(
      [
        textCell(row.customerId),
        textCell(row.customerName),
        textCell(row.total.currencyCode),
        row.current.amount,
        row.days1To30.amount,
        row.days31To60.amount,
        row.days61To90.amount,
        row.over90.amount,
        row.total.amount,
        textCell(row.oldestDueOn),
      ].join(','),
    );
  }
  return `${lines.join('\r\n')}\r\n`;
}
