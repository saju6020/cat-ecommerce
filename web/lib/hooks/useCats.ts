import { useQuery } from '@tanstack/react-query';
import { getCats, type GetCatsParams } from '@/lib/api/cats';

/** Query key convention — HO-CAT-002 §8.4: `['cats', filters]`. */
export function catsQueryKey(filters: GetCatsParams) {
  return ['cats', filters] as const;
}

export function useCats(filters: GetCatsParams = {}) {
  return useQuery({
    queryKey: catsQueryKey(filters),
    queryFn: () => getCats(filters),
  });
}
