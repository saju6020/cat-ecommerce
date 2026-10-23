'use client';

import * as React from 'react';
import Link from 'next/link';
import { ChevronDown, ChevronUp, ChevronsUpDown } from 'lucide-react';
import { EmptyState, type EmptyStateProps } from '@/components/common/EmptyState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

/**
 * `components/billing/ResponsiveTable.tsx` — HO-BIL-002 §7 UI-1/UI-4/UI-6. One list pattern for every
 * Billing list: a real `<table>` with a caption and header cells from 768 px (money columns
 * right-aligned with tabular numerals), **stacked cards** below it — only one of the two is ever in
 * the DOM, so there is no duplicate text and no horizontal scroll at 360 px. Sorting is on the
 * **text** columns of the rows already loaded (the Billing endpoints take no sort parameter); money
 * columns are never sortable (the browser compares no amounts, ADR-FE-080). Paging is server paging
 * through `pagination`.
 */
export interface Column<TRow> {
  id: string;
  header: string;
  cell: (row: TRow) => React.ReactNode;
  /** Right-aligned money or number column (tabular numerals). */
  numeric?: boolean;
  /** Present → the column header becomes a sort button over the loaded rows (text/date strings). */
  sortValue?: (row: TRow) => string;
  /** The card heading on a phone (first column by default); it is the row link when `rowHref` is set. */
  primary?: boolean;
  /** Keep the cell on one line (dates, ages, short codes). The primary and numeric columns always are,
   *  so an identifier like ORD-2026-000003 never breaks; the table scrolls sideways instead (2026-10-16). */
  nowrap?: boolean;
}

export interface Pager {
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (pageNumber: number) => void;
}

function useIsDesktop(minWidth: number): boolean {
  const [desktop, setDesktop] = React.useState(true);
  React.useEffect(() => {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return;
    const media = window.matchMedia(`(min-width: ${minWidth}px)`);
    const update = () => setDesktop(media.matches);
    update();
    media.addEventListener('change', update);
    return () => media.removeEventListener('change', update);
  }, [minWidth]);
  return desktop;
}

