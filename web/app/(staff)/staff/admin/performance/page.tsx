'use client';

import { useMemo, useState } from 'react';
import { format, subDays } from 'date-fns';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { WebVitalsSummary } from '@/components/observability/WebVitalsSummary';
import { ApiForbiddenError } from '@/lib/api/client';
import { useWebVitalsSummary } from '@/lib/hooks/useOperations';

const DEFAULT_RANGE_DAYS = 28;
const MAX_RANGE_DAYS = 93;
type ViewportFilter = '' | 'Narrow' | 'Medium' | 'Wide';

/** `/staff/admin/performance` — US-XCT-02. Storefront web vitals at p75 from real visitors. */
export default function PerformancePage() {
  const defaults = useMemo(() => {
    const now = new Date();
    return { from: format(subDays(now, DEFAULT_RANGE_DAYS - 1), 'yyyy-MM-dd'), to: format(now, 'yyyy-MM-dd') };
  }, []);
  const [from, setFrom] = useState(defaults.from);
  const [to, setTo] = useState(defaults.to);
  const [viewport, setViewport] = useState<ViewportFilter>('');

  const days = Math.round((Date.parse(to) - Date.parse(from)) / 86_400_000) + 1;
  const rangeError =
    !from || !to || Number.isNaN(days) || days < 1
      ? 'Choose a start date on or before the end date.'
      : days > MAX_RANGE_DAYS
        ? `Choose at most ${MAX_RANGE_DAYS} days.`
        : null;

  const enabledReady = rangeError === null;
  const summary = useWebVitalsSummary(
    { from, to, viewportClass: viewport || undefined },
    enabledReady,
  );

  if (summary.error instanceof ApiForbiddenError) return <PermissionDeniedState />;

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl font-semibold text-ink">Storefront performance</h1>
      <form className="flex flex-wrap items-end gap-3 text-sm" onSubmit={(e) => e.preventDefault()}>
        <div className="flex flex-col gap-1">
          <label htmlFor="perf-from" className="font-semibold text-ink">From</label>
          <input id="perf-from" type="date" value={from} onChange={(e) => setFrom(e.target.value)}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 py-1.5 text-ink" />
        </div>
        <div className="flex flex-col gap-1">
          <label htmlFor="perf-to" className="font-semibold text-ink">To</label>
          <input id="perf-to" type="date" value={to} onChange={(e) => setTo(e.target.value)}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 py-1.5 text-ink" />
        </div>
        <div className="flex flex-col gap-1">
          <label htmlFor="perf-viewport" className="font-semibold text-ink">Screen size</label>
          <select id="perf-viewport" value={viewport} onChange={(e) => setViewport(e.target.value as ViewportFilter)}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 py-1.5 text-ink">
            <option value="">All</option>
            <option value="Narrow">Narrow (phones)</option>
            <option value="Medium">Medium (tablets)</option>
            <option value="Wide">Wide (desktops)</option>
          </select>
        </div>
      </form>
      {rangeError && <p role="alert" className="text-sm text-danger-ink">{rangeError}</p>}

      {!enabledReady ? null : summary.isLoading ? (
        <LoadingState rows={5} label="Loading performance…" />
      ) : summary.data ? (
        <WebVitalsSummary summary={summary.data} />
      ) : (
        <ErrorState description="We couldn't load the performance figures just now." onRetry={() => void summary.refetch()} />
      )}
    </div>
  );
}
