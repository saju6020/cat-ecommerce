/**
 * Customer (Hearth) form-control classes. Pill-shaped text inputs and selects at least 44px high;
 * a textarea takes `--radius-card`. Staff screens do not use these.
 */
export const pillInputClass =
  'min-h-11 w-full rounded-pill border border-border-strong bg-surface px-4 text-[15px] text-ink placeholder:text-ink-faint disabled:bg-surface-sunken disabled:text-ink-muted';

export const pillSelectClass = pillInputClass;

export const pillTextareaClass =
  'min-h-24 w-full rounded-card border border-border-strong bg-surface px-4 py-3 text-[15px] text-ink placeholder:text-ink-faint disabled:bg-surface-sunken disabled:text-ink-muted';
