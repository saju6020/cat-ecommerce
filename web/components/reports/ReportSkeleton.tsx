import type { ReportCatalogueEntry } from '@/lib/api/reports';

/**
 * `components/reports/ReportSkeleton.tsx` — HO-RPT-002 §11.4, NFR-PRF-3. The loading state of the
 * rows: the REAL column headings from the cached definition over shimmering rows, so the shape of
 * the report is known before the rows arrive. The shimmer is decorative; screen readers hear one
 * "Loading …" status.
 */
export function ReportSkeleton({ definition }: { definition: ReportCatalogueEntry }) {
  return (
    <div
      role="status"
      aria-live="polite"
      className="overflow-hidden rounded-[var(--radius-md)] border border-border bg-surface"
    >
      <span className="sr-only">Loading {definition.title}…</span>
      <table aria-hidden="true" className="w-full text-sm">
        <thead className="bg-surface-sunken">
          <tr>
            {definition.columns.map((column) => (
              <th
                key={column.key}
                scope="col"
                className="px-3 py-2.5 text-left text-[11px] font-bold uppercase text-ink-muted"
              >
                {column.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {[0, 1, 2, 3].map((row) => (
            <tr key={row} className="border-t border-border">
              {definition.columns.map((column) => (
                <td key={column.key} className="px-3 py-3">
                  <div className="h-3 rounded bg-surface-sunken motion-safe:animate-pulse" />
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
