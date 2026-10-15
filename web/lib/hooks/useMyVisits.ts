import { useQuery } from '@tanstack/react-query';
import { getMyCatVisits, getMyLabResults, getMyVisitSummary } from '@/lib/api/my-visits';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';

/**
 * `lib/hooks/useMyVisits.ts` — HO-EMR-002 §8.3 (US-CLN-16, FR-POR-3). The owner's finalised visits,
 * lab results and one discharge summary. Lab results carry signed attachment URLs, so that query has
 * a 4-minute `staleTime`. A `404` is never retried and renders the neutral message.
 */

function noRetryOnRefusal(failureCount: number, error: unknown) {
  if (error instanceof ApiForbiddenError || error instanceof ApiNotFoundError) return false;
  return failureCount < 1;
}

export function useMyCatVisits(catId: string) {
  return useQuery({
    queryKey: ['my', 'cat', catId, 'visits'],
    queryFn: () => getMyCatVisits(catId),
    enabled: !!catId,
    retry: noRetryOnRefusal,
  });
}

export function useMyLabResults(catId: string) {
  return useQuery({
    queryKey: ['my', 'cat', catId, 'lab-results'],
    queryFn: () => getMyLabResults(catId),
    enabled: !!catId,
    staleTime: 4 * 60_000,
    retry: noRetryOnRefusal,
  });
}

export function useMyVisitSummary(visitId: string) {
  return useQuery({
    queryKey: ['my', 'visit', visitId],
    queryFn: () => getMyVisitSummary(visitId),
    enabled: !!visitId,
    retry: noRetryOnRefusal,
  });
}
