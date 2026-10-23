'use client';

import { useState } from 'react';
import { cn } from '@/lib/utils';
import { resolveFileUrl } from '@/lib/api/files';
import { ProductPackArt } from '@/components/hearth/ProductPackArt';
import { PACK_BG, packToneForCategory } from '@/components/hearth/packTone';
import type { ProductImageRef } from '@/lib/api/shop-catalogue';

export interface ProductGalleryProps {
  /** The selected variant's own images first, then the product's (US-SHP-02 AC1). */
  images: ProductImageRef[];
  productName: string;
  /** The product's catalogue category; picks the placeholder tint. */
  category?: string;
}

/**
 * HO-SHP-002 §11.3 `ProductGallery`, in the Hearth look: a large warm band (radius 32) holding the
 * primary image (the LCP element: eager, `fetchpriority="high"`, explicit size) and thumbnail
 * TILES that are BUTTONS named by each image's alt text (UI-6) — not an auto-rotating carousel. A
 * variant with no photo shows the tinted pack drawing instead. Key it by variant so a variant
 * change starts at its first image.
 */
export function ProductGallery({ images, productName, category }: ProductGalleryProps) {
  const tone = packToneForCategory(category);
  const [index, setIndex] = useState(0);
  // Photos that fail to load (expired link, rate limit) fall back to the pack drawing, not a broken image.
  const [failed, setFailed] = useState<ReadonlySet<string>>(new Set());
  const srcOf = (url?: string | null) => {
    const src = resolveFileUrl(url);
    return src && !failed.has(src) ? src : null;
  };
  const markFailed = (src: string) => setFailed((prev) => new Set(prev).add(src));
  const list = images.length > 0 ? images : [null];
  const current = list[Math.min(index, list.length - 1)] ?? null;
  const currentAlt = current?.altText || `${productName} — photo coming soon`;
  // Signed, API-relative file URLs (SHP row 2): resolved against the API base before use.
  const currentSrc = srcOf(current?.url);
  return (
    <div className="flex flex-col gap-3.5">
      <div
        className={cn(
          'relative grid h-[340px] place-items-center overflow-hidden rounded-band sm:h-[440px] lg:h-[520px]',
          PACK_BG[tone],
        )}
      >
        {currentSrc ? (
          // eslint-disable-next-line @next/next/no-img-element -- ADR-FE-035: no next/image remote config
          <img
            src={currentSrc}
            alt={currentAlt}
            width={600}
            height={520}
            fetchPriority="high"
            onError={() => markFailed(currentSrc)}
            className="size-full object-cover"
          />
        ) : (
          <span role="img" aria-label={currentAlt} className="grid size-full place-items-center">
            <span
              aria-hidden="true"
              className="absolute size-[min(380px,75%)] rounded-full bg-surface opacity-60"
            />
            <ProductPackArt
              tone={tone}
              className="relative h-[310px] w-[250px] rounded-none bg-transparent [&>svg]:h-full"
            />
          </span>
        )}
      </div>
      {list.length > 1 && (
        <ul aria-label="Product photos" className="m-0 grid list-none grid-cols-4 gap-3 p-0">
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
                    'grid h-24 w-full place-items-center overflow-hidden rounded-[18px] p-0',
                    PACK_BG[tone],
                    i === index ? 'border-2 border-accent' : 'border border-border',
                  )}
                >
                  {srcOf(image?.url) ? (
                    // eslint-disable-next-line @next/next/no-img-element -- ADR-FE-035
                    <img
                      src={srcOf(image?.url) ?? undefined}
                      onError={() => markFailed(srcOf(image?.url) ?? '')}
                      alt=""
                      width={96}
                      height={96}
                      loading="lazy"
                      className="size-full object-cover"
                    />
                  ) : (
                    <ProductPackArt tone={tone} className="size-full rounded-none bg-transparent" />
                  )}
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
