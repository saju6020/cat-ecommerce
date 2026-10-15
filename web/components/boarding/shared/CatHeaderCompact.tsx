import { Cat as CatIcon } from 'lucide-react';
import { HighRiskBadge } from '@/components/cat/HighRiskBadge';
import { cn } from '@/lib/utils';

/**
 * `components/boarding/shared/CatHeaderCompact.tsx` — UI-7's compact variant for every boarding
 * card, row, incident and diary entry. The stay views carry the cat's name, high-risk flag and
 * handling note on every cat (HO-BRD-001 §12.4 header), so this shows exactly those: the high-risk
 * warning in words with an icon **before the name**, and the handling note as visible text — it is
 * what a caretaker must see before opening the unit. (The full `CatProfileHeader` is on the staff
 * stay workspace.)
 */
export interface CatHeaderCompactProps {
  name: string;
  isHighRisk: boolean;
  handlingNote?: string | null;
  /** Hide the handling note for a customer-facing list that must not expose the clinical note. */
  hideNote?: boolean;
  className?: string;
  notEating?: boolean;
}

export function CatHeaderCompact({
  name,
  isHighRisk,
  handlingNote,
  hideNote,
  className,
}: CatHeaderCompactProps) {
  return (
    <div className={cn('flex min-w-0 flex-col gap-1', className)}>
      <div className="flex flex-wrap items-center gap-2">
        <CatIcon size={16} className="flex-none text-ink-muted" aria-hidden="true" />
        {isHighRisk && <HighRiskBadge handlingNote={null} variant="compact" />}
        <span className="text-[15px] font-semibold text-ink">{name}</span>
      </div>
      {isHighRisk && handlingNote && !hideNote && (
        <p className="text-[12.5px] font-semibold leading-snug text-danger-ink">{handlingNote}</p>
      )}
    </div>
  );
}
