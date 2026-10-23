import * as React from 'react';
import { cn } from '@/lib/utils';
import { LineArtCat } from './LineArtCat';

/**
 * The Hearth empty state (docs/design/orders/MyOrders.dc.html, CartStates): a centred card with the
 * line-art cat in a warm-soft disc, a display-font headline, a sentence and the next step. Empty is a
 * calm place, not a problem, so warmth is allowed; errors never use this.
 */
export function HearthEmpty({
  title,
  description,
  action,
  hint,
  className,
}: {
  title: string;
  description?: React.ReactNode;
  action?: React.ReactNode;
  hint?: React.ReactNode;
  className?: string;
}) {
  return (
    <div
      role="status"
      aria-live="polite"
      className={cn(
        'flex flex-col items-center gap-2.5 rounded-card border border-border bg-surface p-9 text-center',
        className,
      )}
    >
      <span
        aria-hidden="true"
        className="grid size-36 place-items-center rounded-pill bg-warm-soft text-warm-ink"
      >
        <LineArtCat width={76} height={84} />
      </span>
      <p className="m-0 font-display text-[26px] font-semibold leading-tight text-ink">{title}</p>
      {description && <p className="m-0 text-ink-muted">{description}</p>}
      {action && <div className="mt-1.5">{action}</div>}
      {hint && <p className="m-0 text-xs text-ink-muted">{hint}</p>}
    </div>
  );
}
