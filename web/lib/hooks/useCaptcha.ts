import { useQuery } from '@tanstack/react-query';
import { getCaptcha } from '@/lib/api/auth';
import { ApiError } from '@/lib/api/client';

/**
 * HO-ACC-002 §8.5 — the CAPTCHA challenge's own caching rule, distinct from every other query in
 * the app: `staleTime: Infinity` (never silently refetched), `gcTime: 0` (never served stale from
 * a previous mount once this component unmounts), no refetch on window focus or reconnect — a
 * solved-then-abandoned challenge must not quietly reappear. Callers `refetch()` explicitly: on
 * "New code", after every failed submit (ADR-FE-006), and ~10s before `expiresInSeconds` elapses.
 *
 * `retry`: ADR-FE-006 rules out a silent auto-retry of a REAL failure (`CaptchaField`'s own
 * "couldn't load" state and its retry button are the answer to that) — but a network-level failure
 * (`ApiError` with `status === 0`, `client.ts`'s own marker for "the request never reached a
 * server at all") never issued a challenge to lose in the first place, so retrying THAT kind of
 * failure a couple of times is not "silently retrying a used challenge", it's recovering from a
 * request that never happened. In practice this is what saves the very first CAPTCHA fetch on a
 * page load from HO-CAT-002's own documented MSW-registration race (`app/providers.tsx`'s comment)
 * in development/test, which every other query "papers over" with the default `retry: 1` — this
 * query cannot use that default (it WOULD silently retry a real `CaptchaInvalid`-shaped failure),
 * so it needs its own, narrower rule instead of none at all.
 */
export function useCaptcha() {
  return useQuery({
    queryKey: ['auth', 'captcha'],
    queryFn: getCaptcha,
    staleTime: Infinity,
    gcTime: 0,
    retry: (failureCount, error) =>
      error instanceof ApiError && error.status === 0 && failureCount < 2,
    retryDelay: 300,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
}
