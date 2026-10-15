import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import {
  getClinicalHistory,
  getHistorySection,
  type HistorySectionKey,
} from '@/lib/api/clinical-history';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { clinicalHistoryKey } from './useVisit';

/**
 * `lib/hooks/useClinicalHistory.ts` — HO-EMR-002 §8.3, ADR-FE-056. The summary is one call
 * (`['cat', catId, 'clinical-history']`); a section's pages load ONLY when its tab opens
 * (`enabled`), and an `available: false` section is never fetched at all (U-F2). `staleTime` 60 s.
 * Signed photo URLs inside a page expire in 5 minutes, so a section query is dropped after
 * `gcTime` 4 minutes rather than shown stale for long.
 */

const HISTORY_STALE_MS = 60_000;
const SECTION_PAGE_SIZE = 20;

function noRetryOnRefusal(failureCount: number, error: unknown) {
  if (error instanceof ApiForbiddenError || error instanceof ApiNotFoundError) return false;
  return failureCount < 1;
}

export function useClinicalHistory(catId: string, enabled = true) {
  return useQuery({
    queryKey: clinicalHistoryKey(catId),
    queryFn: () => getClinicalHistory(catId),
    enabled: enabled && !!catId,
    staleTime: HISTORY_STALE_MS,
    retry: noRetryOnRefusal,
  });
}

export function useHistorySection(catId: string, section: HistorySectionKey, enabled: boolean) {
  return useInfiniteQuery({
    queryKey: [...clinicalHistoryKey(catId), section] as const,
    queryFn: ({ pageParam }) => getHistorySection(catId, section, pageParam, SECTION_PAGE_SIZE),
    initialPageParam: 1,
    getNextPageParam: (last) =>
      last.available && last.pageNumber * last.pageSize < last.totalCount
        ? last.pageNumber + 1
        : undefined,
    enabled: enabled && !!catId,
    staleTime: HISTORY_STALE_MS,
    gcTime: 4 * 60_000,
    retry: noRetryOnRefusal,
  });
}
