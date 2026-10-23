import type { Metadata } from 'next';
import { categoryLabel, subcategoryLabel } from '@/lib/shop/labels';
import { parseCatalogueParams } from '@/lib/shop/catalogueParams';
import { CatalogueView } from '../../_components/CatalogueView';

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export async function generateMetadata({
  searchParams,
}: {
  searchParams: SearchParams;
}): Promise<Metadata> {
  const params = parseCatalogueParams(await searchParams);
  const title = params.q
    ? `Search results for “${params.q}” | PurrCare Shop`
    : params.category
      ? `${categoryLabel(params.category)} | PurrCare Shop`
      : 'Cat food, litter and more | PurrCare Shop';
  return {
    title,
    description:
      'Browse cat food, litter, carriers, grooming and toys. Filter by country of origin, brand and life stage.',
    alternates: { canonical: '/shop/products' },
  };
}

/**
 * `/shop/products` — HO-SHP-002 §3.2, US-SHP-04/06. A Server Component (NFR-PRF-1, ADR-FE-030): all
 * state is in the URL (ADR-FE-031); the page reads it through the one parser and the server returns
 * the filtered, sorted, paged results.
 */
export default async function CataloguePage({ searchParams }: { searchParams: SearchParams }) {
  const params = parseCatalogueParams(await searchParams);
  // The title follows what is shown (2026-10-16): "Cat food · Wet", or a single chosen brand.
  const heading = params.q
    ? `Results for “${params.q}”`
    : params.category
      ? params.subcategory
        ? `${categoryLabel(params.category)} · ${subcategoryLabel(params.subcategory)}`
        : categoryLabel(params.category)
      : params.brand.length === 1
        ? params.brand[0]!
        : 'All products';
  return (
    <div className="flex flex-col gap-6">
      <h1 className="m-0 font-display text-[clamp(1.75rem,3.5vw,2.5rem)] font-bold leading-tight tracking-[-0.025em] text-ink">
        {heading}
      </h1>
      <CatalogueView params={params} basePath="/shop/products" />
    </div>
  );
}
