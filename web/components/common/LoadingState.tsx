import { cn } from '@/lib/utils';

/**
 * `components/common/LoadingState.tsx` — HO-CAT-002 §11.7, Definition of Done: "Skeletons for
 * loading, not spinners, on list and profile screens."
 *
 * Renders `rows` shimmering placeholder bars (respecting `prefers-reduced-motion` via Tailwind's
 * `motion-safe:` variant — a static grey bar with no animation is still a legible loading state).
 * The visible skeleton is decorative (`aria-hidden`); the one thing a screen reader announces is
 * the `sr-only` "Loading…" text, once, via `role="status"`.
 */
export interface LoadingStateProps {
  /** How many placeholder rows/lines to render. */
  rows?: number;
  /** `table` matches a `DataTable` row's shape (one wide bar); `card` staggers width per line to
   *  read as prose, for profile/detail screens. */
  variant?: 'table' | 'card';
  label?: string;
  className?: string;
}

export function LoadingState({
  rows = 4,
  variant = 'card',
  label = 'Loading…',
  className,
}: LoadingStateProps) {
  return (
    <div role="status" aria-live="polite" className={cn('w-full', className)}>
      <span className="sr-only">{label}</span>
      <div aria-hidden="true" className="flex flex-col gap-3">
        {Array.from({ length: rows }, (_, index) => (
          <div
            key={index}
            className={cn(
              'motion-safe:animate-pulse rounded-[var(--radius-sm)] bg-surface-sunken',
              variant === 'table' ? 'h-10 w-full' : 'h-4',
            )}
            style={variant === 'card' ? { width: `${Math.max(35, 100 - index * 12)}%` } : undefined}
          />
        ))}
      </div>
    </div>
  );
}
