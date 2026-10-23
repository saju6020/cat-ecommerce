'use client';

import * as React from 'react';
import { SlidersHorizontal } from 'lucide-react';
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

  // 2026-10-16 (owner: "filter and product list alignment"): one toolbar row spans both columns
  // (the panel's title | the count, the active chips and the sort), so the filter panel and the
  // first row of products start on the same line. The panel scrolls with the page (no scrollbar of its own, owner 2026-10-17).
  return (
    <div className="flex flex-col gap-4">
      <div className="grid items-center gap-x-8 gap-y-3 md:grid-cols-[270px_minmax(0,1fr)]">
        <p className="m-0 flex items-center gap-2 text-base font-bold text-ink max-md:hidden">
          <SlidersHorizontal size={17} aria-hidden="true" className="text-ink-muted" />
          Filters
        </p>
        <div className="flex min-w-0 flex-wrap items-center justify-between gap-3">
          <div className="flex min-w-0 flex-wrap items-center gap-3">
            <FiltersSheet {...panel} count={filterCount} onShowResults={(draft) => apply(draft)} />
            {summary}
            <ActiveFilterChips
              value={params}
              origins={facets.origins}
              currencyCode={currencyCode}
              fixedKeys={fixedKeys}
              onChange={apply}
              onClearAll={clearAll}
            />
          </div>
          <SortSelect value={params.sort} hasQuery={!!params.q} onChange={setSort} />
        </div>
      </div>
      <div className="grid gap-6 md:grid-cols-[270px_minmax(0,1fr)] md:gap-8">
        <aside
          aria-label="Filters"
          className="self-start rounded-card border border-border bg-surface px-5 py-4 max-md:hidden"
        >
          <CatalogueFilters {...panel} priceMode="button" onChange={apply} />
        </aside>
        <div className="flex min-w-0 flex-col gap-4">{children}</div>
      </div>
    </div>
  );
}
