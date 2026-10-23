import Link from 'next/link';
import { Info } from 'lucide-react';
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
          className="flex items-start gap-2.5 rounded-[28px] bg-warning-bg px-5 py-3 text-sm text-warning-ink"
        >
          <Info size={18} className="mt-0.5 flex-none" aria-hidden="true" />
          <div className="min-w-0">
            <LimitedMarkup body={banner.title} className="font-semibold" />
            <LimitedMarkup body={banner.body} />
            {isSafeLink(banner.linkUrl) && (
              <Link href={banner.linkUrl} className="font-semibold text-warning-ink underline">
                Find out more
              </Link>
            )}
          </div>
        </div>
      ))}
    </section>
  );
}
