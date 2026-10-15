'use client';

import { ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react';
import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableFooter,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import type { ReportColumn, ReportRow } from '@/lib/api/reports';
import { cn } from '@/lib/utils';
import { CellValue, columnHeading, isNumericColumn } from './CellValue';

const ROW_HEADER =
  'sticky left-0 z-[1] bg-surface text-[13.5px] font-normal normal-case tracking-normal text-ink';

/** `sort` is a sortable column key with an optional `-` prefix (HO-RPT-001 §12.4). */
export function nextSort(current: string, key: string): string {
  if (current === key) return `-${key}`;
  if (current === `-${key}`) return '';
  return key;
}

function ariaSort(sort: string, key: string): 'ascending' | 'descending' | 'none' {
  if (sort === key) return 'ascending';
  if (sort === `-${key}`) return 'descending';
  return 'none';
}

/**
 * `components/reports/ReportTable.tsx` — ⭐ HO-RPT-002 §11.3, ADR-FE-129/133, UI-6. A real `<table>`:
 * a `<caption>` (title, period, branch, zone), `<th scope="col">` per column with the unit in the
 * label, sortable headers as `<button>`s carrying `aria-sort` (a non-sortable header has no button),
 * the first column as a sticky row header, numbers right-aligned in tabular figures, and a
 * `<tfoot>` "Total" row when the server sent `totals`. Every cell goes through `CellValue` ->
 * `formatCell`; nothing here adds, rounds or recomputes (ADR-FE-131).
 */
export function ReportTable({
  caption,
  columns,
  rows,
  totals,
  timeZoneId,
  sort,
  onSort,
}: {
  caption: string;
  columns: ReportColumn[];
  rows: ReportRow[];
  totals: ReportRow | null;
  timeZoneId: string;
  sort: string;
  onSort: (key: string) => void;
}) {
  const [first, ...rest] = columns;
  return (
    <Table>
      <TableCaption>{caption}</TableCaption>
      <TableHeader>
        <TableRow>
          {columns.map((column, index) => {
            const numeric = isNumericColumn(column);
            const state = ariaSort(sort, column.key);
            return (
              <TableHead
                key={column.key}
                aria-sort={column.sortable ? state : undefined}
                className={cn(
                  numeric && 'text-right',
                  index === 0 && 'sticky left-0 z-10 bg-surface-sunken',
                )}
              >
                {column.sortable ? (
                  <button
                    type="button"
                    onClick={() => onSort(column.key)}
                    className={cn(
                      'inline-flex items-center gap-1 rounded-[var(--radius-sm)] font-bold uppercase tracking-wide hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
                      numeric && 'flex-row-reverse',
                    )}
                  >
                    {columnHeading(column)}
                    {state === 'ascending' ? (
                      <ArrowUp size={12} aria-hidden="true" />
                    ) : state === 'descending' ? (
                      <ArrowDown size={12} aria-hidden="true" />
                    ) : (
                      <ArrowUpDown size={12} aria-hidden="true" />
                    )}
                  </button>
                ) : (
                  columnHeading(column)
                )}
              </TableHead>
            );
          })}
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((row, rowIndex) => (
          <TableRow key={rowIndex}>
            {first && (
              <TableHead scope="row" className={ROW_HEADER}>
                <CellValue column={first} value={row[first.key]} timeZoneId={timeZoneId} />
              </TableHead>
            )}
            {rest.map((column) => (
              <TableCell
                key={column.key}
                className={cn(isNumericColumn(column) && 'text-right tabular-nums')}
              >
                <CellValue column={column} value={row[column.key]} timeZoneId={timeZoneId} />
              </TableCell>
            ))}
          </TableRow>
        ))}
      </TableBody>
      {totals && (
        <TableFooter>
          <TableRow>
            {columns.map((column, index) =>
              index === 0 ? (
                <TableHead
                  key={column.key}
                  scope="row"
                  className={cn(ROW_HEADER, 'bg-surface-sunken font-bold')}
                >
                  Total
                  {totals[column.key] != null && (
                    <>
                      {' '}
                      <CellValue
                        column={column}
                        value={totals[column.key]}
                        timeZoneId={timeZoneId}
                      />
                    </>
                  )}
                </TableHead>
              ) : (
                <TableCell
                  key={column.key}
                  className={cn('font-bold', isNumericColumn(column) && 'text-right tabular-nums')}
                >
                  {totals[column.key] === undefined ? null : (
                    <CellValue column={column} value={totals[column.key]} timeZoneId={timeZoneId} />
                  )}
                </TableCell>
              ),
            )}
          </TableRow>
        </TableFooter>
      )}
    </Table>
  );
}
