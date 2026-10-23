import Link from 'next/link';
import { ArrowRight, Check } from 'lucide-react';
import { LimitedMarkup } from '@/components/content/LimitedMarkup';
import { HearthBand } from '@/components/hearth/HearthBand';
import { LineArtCat } from '@/components/hearth/LineArtCat';
import { buttonVariants } from '@/components/ui/button';
import { formatPriceRange } from '@/lib/format/shop';
import { availabilityText } from '@/lib/shop/labels';
import { isSafeLink, plainExcerpt, productHref } from '@/lib/shop/helpers';
import type { CatalogueItem, StorefrontBanner } from '@/lib/api/shop-catalogue';

const FALLBACK_HEADLINE = 'Everything your cat needs, with the country of origin on every food';

/**
 * The page's own headline, styled (owner picked option C, 2026-10-16): "Everything" very large in teal extra-bold (Inter, 2026-10-16),
 * "your cat needs," below it, and the promise as a small capitals line with "country of origin" on an apricot
 * pill. The text — and so the heading's accessible name — is exactly FALLBACK_HEADLINE.
 */
function FancyHeadline() {
  return (
    <>
      <span className="block text-[1.45em] font-extrabold leading-[0.95] tracking-[-0.035em] text-accent">
        Everything
      </span>{' '}
      <span className="block">your cat needs,</span>{' '}
      <span className="mt-4 block font-sans text-[max(13px,0.36em)] font-bold uppercase leading-[1.9] tracking-[0.05em] text-ink-muted [text-wrap:balance]">
        with the{' '}
        <span className="mx-[0.2em] inline-block whitespace-nowrap rounded-pill bg-warm px-[0.8em] py-[0.15em] leading-normal text-ink">
          country of origin
        </span>{' '}
        on every food
      </span>
    </>
  );
}

/**
 * The storefront home hero (docs/design/shop/ShopHome.dc.html). It is built from the Home banner
 * the API sends: its title is the headline, its body the lead and its link the main button. The
 * API sends no eyebrow line and no highlighted phrase, so the design's eyebrow pill and italic
 * phrase are not drawn. With no banner the page's own headline is used. The floating card shows
 * one real product (the newest), only when there is one.
 */
export function ShopHero({
  banner,
  featured,
  lang,
}: {
  banner: StorefrontBanner | null;
  featured: CatalogueItem | null;
  lang?: string;
}) {
  const bannerHeadline = banner ? plainExcerpt(banner.title, 120) : '';
  const headline = bannerHeadline || FALLBACK_HEADLINE;
  const bannerLink = banner && isSafeLink(banner.linkUrl) ? banner.linkUrl : null;
  return (
    <HearthBand
      aria-labelledby="hero-title"
      className="grid items-center gap-8 overflow-hidden px-6 py-10 md:px-12 md:py-14 lg:grid-cols-[minmax(0,1.15fr)_minmax(0,1fr)] lg:pl-16"
    >
      <div className="flex flex-col gap-5">
        <h1
          id="hero-title"
          className="m-0 font-display text-[clamp(2.25rem,5vw,3.5rem)] font-bold leading-[1.05] tracking-[-0.03em] text-ink"
        >
          {headline === FALLBACK_HEADLINE ? <FancyHeadline /> : headline}
        </h1>
        {banner?.body && (
          <LimitedMarkup
            body={banner.body}
            className="max-w-[460px] text-[17px] leading-relaxed text-ink-muted"
          />
        )}
        <div className="flex flex-wrap gap-3">
          <Link
            href={bannerLink ?? '/shop/products'}
            className={buttonVariants({ variant: 'pill', size: 'pillMd' })}
          >
            {bannerLink ? 'Find out more' : 'Shop all products'}
            <ArrowRight size={16} aria-hidden="true" />
          </Link>
          <Link
            href="/shop/countries"
            className={buttonVariants({ variant: 'pillSecondary', size: 'pillMd' })}
          >
            Shop by country
          </Link>
        </div>
      </div>
      <div className="relative hidden h-[340px] lg:block">
        <div
          aria-hidden="true"
          className="absolute right-10 top-2.5 grid size-[300px] place-items-center rounded-pill bg-surface text-warm-ink"
        >
          <LineArtCat />
        </div>
        {featured && (
          <Link
            href={productHref(featured.slug, { lang })}
            className="absolute bottom-[18px] left-0 flex w-[270px] items-center gap-3 rounded-[18px] border border-border bg-surface px-4 py-3.5 text-ink no-underline"
          >
            <span
              aria-hidden="true"
              className="grid size-11 flex-none place-items-center rounded-[12px] bg-accent-tint text-accent"
            >
              <Check size={22} />
            </span>
            <span className="flex min-w-0 flex-col">
              <strong className="truncate text-sm">{featured.name}</strong>
              <span className="mt-0.5 text-[13px] text-ink-muted">
                {availabilityText(featured.availability)} ·{' '}
                {formatPriceRange(featured.fromPrice, featured.priceVaries)}
              </span>
            </span>
          </Link>
        )}
      </div>
    </HearthBand>
  );
}
