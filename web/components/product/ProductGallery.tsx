'use client';

import { useState } from 'react';
import { cn } from '@/lib/utils';
import { ProductImage, PLACEHOLDER_IMAGE } from './ProductImage';
import type { ProductImageRef } from '@/lib/api/shop-catalogue';

export interface ProductGalleryProps {
  /** The selected variant's own images first, then the product's (US-SHP-02 AC1). */
  images: ProductImageRef[];
  productName: string;
}

/**
 * HO-SHP-002 §11.3 `ProductGallery` — the primary image first (it is the LCP element: eager,
 * `fetchpriority="high"`, explicit size) and thumbnails as BUTTONS named by each image's alt text
 * (UI-6) — not an auto-rotating carousel. Key it by variant so a variant change starts at its first
 * image.
 */
export function ProductGallery({ images, productName }: ProductGalleryProps) {
  const [index, setIndex] = useState(0);
  const list = images.length > 0 ? images : [null];
  const current = list[Math.min(index, list.length - 1)] ?? null;
  return (
    <div className="flex flex-col gap-3">
      <ProductImage
        image={current}
        fallbackAlt={`${productName} — photo coming soon`}
        size={600}
        priority
      />
      {list.length > 1 && (
        <ul aria-label="Product photos" className="m-0 flex list-none flex-wrap gap-2 p-0">
          {list.map((image, i) => {
            const alt = image?.altText || `${productName}, photo ${i + 1}`;
            return (
              <li key={`${image?.url ?? 'placeholder'}-${i}`}>
                <button
                  type="button"
                  aria-label={alt}
                  aria-pressed={i === index}
                  onClick={() => setIndex(i)}
                  className={cn(
                    'h-16 w-16 overflow-hidden rounded-[var(--radius-sm)] border-2 bg-surface-sunken p-0',
                    i === index ? 'border-accent' : 'border-border',
                  )}
                >
                  {/* eslint-disable-next-line @next/next/no-img-element -- ADR-FE-035 */}
                  <img
                    src={image?.url ?? PLACEHOLDER_IMAGE}
                    alt=""
                    width={64}
                    height={64}
                    loading="lazy"
                    className="h-full w-full object-cover"
                  />
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
