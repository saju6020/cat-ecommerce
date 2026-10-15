import Link from 'next/link';
import type { DashboardTile as DashboardTileData } from '@/lib/api/dashboard';
import { buildReportQuery } from '@/lib/api/reports';
import { TileFigure } from './TileFigure';

/** The report a tile opens, with the server's range and parameters. */
export function drillDownHref(drillDown: NonNullable<DashboardTileData['drillDown']>): string {
  const query = buildReportQuery(drillDown.parameters ?? {}, {
    from: drillDown.from,
    to: drillDown.to,
  });
  return `/staff/reports/${encodeURIComponent(drillDown.reportKey)}${query ? `?${query}` : ''}`;
}

/**
 * `components/dashboard/DashboardTile.tsx` — HO-RPT-002 §11.2. A card: title, one to four figures,
 * an optional note and an optional "View report →". The link is rendered ONLY when the server sent
 * a `drillDown` (it sends `null` when the caller may not open the report, OQ-4); the page never
 * decides that itself.
 */
export function DashboardTile({
  tile,
  timeZoneId,
}: {
  tile: DashboardTileData;
  timeZoneId: string;
}) {
  const headingId = `tile-${tile.key}`;
  return (
    <section
      aria-labelledby={headingId}
      className="flex min-w-0 flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      <h2 id={headingId} className="text-sm font-bold text-ink">
        {tile.title}
      </h2>
      <dl className="m-0 grid grid-cols-1 gap-3 min-[420px]:grid-cols-2">
        {tile.figures.map((figure) => (
          <TileFigure key={figure.key} figure={figure} timeZoneId={timeZoneId} />
        ))}
      </dl>
      {tile.note && <p className="m-0 text-xs text-ink-muted">{tile.note}</p>}
      {tile.drillDown && (
        <Link
          href={drillDownHref(tile.drillDown)}
          className="mt-auto text-sm font-semibold text-accent-ink underline"
        >
          View report <span aria-hidden="true">→</span>
          <span className="sr-only"> for {tile.title}</span>
        </Link>
      )}
    </section>
  );
}
