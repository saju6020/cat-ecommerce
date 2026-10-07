'use client';

import * as React from 'react';
import { ActiveFilterChips } from '@/components/catalogue/ActiveFilterChips';
import { CatalogueFilters } from '@/components/catalogue/CatalogueFilters';
import { FiltersSheet } from '@/components/catalogue/FiltersSheet';
import { SortSelect } from '@/components/catalogue/SortSelect';
import { useCatalogueNavigation } from '@/lib/hooks/useCatalogueNavigation';
import { activeFilterCount, type CatalogueParams } from '@/lib/shop/catalogueParams';
import type { CatalogueFilters as Facets, CategoryNode } from '@/lib/api/shop-catalogue';

export interface CatalogueControlsProps {
  facets: Facets;
  categories: CategoryNode[];
  currencyCode: string;
  /** Values the page owns and the shopper cannot change (an origin page's country). */
  fixed?: Partial<CatalogueParams>;
  /** The server-rendered result summary (a polite live region) and the results themselves. */
  summary: React.ReactNode;
  children: React.ReactNode;
}

/**
 * HO-SHP-002 §8.2, ADR-FE-031 — the catalogue's interactive island. It owns NO catalogue data: it
 * reads the URL through `useCatalogueNavigation` and writes the next URL; the server re-renders the
 * results (`children`) and the summary. Desktop applies each filter on change; below 768 px the
 * same groups live in a sheet that applies once (*Show results*).
 */
export function CatalogueControls({
  facets,
  categories,
  currencyCode,
  fixed,
  summary,
  children,
}: CatalogueControlsProps) {
  const { params, apply, setSort, clearAll } = useCatalogueNavigation(fixed);
  const hideOrigin = !!fixed?.origin?.length;
  const fixedKeys = Object.keys(fixed ?? {}) as Array<keyof CatalogueParams>;
  const panel = { value: params, facets, categories, currencyCode, hideOrigin };
  // The page-owned filter (an origin page's country) is not the shopper's to count or remove.
  const filterCount = activeFilterCount({ ...params, origin: hideOrigin ? [] : params.origin });

  return (
    <div className="grid gap-6 md:grid-cols-[260px_minmax(0,1fr)]">
      <aside aria-label="Filters" className="max-md:hidden">
        <CatalogueFilters {...panel} priceMode="button" onChange={apply} />
      </aside>
      <div className="flex min-w-0 flex-col gap-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <FiltersSheet {...panel} count={filterCount} onShowResults={(draft) => apply(draft)} />
            {summary}
          </div>
          <SortSelect value={params.sort} hasQuery={!!params.q} onChange={setSort} />
        </div>
        <ActiveFilterChips
          value={params}
          origins={facets.origins}
          currencyCode={currencyCode}
          fixedKeys={fixedKeys}
          onChange={apply}
          onClearAll={clearAll}
        />
        {children}
      </div>
    </div>
  );
}
