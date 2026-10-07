import type { Metadata } from 'next';
import Link from 'next/link';
import { BannerStrip } from '@/components/shop/BannerStrip';
import { OriginGrid } from '@/components/shop/OriginGrid';
import { ProductGrid } from '@/components/catalogue/ProductGrid';
import { MaintenanceNotice } from '@/components/shop/MaintenanceNotice';
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

/**
 * `/shop` — HO-SHP-002 §3.2 storefront home: Home banners, categories, featured origins and new
 * arrivals. Server Component; every block comes from a public endpoint and is simply omitted when
 * its read fails or has nothing to show.
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

  return (
    <div className="flex flex-col gap-10">
      <h1 className="font-display text-3xl font-semibold text-ink">
        Everything your cat needs, with the country of origin on every food
      </h1>
      {banners.ok && <BannerStrip banners={banners.value} />}

      {categories.ok && categories.value.length > 0 && (
        <section aria-labelledby="home-categories" className="flex flex-col gap-3">
          <h2 id="home-categories" className="font-display text-xl font-semibold text-ink">
            Shop by category
          </h2>
          <ul className="grid list-none grid-cols-2 gap-3 p-0 md:grid-cols-3">
            {categories.value.map((category) => (
              <li key={category.value}>
                <Link
                  href={`/shop/products?category=${encodeURIComponent(category.value)}${lang ? `&lang=${encodeURIComponent(lang)}` : ''}`}
                  className="flex h-full flex-col gap-1 rounded-[var(--radius-md)] border border-border bg-surface p-4 text-ink no-underline hover:bg-surface-sunken"
                >
                  <span className="text-base font-semibold">{categoryLabel(category.value)}</span>
                  <span className="text-sm text-ink-muted">
                    {category.productCount} {category.productCount === 1 ? 'product' : 'products'}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        </section>
      )}

      {featured.length > 0 && (
        <section aria-labelledby="home-origins" className="flex flex-col gap-3">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <h2 id="home-origins" className="font-display text-xl font-semibold text-ink">
              Featured origins
            </h2>
            <Link href="/shop/countries" className="text-sm font-semibold text-accent-ink">
              Shop by country
            </Link>
          </div>
          <OriginGrid origins={featured} lang={lang || undefined} headingLevel="h3" />
        </section>
      )}

      {newest.length > 0 && (
        <section aria-labelledby="home-new" className="flex flex-col gap-3">
          <h2 id="home-new" className="font-display text-xl font-semibold text-ink">
            New arrivals
          </h2>
          <ProductGrid items={newest} lang={lang || undefined} headingLevel="h3" />
        </section>
      )}
    </div>
  );
}
