/**
 * U-A9 — fixed-height placeholders so tiles arriving do not shift the layout. Decorative
 * (`aria-hidden`); one `sr-only` line is the only thing a screen reader announces.
 */
const TILE_HEIGHTS = ['h-56', 'h-44', 'h-44', 'h-60', 'h-40', 'h-40'] as const;

export function DashboardSkeleton() {
  return (
    <div role="status" aria-live="polite" data-testid="dashboard-skeleton">
      <span className="sr-only">Loading your home page…</span>
      <div aria-hidden="true" className="flex flex-col gap-5">
        <div className="h-24 rounded-[var(--radius-md)] bg-warm-soft motion-safe:animate-pulse" />
        <div className="grid gap-4 [grid-template-columns:repeat(auto-fit,minmax(min(100%,420px),1fr))]">
          {TILE_HEIGHTS.map((height, index) => (
            <div
              key={index}
              className={`${height} rounded-[var(--radius-md)] border border-border bg-surface-sunken motion-safe:animate-pulse ${
                index === 0 || index === 3 ? 'col-span-full' : ''
              }`}
            />
          ))}
        </div>
      </div>
    </div>
  );
}
