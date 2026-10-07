'use client';

import { useEffect } from 'react';
import { useRouter, usePathname } from 'next/navigation';
import { useStaffSession } from '@/lib/hooks/useSession';
import { StaffSessionIdleTimeoutError } from '@/lib/api/staff-auth';
import { LoadingState } from '@/components/common/LoadingState';
import { useStaffLockState } from './IdleLockContext';

/**
 * `components/staff/StaffSessionGuard.tsx` — HO-ACC-002 §11.9, US-ACC-14. Same shape as
 * `CustomerSessionGuard`: pending → loading, never flashing staff content; `null` or a query
 * error (fail closed, not open — see that component's own comment) → redirect to
 * `/staff/sign-in?next=...`; present → render `children`. This is what replaces `lib/identity.ts`
 * for every existing HO-CAT-002 staff page (unchanged otherwise, ADR-FE-004).
 *
 * ARCH gate HIGH 1 (first pass) — a `StaffSessionIdleTimeoutError` is the one query error this
 * guard does NOT treat as "no session": §8.4 requires the idle lock to take over, never a
 * redirect, and that only works if `children` (which is `<IdleLock>{...}</IdleLock>`, HO-ACC-002
 * §11.9) stays mounted. `IdleLock` itself is the thing that reacts to this error by actually
 * locking (its own comment) — this guard's only job here is to not unmount it.
 *
 * ARCH gate HIGH (second pass) — that was only half the fix. The real API ends the staff session
 * on the FIRST idle-timeout detection and expires `pc_staff_session`; every LATER staff-session
 * read then resolves to a plain `null` (`session_invalid`, not `session_idle_timeout` again) —
 * indistinguishable, to this guard, from a genuine sign-out, unless it also knows the terminal is
 * currently locked (`useStaffLockState()`, provided one level up by `StaffLockProvider` in
 * `(staff)/layout.tsx` — see `IdleLockContext.tsx`'s own comment for why it cannot live below this
 * guard). While `locked`, `children` (`IdleLock`) stays mounted and this guard never redirects,
 * regardless of what the session query reports — the lock's own re-authentication is what resolves
 * it, exactly like the very first idle-timeout error already did.
 */
export function StaffSessionGuard({ children }: { children: React.ReactNode }) {
  const { data: session, isPending, isError, error } = useStaffSession();
  const router = useRouter();
  const pathname = usePathname();
  const { locked } = useStaffLockState();
  const isIdleTimeout = error instanceof StaffSessionIdleTimeoutError;
  const authenticated = !isPending && (isIdleTimeout || locked || (!isError && session !== null));
  const unauthenticated = !isPending && !isIdleTimeout && !locked && (isError || session === null);

  useEffect(() => {
    if (unauthenticated) {
      router.replace(`/staff/sign-in?next=${encodeURIComponent(pathname)}`);
    }
  }, [unauthenticated, pathname, router]);

  if (isPending) {
    return (
      <div className="flex min-h-[60vh] items-center justify-center">
        <LoadingState variant="card" rows={4} label="Loading…" />
      </div>
    );
  }

  if (!authenticated) return null;

  return <>{children}</>;
}
