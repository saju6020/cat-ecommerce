import { CloudOff } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

/**
 * `components/common/ErrorState.tsx` — HO-CAT-002 §11.7, Definition of Done, and NFR-USA-4 ("the
 * message shown is ours, never the server's"). Matches `docs/design/portal/BalancesStates.dc.html`
 * / `CatTileStates.dc.html`'s "unavailable" pattern: an icon that isn't alarm-red (`--ink-muted`,
 * not `--danger-ink` — this is "couldn't load", not "something is wrong with your data"), a bold
 * one-line title, a reassuring caption, and a **Try again** button that refetches.
 *
 * Never pass `error.message` from an `ApiError` straight into `description` — map it to a
 * plain-language string first (HO-CAT-002 §11.8's "never render errorCode" rule applies to raw
 * server/error text generally, not only `errorCode`).
 */
export interface ErrorStateProps {
  title?: string;
  description?: string;
  onRetry?: () => void;
  retryLabel?: string;
  /** ADM backlog 2 — render the title as an <h{n}> instead of a <p>, for a full-page use where the
   *  error IS the page and would otherwise leave it with no heading to navigate by. */
  headingLevel?: 1 | 2 | 3;
  className?: string;
}

export function ErrorState({
  title = "We couldn't load this just now.",
  description = 'This does not mean there is nothing here — please try again.',
  onRetry,
  retryLabel = 'Try again',
  headingLevel,
  className,
}: ErrorStateProps) {
  const TitleTag = headingLevel ? (`h${headingLevel}` as 'h1' | 'h2' | 'h3') : 'p';
  return (
    <div
      role="status"
      aria-live="polite"
      className={cn(
        'flex items-start gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-6',
        className,
      )}
    >
      <CloudOff size={20} className="mt-0.5 flex-none text-ink-muted" aria-hidden="true" />
      <div className="flex-1">
        <TitleTag className="text-[15px] font-semibold text-ink">{title}</TitleTag>
        <p className="mt-1 text-sm text-ink-muted">{description}</p>
        {onRetry && (
          <Button variant="ghost" size="sm" className="mt-3 -ml-2" onClick={onRetry}>
            {retryLabel}
          </Button>
        )}
      </div>
    </div>
  );
}
