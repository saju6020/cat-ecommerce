import type { Metadata } from 'next';
import Link from 'next/link';
import {
  Archive,
  Backpack,
  Fish,
  Gamepad2,
  HeartPulse,
  Layers,
  type LucideIcon,
} from 'lucide-react';
import { BannerStrip } from '@/components/shop/BannerStrip';
import { OriginGrid } from '@/components/shop/OriginGrid';
import { ShopHero } from '@/components/shop/ShopHero';
import { ProductGrid } from '@/components/catalogue/ProductGrid';
import { MaintenanceNotice } from '@/components/shop/MaintenanceNotice';
import { PromiseStrip } from '@/components/hearth/DeliveryPromise';
import { IconTile } from '@/components/hearth/IconTile';
import {
  getBannersServer,
  getCatalogueServer,
  getCategoriesServer,
  getOriginsServer,
} from '@/lib/api/shop-catalogue';
import { EMPTY_CATALOGUE_PARAMS } from '@/lib/shop/catalogueParams';
import { categoryLabel } from '@/lib/shop/labels';
import { isMaintenanceError, settle } from '@/lib/shop/helpers';

export const metadata: Metadata = {
  title: 'PurrCare Shop — cat food, litter, carriers and toys',
  description: 'Shop cat food from named countries of origin, litter, carriers, grooming and toys.',
  alternates: { canonical: '/shop' },
};

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

const CATEGORY_ICONS: Record<string, LucideIcon> = {
  CatFood: Fish,
  Litter: Layers,
  LitterPotsAndAccessories: Archive,
  BagsAndCarriers: Backpack,
  HealthAndGrooming: HeartPulse,
  ToysAndFurniture: Gamepad2,
};

const SECTION_HEADING = 'font-display text-[clamp(1.5rem,3vw,2rem)] font-semibold text-ink m-0';

/**
 * `/shop` — HO-SHP-002 §3.2 storefront home in the Hearth layout: the Home banner as the hero,
 * new arrivals, category tiles, featured origins and the promise strip (order: owner, 2026-10-16). Server Component; every
 * block comes from a public endpoint and is simply omitted when its read fails or has nothing to
 * show.
 */
export default async function ShopHomePage({ searchParams }: { searchParams: SearchParams }) {
  const rawLang = (await searchParams).lang;
  const lang = typeof rawLang === 'string' ? rawLang : '';
  const [banners, categories, origins, arrivals] = await Promise.all([
    settle(getBannersServer('Home')),
    settle(getCategoriesServer()),
    settle(getOriginsServer()),
    settle(getCatalogueServer({ ...EMPTY_CATALOGUE_PARAMS, sort: 'newest', lang })),
  ]);
  for (const read of [categories, origins, arrivals]) {
    if (!read.ok && isMaintenanceError(read.error)) return <MaintenanceNotice />;
  }
  const featured = origins.ok ? origins.value.filter((o) => o.isFeatured) : [];
  const newest = arrivals.ok ? arrivals.value.items.slice(0, 8) : [];
  const [heroBanner, ...otherBanners] = banners.ok ? banners.value : [];

  return (
    <div className="flex flex-col gap-12">
      <div className="flex flex-col gap-4">
        <ShopHero
          banner={heroBanner ?? null}
          featured={newest[0] ?? null}
          lang={lang || undefined}
        />
        {otherBanners.length > 0 && <BannerStrip banners={otherBanners} />}
      </div>

      {newest.length > 0 && (
        <section aria-labelledby="home-new" className="flex flex-col gap-5">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <h2 id="home-new" className={SECTION_HEADING}>
              New arrivals
            </h2>
            <Link href="/shop/products" className="text-sm font-semibold text-accent-ink">
              See all products
            </Link>
          </div>
          <ProductGrid items={newest} lang={lang || undefined} headingLevel="h3" wide />
        </section>
      )}

      {categories.ok && categories.value.length > 0 && (
        <section aria-labelledby="home-categories" className="flex flex-col gap-5">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <h2 id="home-categories" className={SECTION_HEADING}>
              Shop by category
            </h2>
            <Link href="/shop/products" className="text-sm font-semibold text-accent-ink">
              All products
            </Link>
          </div>
          <ul className="m-0 grid list-none grid-cols-2 gap-4 p-0 md:grid-cols-3 lg:grid-cols-6">
            {categories.value.map((category) => {
              const Icon = CATEGORY_ICONS[category.value] ?? Fish;
              return (
                <li key={category.value}>
                  <Link
                    href={`/shop/products?category=${encodeURIComponent(category.value)}${lang ? `&lang=${encodeURIComponent(lang)}` : ''}`}
                    className="flex h-full flex-col items-center gap-3 rounded-[22px] border border-border bg-surface px-3.5 py-5 text-center text-ink no-underline hover:border-accent"
                  >
                    <IconTile size="lg">
                      <Icon size={28} strokeWidth={1.8} />
                    </IconTile>
                    <span className="text-[15px] font-semibold">
                      {categoryLabel(category.value)}
                    </span>
                    <span className="text-[13px] text-ink-muted">
                      {category.productCount} {category.productCount === 1 ? 'product' : 'products'}
                    </span>
                  </Link>
                </li>
              );
            })}
          </ul>
        </section>
      )}

      {featured.length > 0 && (
        <section aria-labelledby="home-origins" className="flex flex-col gap-5">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <h2 id="home-origins" className={SECTION_HEADING}>
              Featured origins
            </h2>
            <Link href="/shop/countries" className="text-sm font-semibold text-accent-ink">
              Shop by country
            </Link>
          </div>
          <OriginGrid origins={featured} lang={lang || undefined} headingLevel="h3" compact />
        </section>
      )}

      <PromiseStrip />
    </div>
  );
}
