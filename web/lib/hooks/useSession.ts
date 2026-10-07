import { useQuery } from '@tanstack/react-query';
import { getCustomerSession, type CustomerSession } from '@/lib/api/auth';
import { getStaffSessionStatus, type StaffSessionStatus } from '@/lib/api/staff-auth';
import { ApiError } from '@/lib/api/client';

/**
 * HO-ACC-002 §8.4 — "who is signed in" is server data, never a store. `getCustomerSession`/
 * `getStaffSessionStatus` return `null` on a `401` rather than throwing (that is the ordinary
 * "not signed in" outcome), so retrying is never about a 401 (which never reaches `useQuery` as an
 * error at all).
 *
 * `retry` only for a network-level failure (`ApiError` with `status === 0` — "the request never
 * reached a server", `client.ts`'s own marker), never for a genuine `5xx`: a session check is the
 * FIRST query almost every page fires, on every full navigation — including the one right after a
 * client-side sign-in push — so in development/test (HO-CAT-002's documented MSW-registration
 * race, `app/providers.tsx`) it is exactly as exposed to losing that race as `useCaptcha()` is,
 * and unlike `useCaptcha()` there is no `ADR-FE-006`-style reason to refuse a retry here — a
 * repeated GET has no "used challenge" to protect. A real `5xx` still fails permanently (no
 * retry), and the guards (`CustomerSessionGuard`/`StaffSessionGuard`) still fail CLOSED on it —
 * see their own comments.
 */
function retryOnlyNetworkError(failureCount: number, error: unknown) {
  return error instanceof ApiError && error.status === 0 && failureCount < 2;
}

export const CUSTOMER_SESSION_QUERY_KEY = ['session', 'customer'] as const;
export const STAFF_SESSION_QUERY_KEY = ['session', 'staff'] as const;

export function useCustomerSession() {
  return useQuery<CustomerSession | null>({
    queryKey: CUSTOMER_SESSION_QUERY_KEY,
    queryFn: getCustomerSession,
    retry: retryOnlyNetworkError,
    retryDelay: 300,
    staleTime: 30_000,
  });
}

export function useStaffSession() {
  return useQuery<StaffSessionStatus | null>({
    queryKey: STAFF_SESSION_QUERY_KEY,
    queryFn: getStaffSessionStatus,
    retry: retryOnlyNetworkError,
    retryDelay: 300,
    staleTime: 10_000,
  });
}

// SEC gate LOW (first pass) — sign-in success used to call a narrower, session-key-only
// `removeQueries()` helper here (kept, in spirit, in this comment): the guard the caller is about
// to navigate INTO is not mounted yet when sign-in succeeds (still on `/sign-in`), so it is not an
// "active" query observer, and `invalidateQueries()`'s default `refetchType: 'active'` would only
// mark the entry stale without actually refetching it — the guard's next mount would then read
// that stale PREVIOUS value (`null`, from before sign-in) synchronously and bounce straight back
// to sign-in. Both `(auth)/sign-in` and `(auth)/staff/sign-in` now call `queryClient.clear()`
// directly instead (their own comments) — it removes the session key exactly the same way, and
// also clears every OTHER cached query, which a bare session-key removal did not: a different
// person signing in on the same tab must never see the previous person's still-cached data.
