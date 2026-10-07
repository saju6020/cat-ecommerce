import type { Metadata } from 'next';
import { categoryLabel } from '@/lib/shop/labels';
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
  const heading = params.q
    ? `Results for “${params.q}”`
    : params.category
      ? categoryLabel(params.category)
      : 'All products';
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-2xl font-semibold text-ink">{heading}</h1>
      <CatalogueView params={params} basePath="/shop/products" />
    </div>
  );
}
