'use client';

import { lazy, Suspense, useId, useMemo, useState } from 'react';
import { BarChart3 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { ReportColumn, ReportRow } from '@/lib/api/reports';
import { formatAxisTick, formatCell, toChartNumber } from '@/lib/format/report';
import { columnHeading } from './CellValue';

const ReportChartCanvas = lazy(() => import('./ReportChartCanvas'));

/** The Y-axis title: the unit and the currency code ("BDT"), else the column label. */
export function chartAxisTitle(
  column: { unit?: string | null; label: string },
  currency: string | undefined,
): string {
  return [column.unit, currency].filter(Boolean).join(' ') || column.label;
}

const SERIES_TYPES = ['Money', 'Integer', 'Decimal', 'Percent'];

/**
 * `components/reports/ReportChart.tsx` — HO-RPT-002 §11.3, ADR-FE-134, UI-6. A companion to the
 * table of a `timeBucketed` report, closed by default so the page stays fast. It plots the SAME
 * rows the table shows — the first column labels the points and a chosen numeric column is the
 * series — and never aggregates or recomputes. A visually hidden `<table>` repeats every point with
 * the server's own formatted text, which is what makes the chart accessible. Money in a chart is
 * only ever plotted, the labels and tooltips are the server's strings.
 */
export function ReportChart({
  columns,
  rows,
  timeZoneId,
  title,
}: {
  columns: ReportColumn[];
  rows: ReportRow[];
  timeZoneId: string;
  title: string;
}) {
  const [open, setOpen] = useState(false);
  const series = useMemo(() => columns.filter((c) => SERIES_TYPES.includes(c.type)), [columns]);
  const [seriesKey, setSeriesKey] = useState('');
  const regionId = useId();

  const labelColumns = columns.filter((c) => !SERIES_TYPES.includes(c.type));
  const active = series.find((c) => c.key === seriesKey) ?? series[0];
  if (labelColumns.length === 0 || !active || rows.length === 0) return null;

  const points = rows.map((row) => ({
    parts: labelColumns.map((column) => formatCell(row[column.key], column, timeZoneId)),
    label: labelColumns
      .map((column) => formatCell(row[column.key], column, timeZoneId))
      .join(' · '),
    value: toChartNumber(row[active.key]),
    text: formatCell(row[active.key], active, timeZoneId),
  }));
  const currency = rows
    .map((row) => row[active.key])
    .find(
      (cell): cell is { amount: string; currencyCode: string } =>
        typeof cell === 'object' && cell !== null,
    )?.currencyCode;
  const axisLabel = chartAxisTitle(active, currency);

  return (
    <section aria-label="Chart" className="flex flex-col gap-3">
      <div className="flex flex-wrap items-end gap-3">
        <Button
          variant="secondary"
          aria-expanded={open}
          aria-controls={regionId}
          onClick={() => setOpen((current) => !current)}
        >
          <BarChart3 size={14} aria-hidden="true" />
          {open ? 'Hide chart' : 'Show chart'}
        </Button>
        {open && series.length > 1 && (
          <div className="flex min-w-0 max-w-full flex-col gap-1">
            <label htmlFor={`${regionId}-series`} className="text-sm font-semibold text-ink">
              Chart column
            </label>
            <select
              id={`${regionId}-series`}
              value={active.key}
              onChange={(event) => setSeriesKey(event.target.value)}
              className="w-full max-w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
            >
              {series.map((column) => (
                <option key={column.key} value={column.key}>
                  {columnHeading(column)}
                </option>
              ))}
            </select>
          </div>
        )}
      </div>

      {open && (
        <div
          id={regionId}
          className="rounded-[var(--radius-md)] border border-border bg-surface p-3"
        >
          <Suspense
            fallback={
              <div className="h-64" role="status">
                Loading chart…
              </div>
            }
          >
            <ReportChartCanvas
              points={points}
              kind={active.type === 'Percent' ? 'line' : 'bar'}
              axisLabel={axisLabel}
              seriesLabel={active.label}
              formatTick={(value) => formatAxisTick(value, currency)}
            />
          </Suspense>
          <table className="sr-only" data-testid="chart-data-table">
            <caption>
              {title}: {columnHeading(active)}
            </caption>
            <thead>
              <tr>
                {labelColumns.map((column) => (
                  <th key={column.key} scope="col">
                    {column.label}
                  </th>
                ))}
                <th scope="col">{columnHeading(active)}</th>
              </tr>
            </thead>
            <tbody>
              {points.map((point, index) => (
                <tr key={`${point.label}-${index}`}>
                  {point.parts.map((part, partIndex) => (
                    <td key={partIndex}>{part}</td>
                  ))}
                  <td>{point.text}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