export function ResponsiveTable<TRow>({
  caption,
  columns,
  rows,
  getRowId,
  rowHref,
  isLoading,
  isError,
  onRetry,
  emptyState,
  pager,
  footer,
  minDesktopWidth = 768,
  dense = false,
}: {
  caption: string;
  columns: Array<Column<TRow>>;
  rows: TRow[] | undefined;
  getRowId: (row: TRow) => string;
  rowHref?: (row: TRow) => string;
  isLoading?: boolean;
  isError?: boolean;
  onRetry?: () => void;
  emptyState: Omit<EmptyStateProps, 'className'>;
  pager?: Pager;
  footer?: React.ReactNode;
  /** The viewport width from which the real table is used (cards below it). */
  minDesktopWidth?: number;
  /** Tighter padding and type for a very wide table (the invoice lines). */
  dense?: boolean;
}) {
  const desktop = useIsDesktop(minDesktopWidth);
  const [sort, setSort] = React.useState<{ id: string; direction: 'asc' | 'desc' } | null>(null);

  const sorted = React.useMemo(() => {
    if (!rows || !sort) return rows;
    const column = columns.find((c) => c.id === sort.id);
    if (!column?.sortValue) return rows;
    const pick = column.sortValue;
    const copy = [...rows].sort((a, b) => pick(a).localeCompare(pick(b)));
    return sort.direction === 'asc' ? copy : copy.reverse();
  }, [rows, sort, columns]);

  if (isLoading) return <LoadingState variant="table" rows={5} label={`Loading ${caption}…`} />;
  if (isError && !rows) return <ErrorState onRetry={onRetry} />;
  if (!sorted || sorted.length === 0) return <EmptyState {...emptyState} />;

  const primary = columns.find((c) => c.primary) ?? columns[0]!;
  const pageCount = pager ? Math.max(1, Math.ceil(pager.totalCount / pager.pageSize)) : 1;

  function toggleSort(id: string) {
    setSort((current) =>
      current?.id === id
        ? { id, direction: current.direction === 'asc' ? 'desc' : 'asc' }
        : { id, direction: 'asc' },
    );
  }

  return (
    <div className="flex min-w-0 flex-col gap-3">
      {desktop ? (
        <div
          role="region"
          aria-label={`${caption} (scrolls horizontally)`}
          tabIndex={0}
          className="relative min-w-0 overflow-x-auto rounded-[var(--radius-md)] border border-border"
        >
          <table
            className={cn('w-full border-collapse bg-surface', dense ? 'text-[13px]' : 'text-sm')}
          >
            <caption className="sr-only">{caption}</caption>
            <thead>
              <tr className="border-b border-border bg-surface-sunken text-left">
                {columns.map((column) => (
                  <th
                    key={column.id}
                    scope="col"
                    aria-sort={
                      sort?.id === column.id
                        ? sort.direction === 'asc'
                          ? 'ascending'
                          : 'descending'
                        : undefined
                    }
                    className={cn(
                      dense ? 'px-2 py-2' : 'px-3 py-2',
                      'text-xs font-semibold uppercase tracking-wide text-ink-muted',
                      column.numeric && 'text-right',
                    )}
                  >
                    {column.sortValue ? (
                      <button
                        type="button"
                        onClick={() => toggleSort(column.id)}
                        className="inline-flex items-center gap-1 font-semibold uppercase tracking-wide text-inherit hover:text-ink"
                        aria-label={`Sort by ${column.header}`}
                      >
                        {column.header}
                        {sort?.id === column.id ? (
                          sort.direction === 'asc' ? (
                            <ChevronUp size={12} aria-hidden="true" />
                          ) : (
                            <ChevronDown size={12} aria-hidden="true" />
                          )
                        ) : (
                          <ChevronsUpDown size={12} aria-hidden="true" className="opacity-50" />
                        )}
                      </button>
                    ) : (
                      column.header
                    )}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {sorted.map((row) => {
                const href = rowHref?.(row);
                return (
                  <tr key={getRowId(row)} className="border-b border-border last:border-0">
                    {columns.map((column) => (
                      <td
                        key={column.id}
                        className={cn(
                          dense ? 'px-2 py-2' : 'px-3 py-2',
                          'align-top text-ink',
                          column.numeric && 'text-right tabular-nums',
                          (column.nowrap || column.numeric || column.id === primary.id) &&
                            'whitespace-nowrap',
                        )}
                      >
                        {href && column.id === primary.id ? (
                          <Link href={href} className="font-semibold text-ink hover:underline">
                            {column.cell(row)}
                          </Link>
                        ) : (
                          column.cell(row)
                        )}
                      </td>
                    ))}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      ) : (
        <ul aria-label={caption} className="flex flex-col gap-3">
          {sorted.map((row) => {
            const href = rowHref?.(row);
            return (
              <li
                key={getRowId(row)}
                className="min-w-0 rounded-[var(--radius-md)] border border-border bg-surface p-3"
              >
                <div className="text-sm font-semibold text-ink [overflow-wrap:anywhere]">
                  {href ? (
                    <Link href={href} className="text-ink hover:underline">
                      {primary.cell(row)}
                    </Link>
                  ) : (
                    primary.cell(row)
                  )}
                </div>
                <dl className="mt-2 grid grid-cols-[minmax(0,8rem)_minmax(0,1fr)] gap-x-3 gap-y-1 text-sm">
                  {columns
                    .filter((column) => column.id !== primary.id)
                    .map((column) => (
                      <React.Fragment key={column.id}>
                        <dt className="text-ink-muted">{column.header}</dt>
                        <dd className="min-w-0 text-ink [overflow-wrap:anywhere]">
                          {column.cell(row)}
                        </dd>
                      </React.Fragment>
                    ))}
                </dl>
              </li>
            );
          })}
        </ul>
      )}
      {footer}
      {pager && pageCount > 1 && (
        <nav
          aria-label="Pages"
          className="flex flex-wrap items-center justify-between gap-2 text-sm text-ink-muted"
        >
          <span>{`Page ${pager.pageNumber} of ${pageCount} · ${pager.totalCount} total`}</span>
          <div className="flex gap-2">
            <Button
              variant="secondary"
              size="sm"
              disabled={pager.pageNumber <= 1}
              onClick={() => pager.onPageChange(pager.pageNumber - 1)}
            >
              Previous
            </Button>
            <Button
              variant="secondary"
              size="sm"
              disabled={pager.pageNumber >= pageCount}
              onClick={() => pager.onPageChange(pager.pageNumber + 1)}
            >
              Next
            </Button>
          </div>
        </nav>
      )}
    </div>
  );
}
