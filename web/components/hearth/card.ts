/** Hearth card surfaces for customer screens (radius 20 = `rounded-card`; staff screens do not use these). */
export const cardClass = 'rounded-card border border-border bg-surface p-5 sm:px-6';
/** The neutral order-summary card: white, bordered, a little roomier. */
export const summaryCardClass = 'rounded-card border border-border bg-surface p-6';
/** A sunken, borderless card for secondary notes and reasons. */
export const flatCardClass =
  'rounded-card border border-surface-sunken bg-surface-sunken p-5 sm:px-6';
/** A 16px-radius inline banner (the design's `.banner`); add a tone class. */
export const bannerClass = 'flex items-start gap-3 rounded-2xl border px-[18px] py-3.5 text-sm';
export const bannerInfoClass = `${bannerClass} border-accent/20 bg-accent-tint text-accent-ink`;
export const bannerWarnClass = `${bannerClass} border-warning-ink/20 bg-warning-bg text-warning-ink`;
