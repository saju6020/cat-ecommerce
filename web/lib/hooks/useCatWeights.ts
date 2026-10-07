import { useQuery } from '@tanstack/react-query';
import { getCatWeights, getCatWeightSeries, type GetCatWeightsParams } from '@/lib/api/cats';

/** Query key convention — HO-CAT-002 §8.4: `['cat', catId, 'weights']`, narrowed by params. */
export function catWeightsQueryKey(catId: string, params: GetCatWeightsParams) {
  return ['cat', catId, 'weights', params] as const;
}

/** The weight history table — paginated (UI-4). */
export function useCatWeights(catId: string, params: GetCatWeightsParams = {}) {
  return useQuery({
    queryKey: catWeightsQueryKey(catId, params),
    queryFn: () => getCatWeights(catId, params),
    enabled: !!catId,
  });
}

/** The full series for `WeightTrendChart` (US-CAT-02 AC2) — a chart cannot page. */
export function useCatWeightSeries(catId: string) {
  return useQuery({
    queryKey: ['cat', catId, 'weights', 'series'] as const,
    queryFn: () => getCatWeightSeries(catId),
    enabled: !!catId,
  });
}
