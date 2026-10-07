import * as React from 'react';
import { Inbox } from 'lucide-react';
import { cn } from '@/lib/utils';

/**
 * The warm-customer-layer line-art cat, copied verbatim from `docs/design/portal/
 * DashboardNewCustomer.dc.html`'s `.empty-cat` (also used by `CatalogueNoResults.dc.html` and
 * `MySubscriptionsEmpty.dc.html` — at least three portal-facing areas beyond Cat Profiles, so it
 * lives here rather than being re-copied per area). Sits in a `--warm-soft` disc — only ever
 * meaningful inside `[data-theme="customer"]` (app/(portal)/layout.tsx); never pass this as
 * `EmptyState`'s icon on a staff screen (HO-CAT-002 §8.5's `--warm` contrast note, and the
 * task's "staff empty states stay neutral" rule).
 */
export function EmptyCatIllustration({ className }: { className?: string }) {
  return (
    <span
      aria-hidden="true"
      className={cn(
        'flex h-16 w-16 flex-none items-center justify-center rounded-full bg-warm-soft text-warm-ink',
        className,
      )}
    >
      <svg
        width="38"
        height="38"
        viewBox="0 0 64 64"
        fill="none"
        stroke="currentColor"
        strokeWidth="2.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      >
        <path d="M19 23 17 9l10 7M45 23l2-14-10 7" />
        <path d="M18 26c0-7 6-11 14-11s14 4 14 11-6 11-14 11-14-4-14-11Z" />
        <path d="M26 25v1.5M38 25v1.5M30 30.5l2 1.5 2-1.5" />
        <path d="M22 36c-4 6-5 13-3 21h26c2-8 1-15-3-21" />
        <path d="M45 55c6 0 9-4 7.5-9" />
      </svg>
    </span>
  );
}

/** The default neutral icon for a staff (or icon-less) empty state — never the warm cat art. */
function NeutralIllustration({ className }: { className?: string }) {
  return (
    <span
      aria-hidden="true"
      className={cn(
        'flex h-16 w-16 flex-none items-center justify-center rounded-full bg-surface-sunken text-ink-faint',
        className,
      )}
    >
      <Inbox size={28} aria-hidden="true" />
    </span>
  );
}

/**
 * `components/common/EmptyState.tsx` — HO-CAT-002 §11.7, Definition of Done: "`EmptyState` says
 * what to do next, not 'No data'." Every prop that isn't the title is deliberately there to force
 * that: `description` for the plain-language "why", `action` for the next step.
 *
 * `role="status" aria-live="polite"` matches `docs/design/shop/CatalogueNoResults.dc.html` — an
 * empty state that replaces a previous loading/list state is announced once, the same way a
 * search-result count changing is.
 */
export interface EmptyStateProps {
  title: string;
  description?: React.ReactNode;
  action?: React.ReactNode;
  /** Pass `<EmptyCatIllustration />` on a portal screen with nothing to show yet; pass `null` to
   *  render no icon at all. Defaults to a neutral icon — staff empty states stay neutral. */
  icon?: React.ReactNode | null;
  className?: string;
}

export function EmptyState({ title, description, action, icon, className }: EmptyStateProps) {
  const resolvedIcon = icon === undefined ? <NeutralIllustration /> : icon;

  return (
    <div
      role="status"
      aria-live="polite"
      className={cn(
        'flex flex-wrap items-center gap-5 rounded-[var(--radius-md)] border border-border bg-surface p-7',
        className,
      )}
    >
      {resolvedIcon}
      <div className="min-w-[220px] flex-1">
        <p className="text-[15px] font-semibold text-ink">{title}</p>
        {description && <p className="mt-1.5 text-sm text-ink-muted">{description}</p>}
        {action && <div className="mt-3">{action}</div>}
      </div>
    </div>
  );
}
