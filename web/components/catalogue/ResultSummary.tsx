/**
 * HO-SHP-002 §11.2 — "42 products for "salmon"", announced politely after each change. The element
 * stays mounted while the server re-renders the page, so the text change is what a screen reader
 * hears (UI-6).
 */
export function ResultSummary({ totalCount, q }: { totalCount: number; q: string }) {
  const noun = totalCount === 1 ? 'product' : 'products';
  return (
    <p role="status" aria-live="polite" aria-atomic="true" className="text-sm text-ink-muted">
      {totalCount} {noun}
      {q ? <> for &ldquo;{q}&rdquo;</> : null}
    </p>
  );
}
