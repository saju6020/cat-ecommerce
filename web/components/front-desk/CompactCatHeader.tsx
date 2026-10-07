import { Cat as CatIcon } from 'lucide-react';
import { HighRiskBadge } from '@/components/cat/HighRiskBadge';

/**
 * `components/front-desk/CompactCatHeader.tsx` — HO-APT-002 UI-7's compact variant for a queue
 * card. The queue payload carries the cat's name and high-risk flag only (HO-APT-001 §12.4), so
 * this shows exactly those — the high-risk warning in words with an icon, before the name —
 * rather than fetching a full profile per card. The full header is on the appointment detail.
 */
export function CompactCatHeader({ name, isHighRisk }: { name: string; isHighRisk: boolean }) {
  return (
    <div className="flex flex-wrap items-center gap-2">
      <CatIcon size={16} className="flex-none text-ink-muted" aria-hidden="true" />
      {isHighRisk && <HighRiskBadge handlingNote={null} variant="compact" />}
      <span className="text-[15px] font-semibold text-ink">{name}</span>
    </div>
  );
}
