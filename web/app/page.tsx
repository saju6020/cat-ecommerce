'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useCustomerSession, useStaffSession } from '@/lib/hooks/useSession';
import { StaffSessionIdleTimeoutError } from '@/lib/api/staff-auth';
import { LoadingState } from '@/components/common/LoadingState';
import { DEFAULT_CUSTOMER_LANDING } from '@/lib/routing/portal-links';

/**
 * HO-ACC-002 §1 Amended (2026-09-26, owner decision) — supersedes HO-CAT-002 §8.3's unconditional
 * `redirect('/staff/cats')`, and this file's own prior "redirect to whichever area registered a
 * nav item first" behaviour (`nav/index.ts`'s `navRegistry` order): neither asked who was actually
 * visiting. A signed-in customer belongs in their own portal; a signed-in staff member belongs in
 * the staff app; everyone else belongs at customer sign-in — never at staff sign-in, which would
 * hand an anonymous visitor a staff-flavoured screen by default.
 *
 * SECURITY gate item 7 (first-pass gate fix round, 2026-09-26) — this used to be an async Server
 * Component reading `cookies().has('pc_staff_session' | 'pc_session')` directly
 * (`next/headers`). That only ever worked by coincidence in local development, where the web app's
 * own Next.js server and the API share a hostname (`localhost`, different ports only — cookie
 * scoping ignores port). ADR-FE-002's actual production topology puts the web app and the API on
 * two different subdomains (`www.example.com` / `api.example.com`); a request arriving at the web
 * app's own Next.js server never carries a cookie scoped to the API's origin at all — this page
 * would have read "signed out" for literally everyone in production, regardless of whether they
 * actually had a live session, and always redirected to `/sign-in`.
 *
 * Fixed by asking the API itself, client-side, the same way `CustomerSessionGuard`/
 * `StaffSessionGuard` already do (`useCustomerSession()`/`useStaffSession()`, a real
 * `credentials: 'include'` fetch to the API's own origin, which the browser attaches the right
 * cookies to regardless of how many subdomains apart the two origins are). Fails CLOSED to
 * `/sign-in` — an error, a network failure, or a `session_invalid` on either query is never treated
 * as "signed in"; only a real, resolved session (or, for staff, a `session_idle_timeout` — the
 * account is still real, just locked, and belongs on `/staff` (the home dashboard, HO-RPT-002 ADR-FE-130) where `IdleLock` shows the lock
 * overlay) redirects anywhere else.
 *
 * `/portal/cats` and `/staff/cats` are not a new convention invented here — they are the exact same
 * defaults `(auth)/sign-in` and `(auth)/staff/sign-in` already `router.push()` to after a real
 * sign-in (`lib/validation/nextPath.ts`'s `sanitizeNextPath` calls in both files).
 */
/** HO-RPT-002 ADR-FE-130 — the staff landing route is the role home dashboard, not `/staff/cats` (which stays in the nav). */
const STAFF_HOME = '/staff';

export default function HomePage() {
  const router = useRouter();
  const staffSession = useStaffSession();
  const customerSession = useCustomerSession();

  const isStaffIdleTimeout = staffSession.error instanceof StaffSessionIdleTimeoutError;
  const isStaffSignedIn = Boolean(staffSession.data) || isStaffIdleTimeout;
  const isCustomerSignedIn = Boolean(customerSession.data);
  const isResolved = (!staffSession.isPending || isStaffIdleTimeout) && !customerSession.isPending;

  useEffect(() => {
    if (!isResolved) return;
    if (isStaffSignedIn) {
      router.replace(STAFF_HOME);
    } else if (isCustomerSignedIn) {
      router.replace(DEFAULT_CUSTOMER_LANDING);
    } else {
      router.replace('/shop');
    }
  }, [isResolved, isStaffSignedIn, isCustomerSignedIn, router]);

  return (
    <div className="flex min-h-screen items-center justify-center p-6">
      <div className="w-full max-w-sm">
        <LoadingState variant="card" rows={2} label="Loading…" />
      </div>
    </div>
  );
}
