import { AlertTriangle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';

/**
 * `components/cat/HighRiskBadge.tsx` — US-CAT-04 AC1 (HO-CAT-002 §11.4).
 *
 * Icon + the literal text "High risk — handle with care" + the handling note — never colour
 * alone (UI-6). `variant="full"` (the profile header, §11.3) renders the note inline;
 * `variant="compact"` (portal cat list, `app/(portal)/portal/cats/page.tsx`) renders just the
 * badge — a customer-facing list has no need to expose the clinical note text.
 *
 * `variant="list"` (the **staff** cat list, `app/(staff)/staff/cats/page.tsx`) renders the badge
 * plus a truncated line of the handling note underneath, with the full text available via the
 * native `title` tooltip on hover/focus (the DOM text itself is never truncated — screen readers
 * always read the whole note regardless of the CSS `truncate`). **Owner decision, 2026-09-27
 * (HO-CAT-002 §1 Amended)**: staff must see handling warnings without opening each cat, which
 * supersedes this file's own earlier "a list row has no room for the note" reasoning for the
 * *staff* list specifically — `variant="compact"` (the portal list) is unchanged.
 *
 * Uses `--danger-bg`/`--danger-ink` (not `--warning-*`) — docs/design/cat-profile/README.md's
 * design-system table puts "Overdue, high risk, destructive" on the danger pair specifically
 * because the warning pair's contrast is weaker; check contrast before changing either.
 */
export interface HighRiskBadgeProps {
  handlingNote: string | null;
  variant?: 'full' | 'compact' | 'list';
  className?: string;
}

export function HighRiskBadge({ handlingNote, variant = 'full', className }: HighRiskBadgeProps) {
  if (variant === 'compact') {
    return (
      <Badge
        variant="danger"
        icon={<AlertTriangle size={12} aria-hidden="true" />}
        className={className}
      >
        High risk
      </Badge>
    );
  }

  if (variant === 'list') {
    return (
      <div className={cn('flex flex-col items-start gap-0.5', className)}>
        <Badge variant="danger" icon={<AlertTriangle size={12} aria-hidden="true" />}>
          High risk
        </Badge>
        {handlingNote && (
          <span
            title={handlingNote}
            className="max-w-[9rem] truncate text-[11.5px] text-danger-ink sm:max-w-[16rem]"
          >
            {handlingNote}
          </span>
        )}
      </div>
    );
  }

  return (
    <div
      role="alert"
      className={cn(
        'flex items-start gap-3 rounded-[var(--radius-md)] border border-danger-ink/20 bg-danger-bg p-4 text-danger-ink',
        className,
      )}
    >
      <AlertTriangle size={20} className="mt-0.5 flex-none" aria-hidden="true" />
      <div>
        <p className="text-sm font-bold">High risk — handle with care</p>
        {handlingNote && <p className="mt-1 text-sm">{handlingNote}</p>}
      </div>
    </div>
  );
}
