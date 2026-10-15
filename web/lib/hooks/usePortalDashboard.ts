import { useQuery } from '@tanstack/react-query';
import { getPortalDashboard } from '@/lib/api/portal';
import { getPublicBranches } from '@/lib/api/admin-branches';
import { ApiError } from '@/lib/api/client';

/**
 * `lib/hooks/usePortalDashboard.ts` — HO-POR-002 §8. ONE request feeds every tile (the backend
 * composes the sections and degrades each on its own, ADR-260). A section that is `Unavailable`
 * is handled per tile; this query only errors when the whole request failed. The key is exported
 * so a test can prove a re-order does NOT invalidate it (U-B5).
 */
export const PORTAL_DASHBOARD_QUERY_KEY = ['portal', 'dashboard'] as const;

export function usePortalDashboard() {
  return useQuery({
    queryKey: PORTAL_DASHBOARD_QUERY_KEY,
    queryFn: getPortalDashboard,
    // One retry for the MSW start-up race / a dropped connection; a 5xx is shown, not hidden.
    retry: (failureCount, error) =>
      error instanceof ApiError && error.status === 0 && failureCount < 1,
    retryDelay: 300,
  });
}

/** Clinic phone numbers for the "how to pay" box (U-A4). Decoration only: a failure hides the
 *  contacts, it never fails the tile. */
export function usePortalClinicContacts() {
  return useQuery({
    queryKey: ['public', 'branches'],
    queryFn: getPublicBranches,
    staleTime: 5 * 60_000,
    retry: false,
  });
}
