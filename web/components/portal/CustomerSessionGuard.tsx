'use client';

import { useEffect } from 'react';
import { useRouter, usePathname } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useCustomerSession } from '@/lib/hooks/useSession';
import { setSessionErrorHandler } from '@/lib/api/client';
import { LoadingState } from '@/components/common/LoadingState';

/**
 * `components/portal/CustomerSessionGuard.tsx` — HO-ACC-002 §11.5, US-ACC-04. Wraps every
 * `(portal)` page's content (not the header chrome, which renders regardless so the shell itself
 * never flashes):
 *  - pending → a full-height loading state, `children` never rendered (U-C3);
 *  - `null` (no session) → `redirect('/sign-in?next=' + encodeURIComponent(pathname))`;
 *  - present → renders `children`.
 *
 * A query error (a network failure, a 5xx from the session endpoint itself — distinct from the
 * ordinary `401` `getCustomerSession()` already turns into a `null` result) is treated the SAME
 * as "no session", not as "session present": failing open here — rendering portal content because
 * the one call that would have said otherwise happened to fail — is a bigger risk than an
 * occasional unnecessary bounce to sign-in.
 *
 * ARCH gate HIGH 2 (first pass) — this is also where the customer-audience `client.ts` session-
 * error handler is registered (any OTHER call than the session check itself getting a `401`,
 * e.g. a role/permission change mid-session). Registering it HERE, rather than once globally with
 * a guessed audience, is exactly the fix: only `(portal)`'s own guard registers "route to
 * `/sign-in`", only `(staff)`'s (`IdleLock`, which also needs the idle-timeout split) registers
 * "route to `/staff/sign-in`" — see that component's own comment — and since Next.js only ever
 * mounts one of the two route groups' layouts for a given URL, there is never a question of which
 * one is "active" for the request that just failed.
 */
export function CustomerSessionGuard({ children }: { children: React.ReactNode }) {
  const { data: session, isPending, isError } = useCustomerSession();
  const router = useRouter();
  const pathname = usePathname();
  const queryClient = useQueryClient();
  const authenticated = !isPending && !isError && session !== null;
  const unauthenticated = !isPending && (isError || session === null);

  useEffect(() => {
    if (unauthenticated) {
      // HO-APT-002 §8.1 (Amended 2026-10-04): the query string rides along, so a reminder link to
      // `/book?catId=…&category=…` comes back with its booking parameters after sign-in. The
      // `?next=` open-redirect guard (`sanitizeNextPath`) still decides what is honoured.
      const search = typeof window !== 'undefined' ? window.location.search : '';
      router.replace(`/sign-in?next=${encodeURIComponent(pathname + search)}`);
    }
  }, [unauthenticated, pathname, router]);

  useEffect(() => {
    setSessionErrorHandler(() => {
      // A customer session never idle-locks (US-ACC-08 is staff-only) — every session-error body
      // reaching this handler means "not signed in any more", full stop.
      queryClient.clear();
      // Same as the first redirect above: the query string rides along (`/book?catId=…`).
      const next =
        typeof window !== 'undefined' ? window.location.pathname + window.location.search : '/';
      router.replace(`/sign-in?next=${encodeURIComponent(next)}`);
    });
    return () => setSessionErrorHandler(null);
  }, [queryClient, router]);

  if (isPending) {
    return (
      <div className="flex min-h-[60vh] items-center justify-center">
        <LoadingState variant="card" rows={4} label="Loading your account…" />
      </div>
    );
  }

  if (!authenticated) return null; // redirect effect above is already navigating away

  return <>{children}</>;
}
