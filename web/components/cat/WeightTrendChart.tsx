import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import type { CatWeightPoint } from '@/lib/api/cats';
import { formatDateOnly } from '@/lib/dateOnly';

/**
 * `components/cat/WeightTrendChart.tsx` — US-CAT-02 AC2 (HO-CAT-002 §11.7). `recharts` line
 * chart; the Y axis is labelled "kg" (UI-8). A chart has no text content a screen reader can
 * read, so a visually-hidden `<table>` of the same points sits alongside it (component test
 * U22) — that table, not the SVG, is what actually satisfies UI-6 for this component.
 */
export interface WeightTrendChartProps {
  points: CatWeightPoint[];
  className?: string;
}

export function WeightTrendChart({ points, className }: WeightTrendChartProps) {
  const data = points.map((point) => ({
    recordedOn: point.recordedOn,
    label: formatDateOnly(point.recordedOn),
    kilograms: point.kilograms,
  }));

  return (
    <div className={className}>
      <div aria-hidden="true" className="h-64 w-full">
        <ResponsiveContainer width="100%" height="100%">
          {/* HO-ACC-002 §1 Amended (2026-09-26) — `accessibilityLayer` is on by default in this
              recharts version and adds `tabIndex="0" role="application"` to the chart's own SVG
              surface for arrow-key data-point navigation. That directly conflicts with this
              chart's OWN, deliberate `aria-hidden="true"` (above) — the chart is intentionally
              non-interactive/decorative to assistive tech, since the `sr-only` <table> below is
              the real accessible representation of the same data (this file's own top comment).
              A focusable element inside an `aria-hidden` ancestor is invalid regardless of intent
              (axe `aria-hidden-focus`, WCAG 4.1.2) — disabled here since this chart was never
              meant to be keyboard-operable in the first place, not because the underlying rule is
              wrong. Found via HO-ACC-002's own e2e pass: the staff session guard/idle-lock this
              slice adds to every `/staff/**` layout delays first paint just enough that a fuller
              page load consistently lands after recharts finishes attaching this layer, where an
              unguarded page's faster load previously usually raced ahead of it — a genuine,
              pre-existing defect in this chart, made reliably visible rather than caused. */}
          <LineChart
            data={data}
            margin={{ top: 8, right: 16, bottom: 8, left: 0 }}
            accessibilityLayer={false}
          >
            <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
            <XAxis dataKey="label" tick={{ fontSize: 11 }} stroke="var(--ink-muted)" />
            <YAxis
              tick={{ fontSize: 11 }}
              stroke="var(--ink-muted)"
              label={{ value: 'kg', position: 'insideLeft', angle: -90, fontSize: 11 }}
              domain={['dataMin - 0.5', 'dataMax + 0.5']}
            />
            <Tooltip formatter={(value) => [`${value} kg`, 'Weight']} />
            <Line type="monotone" dataKey="kilograms" stroke="var(--accent)" strokeWidth={2} dot />
          </LineChart>
        </ResponsiveContainer>
      </div>

      <table className="sr-only">
        <caption>Weight history</caption>
        <thead>
          <tr>
            <th scope="col">Date</th>
            <th scope="col">Weight (kg)</th>
          </tr>
        </thead>
        <tbody>
          {data.map((point, index) => (
            // `CatWeightPoint` (Q3) carries no id from the API (HO-CAT-001 §12.3 Q3), so
            // `recordedOn` alone can collide: two weights recorded on the same calendar date
            // (intake, then again before a same-day procedure) would otherwise share a React key —
            // the same defect CODE_REVIEW found and `ee59d2a` fixed in the sibling weight-history
            // `DataTable`, just not applied here too at the time. Pair `recordedOn` with this row's
            // position, same fix shape, until the API ever adds a real id.
            <tr key={`${point.recordedOn}-${index}`}>
              <td>{point.label}</td>
              <td>{point.kilograms} kg</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
