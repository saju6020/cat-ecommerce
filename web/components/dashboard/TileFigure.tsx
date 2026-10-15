import type { DashboardFigure } from '@/lib/api/dashboard';
import { formatFigure, NOT_RECORDED_TEXT } from '@/lib/format/report';

/**
 * `components/dashboard/TileFigure.tsx` — HO-RPT-002 §11.2. One labelled figure of a tile, rendered
 * only through `formatFigure`/`formatCell` (ADR-FE-131). A `null` value is "—" with a visually
 * hidden "Not recorded" — never a zero (ADR-FE-012). The page never reads the sign of an amount:
 * the cash-up tile's variance is shown as the amount the server sent (the server's direction word,
 * HO-RPT-001-FU1, is on the R13/R14 report rows, not in the tile payload).
 */
export function TileFigure({
  figure,
  timeZoneId,
}: {
  figure: DashboardFigure;
  timeZoneId: string;
}) {
  const text = formatFigure(figure.value, figure.type, figure.unit, timeZoneId);
  const notRecorded = text === NOT_RECORDED_TEXT;

  return (
    <div className="min-w-0">
      <dt className="text-xs font-semibold text-ink-muted">{figure.label}</dt>
      <dd className="m-0 mt-0.5 flex flex-wrap items-center gap-x-2 text-xl font-semibold tabular-nums text-ink">
        <span>{text}</span>
        {notRecorded && <span className="sr-only">Not recorded</span>}
      </dd>
    </div>
  );
}
