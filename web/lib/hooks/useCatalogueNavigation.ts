'use client';

import { useCallback, useMemo, useState } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import {
  clearFilters,
  parseCatalogueParams,
  toQueryString,
  withFilterChange,
  withoutFixed,
  type CatalogueParams,
} from '@/lib/shop/catalogueParams';

/**
 * HO-SHP-002 §8.4, ADR-FE-031 — the catalogue's URL state. The URL is the only source of truth
 * (the server is the only filter engine): this hook parses it through the ONE parser
 * (`catalogueParams`) and writes changes back. A filter or sort change uses `router.replace` (no
 * history spam; a new filter starts at page 1, sort keeps every filter); a page change uses
 * `router.push`, so Back returns to the previous page of the same results (E-A2).
 *
 * Why not `useUrlFilterState`/`useFilterUrlUpdate` (HO-ADM-002)? They model single-valued
 * `name -> string` fields and a `page` param; the catalogue has repeated values (`brand=A&brand=B`)
 * and the API's `pageNumber`, and its controls are checkboxes, not typing bursts.
 *
 * `fixed` pins values the page owns (an origin page's `origin`) — they are always re-applied and
 * never offered as removable.
 *
 * ADR-FE-020's reason, for checkboxes: a router navigation is async, so a control bound straight to
 * the URL would snap back (and lose a quick second click) until the navigation lands. `pending` is
 * the value the shopper just chose — shown at once and merged into the next change — and is dropped
 * the moment the URL catches up (or moves elsewhere: Back, a link).
 */
export function useCatalogueNavigation(fixed: Partial<CatalogueParams> = {}) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const queryKey = searchParams.toString();
  const fixedKey = JSON.stringify(fixed);

  const fromUrl = useMemo(
    () => ({ ...parseCatalogueParams(new URLSearchParams(queryKey)), ...fixed }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [queryKey, fixedKey],
  );
  // The chosen value is tagged with the URL it was chosen on and only counts while the URL is
  // still that one. Dropping it in an effect left one stale frame after Back (S15); deriving it
  // makes it vanish in the same render the URL changes.
  const [pending, setPending] = useState<{ value: CatalogueParams; queryKey: string } | null>(null);
  const params = pending && pending.queryKey === queryKey ? pending.value : fromUrl;

  const hrefFor = useCallback(
    (next: CatalogueParams) => `${pathname}${toQueryString(withoutFixed(next, fixed))}`,
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [pathname, fixedKey],
  );

  const navigate = useCallback(
    (next: CatalogueParams, how: 'replace' | 'push') => {
      setPending({ value: next, queryKey });
      const url = hrefFor(next);
      if (how === 'push') router.push(url);
      else router.replace(url, { scroll: false });
    },
    [hrefFor, router, queryKey],
  );

  /** Replace the filters/sort/search: the page resets to 1. */
  const apply = useCallback(
    (patch: Partial<CatalogueParams>) => navigate(withFilterChange(params, patch), 'replace'),
    [navigate, params],
  );

  /** Sorting keeps every filter and the search; the page resets (the order changed). */
  const setSort = useCallback(
    (sort: CatalogueParams['sort']) => navigate({ ...params, sort, pageNumber: 1 }, 'replace'),
    [navigate, params],
  );

  const goToPage = useCallback(
    (pageNumber: number) => navigate({ ...params, pageNumber }, 'push'),
    [navigate, params],
  );

  const clearAll = useCallback(
    () => navigate(clearFilters(params, fixed), 'replace'),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [navigate, params, fixedKey],
  );

  return { params, apply, setSort, goToPage, clearAll, hrefFor };
}
