import { useQuery } from '@tanstack/react-query';
import { getVaccines, type GetVaccinesParams, type VaccineListItem } from '@/lib/api/vaccines';

/** Query key convention — HO-CAT-002 §8.4: `['vaccines', filters]`. */
export function vaccinesQueryKey(filters: GetVaccinesParams) {
  return ['vaccines', filters] as const;
}

/** One page — for the vaccine catalogue list (`DataTable`'s own pagination). */
export function useVaccines(filters: GetVaccinesParams = {}) {
  return useQuery({
    queryKey: vaccinesQueryKey(filters),
    queryFn: () => getVaccines(filters),
  });
}

/**
 * Every vaccine matching `filters`, for a picker that can't page (`VaccinationForm`'s `<select>`
 * — US-CAT-05). Uses the server-reported `totalCount` (owner decision 2026-09-26, HO-CAT-001 §1
 * Amended) to compute exactly how many pages of `pageSize` (capped at 100 server-side) are
 * needed, rather than looping on a `hasNextPage` flag or a single large `pageSize` that would
 * silently truncate a catalogue past one page's worth (ARCHITECTURE gate finding on HO-CAT-002 —
 * the previous `pageSize: 50` did exactly that).
 */
// Generous, sane ceiling (LOW, carried since ARCHITECTURE) — 100 pages * 100/page = 10,000
// vaccines, far beyond any real catalogue. Guards against a runaway loop (e.g. a totalCount that
// keeps growing across requests); it must fail loudly rather than hang the tab if it's ever
// actually reached.
const MAX_VACCINE_CATALOGUE_PAGES = 100;

export function useAllVaccines(filters: Omit<GetVaccinesParams, 'pageNumber' | 'pageSize'> = {}) {
  return useQuery({
    queryKey: [...vaccinesQueryKey(filters), 'all'] as const,
    queryFn: async () => {
      const pageSize = 100;
      const items: VaccineListItem[] = [];

      const firstPage = await getVaccines({ ...filters, pageNumber: 1, pageSize });
      items.push(...firstPage.items);

      const totalPages = Math.max(1, Math.ceil(firstPage.totalCount / pageSize));
      if (totalPages > MAX_VACCINE_CATALOGUE_PAGES) {
        throw new Error(
          `useAllVaccines: the catalogue reports ${firstPage.totalCount} vaccines, which needs ` +
            `${totalPages} pages — more than the ${MAX_VACCINE_CATALOGUE_PAGES}-page ceiling.`,
        );
      }

      for (let pageNumber = 2; pageNumber <= totalPages; pageNumber += 1) {
        const page = await getVaccines({ ...filters, pageNumber, pageSize });
        items.push(...page.items);
      }

      return items;
    },
  });
}
