import Link from 'next/link';
import { LimitedMarkup } from '@/components/content/LimitedMarkup';
import { isSafeLink } from '@/lib/shop/helpers';
import type { StorefrontBanner } from '@/lib/api/shop-catalogue';

/**
 * HO-SHP-002 §11.1 `BannerStrip` — FR-ADM-7. Each banner's title and body go through HO-ADM-002's
 * `LimitedMarkup`; a link only when one is present (and safe); no auto-rotation, no carousel.
 */
export function BannerStrip({ banners }: { banners: StorefrontBanner[] }) {
  if (banners.length === 0) return null;
  return (
    <section aria-label="Announcements" className="flex flex-col gap-2">
      {banners.map((banner) => (
        <div
          key={banner.slug}
          className="rounded-[var(--radius-md)] border border-border bg-warm-soft px-4 py-3 text-warm-ink"
        >
          <LimitedMarkup body={banner.title} className="font-semibold" />
          <LimitedMarkup body={banner.body} className="text-sm" />
          {isSafeLink(banner.linkUrl) && (
            <Link href={banner.linkUrl} className="text-sm font-semibold underline">
              Find out more
            </Link>
          )}
        </div>
      ))}
    </section>
  );
}
