import { RefreshCw } from 'lucide-react';
import { EmptyState } from '@/components/common/EmptyState';
import type { Dashboard } from '@/lib/api/dashboard';
import { formatReportDate } from '@/lib/format/report';
import { timeOnlyInZone } from '@/lib/format/time';
import { DashboardTile } from './DashboardTile';
import { RefreshAnnouncer } from './RefreshAnnouncer';

/**
 * `components/dashboard/DashboardView.tsx` — ⭐ HO-RPT-002 §11.2, US-RPT-05. Header ("Today — date ·
 * branch", the zone, "Updated HH:mm") and the tiles the server sent, IN THE SERVER'S ORDER. Nothing
 * is added, reordered or filled in: a tile the caller may not see is not in the payload and has no
 * placeholder (ADR-FE-137). The date is the payload's `localDate` (the branch's day), never the
 * browser's today (U-A7).
 */
export function DashboardView({
  dashboard,
  updatedAt,
  refreshFailed = false,
}: {
  dashboard: Dashboard;
  updatedAt: number;
  /** The last background refresh failed; the tiles below are from the last good one. */
  refreshFailed?: boolean;
}) {
  const { timeZoneId } = dashboard;
  return (
    <div className="flex flex-col gap-5">
      <header className="flex flex-wrap items-end justify-between gap-2">
        <div className="min-w-0">
          <h1 className="font-display text-2xl font-semibold text-ink">
            Today — {formatReportDate(dashboard.localDate)} · {dashboard.branchName}
          </h1>
          <p className="mt-1 text-sm text-ink-muted">Times are in {timeZoneId}.</p>
        </div>
        <p className="inline-flex items-center gap-1.5 text-sm text-ink-muted">
          <RefreshCw size={14} aria-hidden="true" />
          Updated {timeOnlyInZone(dashboard.generatedOn, timeZoneId)} ({timeZoneId})
        </p>
      </header>

      <RefreshAnnouncer updatedAt={updatedAt} />
      {refreshFailed && (
        <p role="status" className="text-sm text-warning-ink">
          We couldn&rsquo;t refresh just now. These figures are from the last update; we&rsquo;ll
          try again.
        </p>
      )}

      {dashboard.tiles.length === 0 ? (
        <EmptyState
          title="Nothing to show for today yet"
          description={`Today's figures appear here once the areas your role works in are in use at ${dashboard.branchName}. Nothing has been recorded for ${formatReportDate(dashboard.localDate)} (${timeZoneId}) so far.`}
        />
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
          {dashboard.tiles.map((tile) => (
            <DashboardTile key={tile.key} tile={tile} timeZoneId={timeZoneId} />
          ))}
        </div>
      )}
    </div>
  );
}
