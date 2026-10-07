import { HeartCrack } from 'lucide-react';
import { formatDateOnly } from '@/lib/dateOnly';
import { cn } from '@/lib/utils';

/**
 * `components/cat/DeceasedBanner.tsx` — US-CAT-09 (HO-CAT-002 §11.7). Renders on every screen
 * for a deceased cat: in `CatProfileHeader` (with the date, component test U8) and again at the
 * top of any edit form for the cat (explaining *why* its controls are disabled — component test
 * U30, "with an explanation, not hidden"). One message satisfies both: it always states the date
 * **and** the editing consequence, so no caller has to choose between the two.
 */
export interface DeceasedBannerProps {
  deceasedOn: string | null;
  deceasedNote?: string | null;
  className?: string;
}

export function DeceasedBanner({ deceasedOn, deceasedNote, className }: DeceasedBannerProps) {
  return (
    <div
      role="status"
      className={cn(
        'flex items-start gap-3 rounded-[var(--radius-md)] border border-border-strong bg-surface-sunken p-4 text-ink',
        className,
      )}
    >
      <HeartCrack size={20} className="mt-0.5 flex-none text-ink-muted" aria-hidden="true" />
      <div>
        <p className="text-sm font-bold">
          This cat is recorded as deceased ({formatDateOnly(deceasedOn)}).
        </p>
        <p className="mt-1 text-sm text-ink-muted">
          The record is kept for reference. Editing is disabled — mark this in error? Contact an
          administrator rather than editing directly.
        </p>
        {deceasedNote && <p className="mt-1 text-sm text-ink-muted">{deceasedNote}</p>}
      </div>
    </div>
  );
}
