import type { ReportColumn } from '@/lib/api/reports';
import { columnHeading } from './CellValue';

/**
 * `components/reports/ReportSortSelect.tsx` — HO-RPT-002 §11.3, ADR-FE-133. The card list has no
 * column headers to click, so on a narrow screen sorting is a `<select>` of the sortable columns
 * (ascending and descending) writing the same `sort` URL param the table headers write.
 */
export function ReportSortSelect({
  columns,
  sort,
  onSort,
}: {
  columns: ReportColumn[];
  sort: string;
  onSort: (sort: string) => void;
}) {
  const sortable = columns.filter((column) => column.sortable);
  if (sortable.length === 0) return null;
  return (
    <div className="flex min-w-0 max-w-full flex-col gap-1">
      <label htmlFor="report-sort" className="text-sm font-semibold text-ink">
        Sort by
      </label>
      <select
        id="report-sort"
        value={sort}
        onChange={(event) => onSort(event.target.value)}
        className="w-full max-w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
      >
        <option value="">Default order</option>
        {sortable.flatMap((column) => [
          <option key={column.key} value={column.key}>
            {columnHeading(column)} — ascending
          </option>,
          <option key={`-${column.key}`} value={`-${column.key}`}>
            {columnHeading(column)} — descending
          </option>,
        ])}
      </select>
    </div>
  );
}
