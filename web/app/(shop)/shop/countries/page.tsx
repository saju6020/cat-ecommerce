import type { Metadata } from 'next';
import { EmptyCatIllustration, EmptyState } from '@/components/common/EmptyState';
import { ShopLoadError } from '@/components/shop/ShopLoadError';
import { MaintenanceNotice } from '@/components/shop/MaintenanceNotice';
import { OriginGrid } from '@/components/shop/OriginGrid';
import { getOriginsServer } from '@/lib/api/shop-catalogue';
import { isMaintenanceError, settle } from '@/lib/shop/helpers';

export const metadata: Metadata = {
  title: 'Shop by country of origin | PurrCare Shop',
  description: 'See where our cat food comes from and shop by country.',
  alternates: { canonical: '/shop/countries' },
};

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

/** `/shop/countries` — HO-SHP-002 §11.4, US-SHP-05 AC1. */
export default async function CountriesPage({ searchParams }: { searchParams: SearchParams }) {
  const lang = (await searchParams).lang;
  const origins = await settle(getOriginsServer());
  if (!origins.ok) {
    if (isMaintenanceError(origins.error)) return <MaintenanceNotice />;
    return <ShopLoadError retryHref="/shop/countries" error={origins.error} />;
  }
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-2xl font-semibold text-ink">Shop by country</h1>
      <p className="max-w-prose text-sm text-ink-muted">
        We name the country of origin on every food we sell. Pick a country to see its products.
      </p>
      {origins.value.length === 0 ? (
        <EmptyState
          icon={<EmptyCatIllustration />}
          title="No countries to show yet"
          description="We will list where our food comes from as soon as products are published."
        />
      ) : (
        <OriginGrid origins={origins.value} lang={typeof lang === 'string' ? lang : undefined} />
      )}
    </div>
  );
}
