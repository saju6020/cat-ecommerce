import type { ReportCell, ReportColumn } from '@/lib/api/reports';
import {
  asVarianceDirection,
  formatCell,
  NOT_RECORDED_TEXT,
  VARIANCE_DIRECTION_KEY,
} from '@/lib/format/report';
import { VarianceBadge } from './VarianceBadge';

/**
 * `components/reports/CellValue.tsx` — HO-RPT-002 §8.4. Renders one report value through
 * `formatCell` (ADR-FE-131: the single rendering path). Text is always a React text node, never
 * HTML (`dangerouslySetInnerHTML` is not used anywhere in this area). `null` is "—" plus a visually
 * hidden "Not recorded", never 0. The `varianceDirection` column is the server's word (Over / Short / Matched) as a badge with an icon (UI-6); the sign of an amount is never read.
 */
export function CellValue({
  column,
  value,
  timeZoneId,
}: {
  column: ReportColumn;
  value: ReportCell | undefined;
  timeZoneId: string;
}) {
  const text = formatCell(value, column, timeZoneId);
  if (text === NOT_RECORDED_TEXT) {
    return (
      // The wrapper is positioned on purpose: an absolutely positioned sr-only span otherwise
      // escapes the table's scroll wrapper (its containing block would be the page) and widens
      // the document.
      <span className="relative">
        {NOT_RECORDED_TEXT}
        <span className="sr-only">Not recorded</span>
      </span>
    );
  }
  // The direction column: the server's word, as a badge. A `null` never reaches here (it is "—"
  // with "Not recorded" above), so "Matched" is never shown for nothing-to-compare.
  const direction = column.key === VARIANCE_DIRECTION_KEY ? asVarianceDirection(value) : null;
  if (direction) return <VarianceBadge direction={direction} />;
  return <>{text}</>;
}

/** Numbers line up on the right with tabular figures; everything else reads left to right. */
export function isNumericColumn(column: ReportColumn): boolean {
  return ['Integer', 'Decimal', 'Money', 'Percent'].includes(column.type);
}

/** The header text: the label with its unit, e.g. "Wait (min)". */
export function columnHeading(column: ReportColumn): string {
  return column.unit ? `${column.label} (${column.unit})` : column.label;
}
