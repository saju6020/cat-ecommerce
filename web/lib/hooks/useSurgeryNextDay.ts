import { useQueries } from '@tanstack/react-query';
import { getSurgeryAvailability, type AvailabilityParams } from '@/lib/api/surgeries';
import { addDaysIso } from '@/lib/format/appointment';
import { useBranchScope } from './useBranchScope';

/**
 * `lib/hooks/useSurgeryNextDay.ts` — HO-SRG-002 §11.1 part 3. When a day has no common start, the next
 * day (up to 7 ahead) that does, fetched ahead from the SAME availability endpoint — the browser never
 * works out a free time itself. Returns `undefined` while looking, `null` when none is free in the week.
 */
export function useSurgeryNextDay(
  params: AvailabilityParams | null,
  enabled: boolean,
): string | null | undefined {
  const { branchId, ready } = useBranchScope();
  const dates = params ? Array.from({ length: 7 }, (_, i) => addDaysIso(params.date, i + 1)) : [];
  const results = useQueries({
    queries: dates.map((date) => ({
      queryKey: ['surgeries', 'availability', { ...params, date }, branchId],
      queryFn: () => getSurgeryAvailability({ ...(params as AvailabilityParams), date }),
      enabled: ready && enabled && params !== null,
      staleTime: 30_000,
      retry: false,
    })),
  });
  if (!enabled || !params) return undefined;
  const firstFree = results.findIndex((r) => (r.data?.starts.length ?? 0) > 0);
  if (firstFree >= 0 && results.slice(0, firstFree).every((r) => !r.isLoading)) {
    return dates[firstFree];
  }
  if (results.some((r) => r.isLoading)) return undefined;
  return null;
}
