import { cn } from '@/lib/utils';

export type PackTone = 'warm' | 'teal' | 'sunken';

const TONES: Record<PackTone, string> = {
  warm: 'bg-warm-soft text-warm',
  teal: 'bg-accent-tint text-accent',
  sunken: 'bg-surface-sunken text-ink-faint',
};

/**
 * The simple tinted product-pack placeholder, for a product with no image (docs/design/orders/
 * Cart.dc.html `.tile`). Decorative. `className` sizes it (default 76px rounded tile); pass
 * `size-full rounded-none` (or similar) to fill a card's image area.
 */
export function ProductPackArt({
  tone = 'sunken',
  className,
}: {
  tone?: PackTone;
  className?: string;
}) {
  return (
    <span
      aria-hidden="true"
      className={cn(
        'grid size-[76px] flex-none place-items-center rounded-[18px]',
        TONES[tone],
        className,
      )}
    >
      <svg viewBox="0 0 120 150" className="h-[55%] w-auto" fill="currentColor">
        <path d="M22 18 Q60 4 98 18 L104 136 Q60 148 16 136 Z" />
      </svg>
    </span>
  );
}
