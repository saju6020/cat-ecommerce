import { Check, TrendingUp, TriangleAlert } from 'lucide-react';
import type { VarianceDirection } from '@/lib/format/report';

/**
 * `components/reports/VarianceBadge.tsx` — HO-RPT-002 UI-6, HO-RPT-001-FU1. A variance direction as a
 * WORD and an icon, never colour alone: **Over** (more than expected), **Short** (less than
 * expected), **Matched**. The word comes from the server's `varianceDirection`; this component
 * never looks at an amount. Shared by the report table, the card list and any figure that carries a
 * direction.
 */
export function VarianceBadge({ direction }: { direction: VarianceDirection }) {
  const base = 'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-semibold';
  if (direction === 'Short') {
    return (
      <span className={`${base} bg-danger-bg text-danger-ink`}>
        <TriangleAlert size={12} aria-hidden="true" /> Short
      </span>
    );
  }
  if (direction === 'Over') {
    return (
      <span className={`${base} bg-warning-bg text-warning-ink`}>
        <TrendingUp size={12} aria-hidden="true" /> Over
      </span>
    );
  }
  return (
    <span className={`${base} bg-accent-soft text-accent-ink`}>
      <Check size={12} aria-hidden="true" /> Matched
    </span>
  );
}
