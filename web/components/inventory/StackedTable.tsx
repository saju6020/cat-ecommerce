'use client';

import * as React from 'react';
import { useNarrowContainer } from '@/lib/hooks/useIsNarrow';
import { cn } from '@/lib/utils';

export interface StackedColumn<T> {
  id: string;
  header: string;
  cell: (row: T, index: number) => React.ReactNode;
  /** Right-aligned, tabular figures (quantities and money). */
  numeric?: boolean;
  /** The row's name: a `<th scope="row">` in the table, the card's title on a phone. */
  rowHeader?: boolean;
}

/**
 * `components/inventory/StackedTable.tsx` — HO-IMP-002 UI-1. A real `<table>` (caption, column and
 * row headers) while its content area is at least 640 px wide; below that each row becomes a card of label/value pairs so nothing
 * scrolls sideways. `DataTable` remains the component for URL-driven lists; this is for the
 * read-only detail tables inside a page (order lines, receipt lines, costs, documents).
 */
export function StackedTable<T>({
  caption,
  columns,
  rows,
  getKey,
  rowClassName,
  footer,
}: {
  caption: string;
  columns: Array<StackedColumn<T>>;
  rows: T[];
  getKey: (row: T, index: number) => string;
  rowClassName?: (row: T) => string | undefined;
  /** Totals the SERVER computed, shown under the rows as label/value pairs. */
  footer?: Array<{ label: string; value: React.ReactNode }>;
}) {
  const [ref, narrow] = useNarrowContainer<HTMLDivElement>();

  if (narrow) {
    const title = columns.find((c) => c.rowHeader) ?? columns[0]!;
    return (
      <div ref={ref}>
        <p className="sr-only">{caption}</p>
        <ul className="flex flex-col gap-2">
          {rows.map((row, index) => (
            <li
              key={getKey(row, index)}
              className={cn('rounded-[var(--radius-md)] border border-border bg-surface p-3', rowClassName?.(row))}
            >
              <p className="mb-1 break-words text-sm font-semibold text-ink">{title.cell(row, index)}</p>
              <dl className="grid grid-cols-[minmax(5rem,7rem)_minmax(0,1fr)] gap-x-3 gap-y-1 text-sm">
                {columns
                  .filter((c) => c !== title)
                  .map((column) => (
                    <React.Fragment key={column.id}>
                      <dt className="text-ink-muted">{column.header}</dt>
                      <dd className="min-w-0 break-words text-ink">{column.cell(row, index)}</dd>
                    </React.Fragment>
                  ))}
              </dl>
            </li>
          ))}
        </ul>
        {footer && <Footer items={footer} />}
      </div>
    );
  }

  return (
    <div ref={ref}>
      <div
        role="region"
        aria-label={`${caption} (scrolls horizontally)`}
        tabIndex={0}
        className="overflow-x-auto rounded-[var(--radius-md)] border border-border"
      >
        <table className="w-full border-collapse bg-surface text-sm">
          <caption className="sr-only">{caption}</caption>
          <thead>
            <tr className="bg-surface-sunken">
              {columns.map((column) => (
                <th
                  key={column.id}
                  scope="col"
                  className={cn(
                    'px-3 py-2 text-left text-xs font-bold uppercase tracking-wide text-ink-muted',
                    column.numeric && 'text-right',
                  )}
                >
                  {column.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row, index) => (
              <tr key={getKey(row, index)} className={cn('border-t border-border align-top', rowClassName?.(row))}>
                {columns.map((column) =>
                  column.rowHeader ? (
                    <th key={column.id} scope="row" className="px-3 py-2 text-left font-semibold text-ink">
                      {column.cell(row, index)}
                    </th>
                  ) : (
                    <td
                      key={column.id}
                      className={cn('px-3 py-2 text-ink', column.numeric && 'text-right tabular-nums')}
                    >
                      {column.cell(row, index)}
                    </td>
                  ),
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {footer && <Footer items={footer} />}
    </div>
  );
}

function Footer({ items }: { items: Array<{ label: string; value: React.ReactNode }> }) {
  return (
    <dl className="mt-2 flex flex-wrap justify-end gap-x-6 gap-y-1 text-sm">
      {items.map((item) => (
        <div key={item.label} className="flex gap-2">
          <dt className="text-ink-muted">{item.label}</dt>
          <dd className="font-semibold text-ink">{item.value}</dd>
        </div>
      ))}
    </dl>
  );
}
