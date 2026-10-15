'use client';

import {
  Bar,
  BarChart,
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

export interface ChartPoint {
  label: string;
  value: number | null;
  /** The server's own text for the value (`formatCell`), used in the tooltip. */
  text: string;
}

/**
 * `components/reports/ReportChartCanvas.tsx` — HO-RPT-002 §11.3, ADR-FE-134. The `recharts` surface
 * only; loaded lazily by `ReportChart` so the page is fast with the chart closed. Decorative to
 * assistive tech (`aria-hidden`, `accessibilityLayer={false}`): the visually hidden data table in
 * `ReportChart` is the accessible representation of the same points. A bar chart for counts and
 * money, a line for percentages (OQ-3).
 */
export default function ReportChartCanvas({
  points,
  kind,
  axisLabel,
  seriesLabel,
  formatTick,
}: {
  points: ChartPoint[];
  kind: 'bar' | 'line';
  axisLabel: string;
  seriesLabel: string;
  /** Formats a Y-axis tick as a compact label with the currency sign (`formatAxisTick`). */
  formatTick: (value: number) => string;
}) {
  const margin = { top: 8, right: 16, bottom: 8, left: 16 };
  const axes = (
    <>
      <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
      <XAxis dataKey="label" tick={{ fontSize: 11 }} stroke="var(--ink-muted)" />
      <YAxis
        width={84}
        tick={{ fontSize: 11 }}
        stroke="var(--ink-muted)"
        tickFormatter={(value: number) => formatTick(value)}
        label={{ value: axisLabel, position: 'insideLeft', angle: -90, fontSize: 11, dx: -12 }}
      />
      <Tooltip
        formatter={(_value, _name, item) => [(item.payload as ChartPoint).text, seriesLabel]}
      />
    </>
  );

  return (
    <div aria-hidden="true" className="h-64 w-full">
      <ResponsiveContainer width="100%" height="100%">
        {kind === 'line' ? (
          <LineChart data={points} margin={margin} accessibilityLayer={false}>
            {axes}
            <Line type="monotone" dataKey="value" stroke="var(--accent)" strokeWidth={2} dot />
          </LineChart>
        ) : (
          <BarChart data={points} margin={margin} accessibilityLayer={false}>
            {axes}
            <Bar dataKey="value" fill="var(--accent)" />
          </BarChart>
        )}
      </ResponsiveContainer>
    </div>
  );
}
