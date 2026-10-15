/**
 * Shared Tailwind class strings for the surgery screens (HO-SRG-002). The tokens are the existing design
 * tokens (`ink`, `border-strong`, `accent`, `danger-ink`…) — nothing new is introduced.
 */
export const inputClass =
  'w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink ' +
  'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:opacity-50 ' +
  'aria-[invalid=true]:border-danger-ink';

export const labelClass = 'text-sm font-semibold text-ink';
export const hintClass = 'text-xs text-ink-muted';
export const errorClass = 'text-xs font-semibold text-danger-ink';
export const cardClass = 'rounded-[var(--radius-md)] border border-border bg-surface p-4';
export const sectionHeadingClass = 'font-display text-lg font-semibold text-ink';
