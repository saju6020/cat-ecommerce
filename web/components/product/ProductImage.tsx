import type { ProductImageRef } from '@/lib/api/shop-catalogue';
import { cn } from '@/lib/utils';

export const PLACEHOLDER_IMAGE = '/images/product-placeholder.svg';

export interface ProductImageProps {
  image: ProductImageRef | null | undefined;
  /** Used when the API sends no image at all. */
  fallbackAlt: string;
  size: number;
  /** The LCP image: eager, high priority. Everything else is lazy (NFR-PRF-1). */
  priority?: boolean;
  className?: string;
}

/**
 * HO-SHP-002 §11.3, ADR-FE-035, NFR-PRF-1 — a plain `<img>` (no `next/image`: its remote
 * configuration lives in `next.config.ts`, which this handover may not touch). `url` is always `null`
 * until HO-FIL-001 signed URLs are wired (backlog SHP row 2), so the in-repo placeholder renders —
 * with the alt text the API sent. Explicit `width`/`height` avoid layout shift.
 */
export function ProductImage({ image, fallbackAlt, size, priority, className }: ProductImageProps) {
  return (
    // eslint-disable-next-line @next/next/no-img-element -- ADR-FE-035: no next/image remote config
    <img
      src={image?.url ?? PLACEHOLDER_IMAGE}
      alt={image?.altText || fallbackAlt}
      width={size}
      height={size}
      loading={priority ? undefined : 'lazy'}
      decoding={priority ? undefined : 'async'}
      {...(priority ? { fetchPriority: 'high' as const } : {})}
      className={cn(
        'h-auto w-full rounded-[var(--radius-sm)] bg-surface-sunken object-cover',
        className,
      )}
    />
  );
}
