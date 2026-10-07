import { useQuery } from '@tanstack/react-query';
import { getCatVaccinationStatus } from '@/lib/api/cats';

/** Query key convention — HO-CAT-002 §8.4: `['cat', catId, 'vaccination-status']`. */
export function catVaccinationStatusQueryKey(catId: string) {
  return ['cat', catId, 'vaccination-status'] as const;
}

export function useCatVaccinationStatus(catId: string) {
  return useQuery({
    queryKey: catVaccinationStatusQueryKey(catId),
    queryFn: () => getCatVaccinationStatus(catId),
    enabled: !!catId,
  });
}
