import { Button } from '@/components/ui/button';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { EmptyState } from '@/components/common/EmptyState';
import { formatDateOnly } from '@/lib/format/time';

/**
 * `components/roster/RosterWeekView.tsx` — ⭐ HO-ADM-002 §11.4, ADR-FE-017 (renumbered — see that
 * ADR's note), UI-1/UI-6. A real `<table>` with row/column headers on desktop; below 768 px a list
 * grouped by day (never a horizontally scrolling grid — both are always mounted, CSS toggled the
 * same way `StaffShell`'s nav does, so neither depends on JS having run to pick the right one).
 * Leave, exceptions ("Extra"/"Cancelled") and "Outside opening hours" are shown as **text**, never
 * colour alone (U-D2).
 */
export interface RosterInterval {
  label: string; // "09:00–13:00 (Asia/Dhaka)"
  outsideOpeningHours: boolean;
}

export interface RosterDayCell {
  intervals: RosterInterval[];
  isOnLeave: boolean;
  exceptionLabels: string[]; // "Extra shift" / "Shift cancelled"
}

export interface RosterRow {
  staffAccountId: string;
  staffFullName: string;
}

export interface RosterWeekViewProps {
  weekDates: string[]; // 7 "yyyy-MM-dd", Monday first
  rows: RosterRow[];
  cellFor: (staffAccountId: string, date: string) => RosterDayCell;
  onPrevWeek: () => void;
  onNextWeek: () => void;
  onThisWeek: () => void;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}

function CellContents({ cell }: { cell: RosterDayCell }) {
  if (cell.isOnLeave) {
    return <p className="text-xs font-semibold text-ink">On leave</p>;
  }
  if (cell.intervals.length === 0 && cell.exceptionLabels.length === 0) {
    return <p className="text-xs text-ink-faint">—</p>;
  }
  return (
    <div className="flex flex-col gap-1">
      {cell.intervals.map((interval, index) => (
        <p key={index} className="text-xs text-ink">
          {interval.label}
          {interval.outsideOpeningHours && (
            <span className="block text-xs font-semibold text-warning-ink">
              Outside opening hours
            </span>
          )}
        </p>
      ))}
      {cell.exceptionLabels.map((label, index) => (
        <p key={index} className="text-xs font-semibold text-ink-muted">
          {label}
        </p>
      ))}
    </div>
  );
}

export function RosterWeekView({
  weekDates,
  rows,
  cellFor,
  onPrevWeek,
  onNextWeek,
  onThisWeek,
  isLoading,
  isError,
  onRetry,
}: RosterWeekViewProps) {
  const firstDate = weekDates[0];
  const lastDate = weekDates[6];

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm font-semibold text-ink">
          {firstDate && formatDateOnly(firstDate)} – {lastDate && formatDateOnly(lastDate)}
        </p>
        <div className="flex flex-wrap gap-2">
          <Button variant="secondary" size="sm" onClick={onPrevWeek}>
            Previous week
          </Button>
          <Button variant="secondary" size="sm" onClick={onThisWeek}>
            This week
          </Button>
          <Button variant="secondary" size="sm" onClick={onNextWeek}>
            Next week
          </Button>
        </div>
      </div>

      {isLoading ? (
        <LoadingState variant="table" rows={5} />
      ) : isError ? (
        <ErrorState onRetry={onRetry} />
      ) : rows.length === 0 ? (
        <EmptyState title="No staff rostered at this branch" />
      ) : (
        <>
          {/* Desktop / tablet: a real table, row headers (staff) and column headers (days). */}
          <div
            role="region"
            aria-label="Roster (scrolls horizontally)"
            tabIndex={0}
            className="hidden overflow-x-auto rounded-[var(--radius-md)] border border-border md:block"
          >
            <table className="w-full border-collapse bg-surface text-sm">
              <caption className="sr-only">Weekly roster</caption>
              <thead>
                <tr>
                  <th
                    scope="col"
                    className="border-b border-border p-2 text-left text-xs font-bold uppercase text-ink-muted"
                  >
                    Staff
                  </th>
                  {weekDates.map((date) => (
                    <th
                      key={date}
                      scope="col"
                      className="border-b border-border p-2 text-left text-xs font-bold uppercase text-ink-muted"
                    >
                      {formatDateOnly(date)}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.staffAccountId}>
                    <th
                      scope="row"
                      className="border-b border-border p-2 text-left text-sm font-semibold text-ink"
                    >
                      {row.staffFullName}
                    </th>
                    {weekDates.map((date) => (
                      <td key={date} className="border-b border-border p-2 align-top">
                        <CellContents cell={cellFor(row.staffAccountId, date)} />
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Below 768px: a list grouped by day — never a horizontally scrolling grid (UI-1). */}
          <div className="flex flex-col gap-5 md:hidden">
            {weekDates.map((date) => (
              <section key={date} aria-label={formatDateOnly(date)}>
                <h3 className="mb-2 text-sm font-semibold text-ink">{formatDateOnly(date)}</h3>
                <ul className="flex flex-col gap-2">
                  {rows.map((row) => (
                    <li
                      key={row.staffAccountId}
                      className="rounded-[var(--radius-md)] border border-border p-3"
                    >
                      <p className="text-sm font-semibold text-ink">{row.staffFullName}</p>
                      <CellContents cell={cellFor(row.staffAccountId, date)} />
                    </li>
                  ))}
                </ul>
              </section>
            ))}
          </div>
        </>
      )}
    </div>
  );
}
