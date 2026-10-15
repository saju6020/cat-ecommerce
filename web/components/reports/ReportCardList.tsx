import type { ReportColumn, ReportRow } from '@/lib/api/reports';
import { CellValue, columnHeading } from './CellValue';

/**
 * `components/reports/ReportCardList.tsx` — HO-RPT-002 §11.3, ADR-FE-133, UI-1. The narrow-screen
 * form of the report table: each row is a card holding a `<dl>` of column label -> value, so a
 * 12-column report never scrolls the page sideways at 360 px. Same cells, same `formatCell`, same
 * order as the table; the totals (when the server sent them) are a last card labelled "Total".
 */
export function ReportCardList({
  label,
  columns,
  rows,
  totals,
  timeZoneId,
}: {
  label: string;
  columns: ReportColumn[];
  rows: ReportRow[];
  totals: ReportRow | null;
  timeZoneId: string;
}) {
  const totalColumns = totals ? columns.filter((column) => totals[column.key] !== undefined) : [];
  return (
    <ul aria-label={label} className="m-0 flex list-none flex-col gap-3 p-0">
      {rows.map((row, rowIndex) => (
        <li
          key={rowIndex}
          className="rounded-[var(--radius-md)] border border-border bg-surface p-3"
        >
          <dl className="m-0 grid grid-cols-1 gap-2">
            {columns.map((column) => (
              <div key={column.key} className="flex min-w-0 justify-between gap-3 text-sm">
                <dt className="min-w-0 flex-none basis-2/5 text-ink-muted">
                  {columnHeading(column)}
                </dt>
                <dd className="m-0 min-w-0 flex-1 break-words text-right text-ink tabular-nums">
                  <CellValue column={column} value={row[column.key]} timeZoneId={timeZoneId} />
                </dd>
              </div>
            ))}
          </dl>
        </li>
      ))}
      {totals && totalColumns.length > 0 && (
        <li className="rounded-[var(--radius-md)] border border-border-strong bg-surface-sunken p-3">
          <p className="m-0 mb-2 text-sm font-bold text-ink">Total</p>
          <dl className="m-0 grid grid-cols-1 gap-2">
            {totalColumns.map((column) => (
              <div key={column.key} className="flex min-w-0 justify-between gap-3 text-sm">
                <dt className="min-w-0 flex-none basis-2/5 text-ink-muted">
                  {columnHeading(column)}
                </dt>
                <dd className="m-0 min-w-0 flex-1 break-words text-right font-semibold text-ink tabular-nums">
                  <CellValue column={column} value={totals[column.key]} timeZoneId={timeZoneId} />
                </dd>
              </div>
            ))}
          </dl>
        </li>
      )}
    </ul>
  );
}
