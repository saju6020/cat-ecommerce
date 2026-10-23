import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { LimitedMarkup } from '@/components/content/LimitedMarkup';
import { HearthBand } from '@/components/hearth/HearthBand';
import { MaintenanceNotice } from '@/components/shop/MaintenanceNotice';
import { FlagOrGlobe } from '@/components/shop/OriginGrid';
import { ApiNotFoundError } from '@/lib/api/client';
import { getOriginServer } from '@/lib/api/shop-catalogue';
import { isMaintenanceError, plainExcerpt, settle } from '@/lib/shop/helpers';
import { parseCatalogueParams } from '@/lib/shop/catalogueParams';
import { CatalogueView } from '../../../_components/CatalogueView';

type Props = {
  params: Promise<{ countryCode: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { countryCode } = await params;
  const origin = await settle(getOriginServer(countryCode));
  if (!origin.ok) return { title: 'Shop by country | PurrCare Shop' };
  return {
    title: `Cat food from ${origin.value.name} | PurrCare Shop`,
    description:
      plainExcerpt(origin.value.sourcingNotes, 155) ||
      `Cat food sourced from ${origin.value.name}.`,
    alternates: { canonical: `/shop/countries/${origin.value.code}` },
  };
}

/**
 * `/shop/countries/[countryCode]` — HO-SHP-002 §11.4 `OriginPage`, US-SHP-05 AC2 and the AC-5 path.
 * The country is fixed by the page (never in the query); every other filter stays available.
 */
export default async function OriginPage({ params, searchParams }: Props) {
  const { countryCode } = await params;
  const code = countryCode.toUpperCase();
  const origin = await settle(getOriginServer(code));
  if (!origin.ok) {
    if (isMaintenanceError(origin.error)) return <MaintenanceNotice />;
    if (origin.error instanceof ApiNotFoundError) notFound();
    throw origin.error;
  }
  const query = parseCatalogueParams(await searchParams);
  return (
    <div className="flex flex-col gap-6">
      <nav aria-label="Breadcrumb" className="text-[13px]">
        <Link href="/shop/countries" className="text-accent-ink">
          Shop by country
        </Link>
      </nav>
      <HearthBand as="header" className="flex flex-col gap-4 md:p-10">
        <div className="flex items-center gap-4">
          <span
            aria-hidden="true"
            className="grid size-16 flex-none place-items-center rounded-pill bg-surface"
          >
            <FlagOrGlobe code={origin.value.code} size={36} />
          </span>
          <h1 className="m-0 font-display text-[clamp(1.75rem,3.5vw,2.5rem)] font-semibold leading-tight text-ink">
            {origin.value.name}
          </h1>
        </div>
        {origin.value.sourcingNotes ? (
          <LimitedMarkup
            body={origin.value.sourcingNotes}
            className="max-w-prose text-[15px] leading-relaxed text-ink"
          />
        ) : (
          <p className="m-0 text-sm text-ink-muted">No sourcing notes yet.</p>
        )}
      </HearthBand>
      <CatalogueView
        params={query}
        basePath={`/shop/countries/${origin.value.code}`}
        fixed={{ origin: [origin.value.code] }}
      />
    </div>
  );
}
