/**
 * `components/audit/DiffTable.tsx` — HO-ADM-002 §11.7, ADR-052/SEC. Before/after JSON is parsed
 * only to walk its keys for display — the *values* are always rendered as plain text nodes
 * (React's default escaping), never through `dangerouslySetInnerHTML` or any markup parser, so a
 * value like `=HYPERLINK(...)` or `<script>` renders inert (U-G2).
 */
function safeParse(value: string | null): Record<string, unknown> | null {
  if (!value) return null;
  try {
    const parsed = JSON.parse(value);
    return parsed && typeof parsed === 'object' ? (parsed as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}

function stringify(value: unknown): string {
  if (value === undefined) return '—';
  if (value === null) return 'null';
  return typeof value === 'string' ? value : JSON.stringify(value);
}

export function DiffTable({ before, after }: { before: string | null; after: string | null }) {
  const beforeObj = safeParse(before);
  const afterObj = safeParse(after);

  if (!beforeObj && !afterObj) {
    return (
      <div className="flex flex-col gap-1 text-sm text-ink">
        {before && <p>Before: {before}</p>}
        {after && <p>After: {after}</p>}
        {!before && !after && <p className="text-ink-muted">No before/after value recorded.</p>}
      </div>
    );
  }

  const keys = Array.from(
    new Set([...Object.keys(beforeObj ?? {}), ...Object.keys(afterObj ?? {})]),
  );

  return (
    <table className="w-full border-collapse text-sm">
      <caption className="sr-only">Before and after values</caption>
      <thead>
        <tr>
          <th
            scope="col"
            className="border-b border-border p-2 text-left text-xs font-bold uppercase text-ink-muted"
          >
            Field
          </th>
          <th
            scope="col"
            className="border-b border-border p-2 text-left text-xs font-bold uppercase text-ink-muted"
          >
            Before
          </th>
          <th
            scope="col"
            className="border-b border-border p-2 text-left text-xs font-bold uppercase text-ink-muted"
          >
            After
          </th>
        </tr>
      </thead>
      <tbody>
        {keys.map((key) => (
          <tr key={key}>
            <th scope="row" className="border-b border-border p-2 text-left font-semibold text-ink">
              {key}
            </th>
            <td className="border-b border-border p-2 text-ink">{stringify(beforeObj?.[key])}</td>
            <td className="border-b border-border p-2 text-ink">{stringify(afterObj?.[key])}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
