'use client';

import { useState } from 'react';
import { CircleAlert, CircleCheck, CircleHelp } from 'lucide-react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { Badge } from '@/components/ui/badge';
import type {
  WebVitalMetric,
  WebVitalsSummary as Summary,
  WebVitalSummaryRow,
  WebVitalVerdict,
} from '@/lib/api/operations';

const TABS: Array<{ metric: WebVitalMetric; label: string }> = [
  { metric: 'LCP', label: 'LCP (loading)' },
  { metric: 'INP', label: 'INP (responsiveness)' },
  { metric: 'CLS', label: 'CLS (layout shift)' },
];

export function budgetFor(summary: Summary, metric: WebVitalMetric): number | null {
  if (metric === 'LCP') return summary.budgets.lcpMs;
  if (metric === 'INP') return summary.budgets.inpMs;
  if (metric === 'CLS') return summary.budgets.cls;
  return null;
}

export function formatMetric(metric: WebVitalMetric, value: number): string {
  return metric === 'CLS' ? value.toFixed(3) : `${Math.round(value)} ms`;
}

function VerdictBadge({ verdict }: { verdict: WebVitalVerdict }) {
  if (verdict === 'Pass') {
    return <Badge variant="success" icon={<CircleCheck size={12} aria-hidden="true" />}>Pass</Badge>;
  }
  if (verdict === 'Fail') {
    return <Badge variant="danger" icon={<CircleAlert size={12} aria-hidden="true" />}>Fail</Badge>;
  }
  return <Badge variant="neutral" icon={<CircleHelp size={12} aria-hidden="true" />}>Not enough data</Badge>;
}

/**
 * `components/observability/WebVitalsSummary.tsx` — HO-XCT-002 §11.5, U-B12. Real users at the 75th
 * percentile, per route and viewport class: metric tabs (LCP, INP, CLS), a table with the budget
 * beside p75 and a verdict (Pass / Fail / Not enough data, text plus icon — never colour alone), and
 * a bar chart of p75 against the budget line. Fewer than 50 samples gives no verdict (the server
 * decides; this just states it).
 */
export function WebVitalsSummary({ summary }: { summary: Summary }) {
  const [metric, setMetric] = useState<WebVitalMetric>('LCP');
  const rows: WebVitalSummaryRow[] = summary.rows.filter((row) => row.metric === metric);
  const budget = budgetFor(summary, metric);
  const chartData = rows.map((row) => ({
    label: `${row.routeTemplate} (${row.viewportClass})`,
    p75: row.p75,
    insufficient: row.verdict === 'InsufficientData',
  }));

  return (
    <div className="flex flex-col gap-4">
      <div role="tablist" aria-label="Metric" className="flex flex-wrap gap-1">
        {TABS.map((tab) => (
          <button
            key={tab.metric}
            role="tab"
            type="button"
            aria-selected={metric === tab.metric}
            onClick={() => setMetric(tab.metric)}
            className={`rounded-[var(--radius-sm)] px-3 py-1.5 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent ${
              metric === tab.metric ? 'bg-accent-soft text-accent-ink' : 'text-ink-muted hover:bg-surface-sunken'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {rows.length === 0 ? (
        <p className="rounded-[var(--radius-md)] border border-border bg-surface p-4 text-sm text-ink-muted">
          No samples for this metric in the chosen dates yet.
        </p>
      ) : (
        <>
          <div aria-hidden="true" className="h-64 w-full">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={chartData} margin={{ top: 8, right: 16, bottom: 8, left: 0 }} accessibilityLayer={false}>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
                <XAxis dataKey="label" tick={{ fontSize: 11 }} stroke="var(--ink-muted)" />
                <YAxis tick={{ fontSize: 11 }} stroke="var(--ink-muted)" />
                <Tooltip />
                {budget !== null && (
                  <ReferenceLine
                    y={budget}
                    stroke="var(--danger-ink)"
                    strokeDasharray="6 4"
                    label={{ value: `Budget ${formatMetric(metric, budget)}`, fontSize: 11 }}
                  />
                )}
                <Bar dataKey="p75" fill="var(--accent)" />
              </BarChart>
            </ResponsiveContainer>
          </div>

          <div
            role="region"
            aria-label="Web vitals by route (scrolls horizontally)"
            tabIndex={0}
            className="w-full overflow-x-auto rounded-[var(--radius-md)] border border-border"
          >
            <table className="w-full border-collapse bg-surface text-sm">
              <caption className="sr-only">p75 by route and viewport against the budget</caption>
              <thead>
                <tr className="border-b border-border bg-surface-sunken text-left text-ink-muted">
                  <th scope="col" className="px-3 py-2">Route</th>
                  <th scope="col" className="px-3 py-2">Viewport</th>
                  <th scope="col" className="px-3 py-2">Samples</th>
                  <th scope="col" className="px-3 py-2">p75</th>
                  <th scope="col" className="px-3 py-2">Budget</th>
                  <th scope="col" className="px-3 py-2">Verdict</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={`${row.routeTemplate}-${row.viewportClass}`} className="border-b border-border last:border-0">
                    <th scope="row" className="px-3 py-2 text-left font-normal text-ink">{row.routeTemplate}</th>
                    <td className="px-3 py-2 text-ink-muted">{row.viewportClass}</td>
                    <td className="px-3 py-2 text-ink-muted">{row.sampleCount}</td>
                    <td className="px-3 py-2 text-ink">{formatMetric(metric, row.p75)}</td>
                    <td className="px-3 py-2 text-ink-muted">{budget !== null ? formatMetric(metric, budget) : '—'}</td>
                    <td className="px-3 py-2"><VerdictBadge verdict={row.verdict} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      <p className="text-sm text-ink-muted">
        These are real visitors, measured at the 75th percentile: three in four visits were at least
        this good. A route with fewer than 50 samples gets no verdict yet.
      </p>
    </div>
  );
}
