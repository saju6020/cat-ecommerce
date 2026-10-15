import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { ApiError } from '@/lib/api/client';
import { getDashboard, type Dashboard } from '@/lib/api/dashboard';
import {
  getReportCatalogue,
  runReport,
  type ReportCatalogueEntry,
  type ReportResult,
} from '@/lib/api/reports';
import { useActiveBranchId } from './useActiveBranch';

/**
 * `lib/hooks/useReports.ts` — HO-RPT-002 §8.3. Server data is TanStack Query and nothing else
 * (no store). The catalogue and the rows are TWO queries (NFR-PRF-3): the viewer reads its
 * definition from the cached catalogue and never blocks its skeleton on it. Branch-scoped keys
 * carry the active branch id, so switching branch refetches by key and needs no manual reset.
 */
export const CATALOGUE_KEY = ['reports', 'catalogue'] as const;
const CATALOGUE_STALE_MS = 5 * 60_000;
/** A floor on the poll so a mis-set server value cannot hammer the API; the server default is 60 s. */
const MIN_REFRESH_SECONDS = 10;

/** A 4xx is an answer, not a transient failure: retrying a 403 or a 400 only delays the message. */
function retryTransient(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) return false;
  return failureCount < 1;
}

export function useReportCatalogue() {
  return useQuery<ReportCatalogueEntry[]>({
    queryKey: CATALOGUE_KEY,
    queryFn: getReportCatalogue,
    staleTime: CATALOGUE_STALE_MS,
    retry: retryTransient,
  });
}

/** One report's rows for a server query string. `enabled` stays false until the filters are known. */
export function useReportRun(reportKey: string, query: string, enabled: boolean) {
  const branchId = useActiveBranchId();
  return useQuery<ReportResult>({
    queryKey: ['report', reportKey, query, branchId],
    queryFn: () => runReport(reportKey, query),
    enabled,
    placeholderData: keepPreviousData,
    retry: retryTransient,
    staleTime: 0,
  });
}

/**
 * The home dashboard (ADR-FE-135): polls on the PAYLOAD's `refreshSeconds`, never in the
 * background, and keeps the previous tiles on screen while a refetch is in flight so an open dialog,
 * a focused control or a half-typed filter is untouched.
 */
export function useDashboard() {
  const branchId = useActiveBranchId();
  return useQuery<Dashboard>({
    queryKey: ['dashboard', branchId],
    queryFn: getDashboard,
    retry: retryTransient,
    refetchInterval: (query) => {
      const seconds = query.state.data?.refreshSeconds;
      return seconds ? Math.max(seconds, MIN_REFRESH_SECONDS) * 1000 : false;
    },
    refetchIntervalInBackground: false,
    placeholderData: keepPreviousData,
  });
}
