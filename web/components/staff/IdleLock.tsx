'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useRouter, usePathname } from 'next/navigation';
import { Dialog, DialogContent } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { IdleLockDialogContent } from './IdleLockDialogContent';
import { useStaffSession, STAFF_SESSION_QUERY_KEY } from '@/lib/hooks/useSession';
import { useRecordStaffActivity, useSignInStaff } from '@/lib/hooks/useStaffAuthMutations';
import { setSessionErrorHandler } from '@/lib/api/client';
import { clearQueryCacheKeepingBranchFor, forgetPreservedBranch } from '@/lib/branch';
import { useStaffLockState } from './IdleLockContext';
import {
  getStaffSessionStatus,
  StaffSessionIdleTimeoutError,
  type StaffSessionStatus,
  type StaffSignInResult,
} from '@/lib/api/staff-auth';
import type { CustomerSignInResult } from '@/lib/api/auth';

const WARNING_LEAD_MS = 2 * 60_000; // OQ-4 default: warn 2 minutes before idleExpiresOn
const ACTIVITY_THROTTLE_MS = 60_000; // at most one heartbeat per 60s
const ACTIVITY_EVENTS = ['keydown', 'pointerdown', 'input'] as const;

/**
 * `components/staff/IdleLock.tsx` — US-ACC-08, HO-ACC-002 §11.9, ADR-FE-010. Wraps `{children}`
 * inside the `(staff)` guard: the page stays mounted throughout, so every form's react-hook-form
 * state survives re-authentication untouched.
 *
 *  - Warning at `idleExpiresOn − 2min`, computed against `serverTime` (never the client clock —
 *    `clockOffset` corrects for a skewed local clock, U-G2).
 *  - Activity heartbeat on real interaction only, throttled to once per 60s — never a timer.
 *  - Lock on a `session_idle_timeout` from ANY staff-audience call (this component registers
 *    itself as `client.ts`'s session-error handler, ARCH gate HIGH 2 first pass) or when the
 *    local countdown reaches zero: a modal `Dialog` (focus trap, `Escape` disabled, background
 *    `inert`/`aria-hidden` all from Radix) with an embedded staff `SignInForm`.
 *  - Unlock only when the re-signed-in `staffAccountId` matches the one held before the lock; the
 *    refused request is never replayed automatically. A different person → discard notice, clear
 *    the whole cache, reload the current route.
 */
export function IdleLock({ children }: { children: React.ReactNode }) {
  const { data: initialStatus, error: sessionError } = useStaffSession();
  const { locked, lock, unlock } = useStaffLockState();
  const [status, setStatus] = useState<StaffSessionStatus | null>(null);
  const [warning, setWarning] = useState(false);
  const [personChanged, setPersonChanged] = useState(false);
  const [idleTimeoutMinutesFromError, setIdleTimeoutMinutesFromError] = useState<
    number | undefined
  >(undefined);

  const statusRef = useRef<StaffSessionStatus | null>(null);
  const lockedRef = useRef(locked);
  const lockedStaffIdRef = useRef<string | null>(null);
  // ACC backlog 8 — set in an effect (StrictMode-safe) so the async bystander resolution can tell
  // that this guard unmounted (e.g. the user already navigated away) while it awaited.
  const mountedRef = useRef(false);
  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);
  const lastActivityPostRef = useRef(0);
  const warningTimerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const lockTimerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  const activityMutation = useRecordStaffActivity();
  // ARCH gate HIGH 3 (first pass) — `IdleLock` is one of §11.3/§11.5/§11.9's named exemptions
  // (guards, `IdleLock`, `SignOutButton`, the header session reads) from "components take props,
  // pages wire hooks", so calling this directly here (for the embedded re-authentication form
  // below) rather than needing a page to supply it is correct, not an oversight.
  const reSignInMutation = useSignInStaff();
  const queryClient = useQueryClient();
  const router = useRouter();
  const pathname = usePathname();

  // CODE_REVIEW suggestion (first pass) — `useMutation()` returns a new object every render, so
  // keeping it out of the activity-listener effect's own deps (via this ref) means that effect's
  // add/removeEventListener churn only tracks `locked`, not every render of this component.
  const activityMutationRef = useRef(activityMutation);
  useEffect(() => {
    activityMutationRef.current = activityMutation;
  }, [activityMutation]);

  useEffect(() => {
    if (initialStatus && !statusRef.current) setStatus(initialStatus);
  }, [initialStatus]);

  useEffect(() => {
    statusRef.current = status ?? initialStatus ?? null;
  }, [status, initialStatus]);

  // ARCHITECTURE gate third pass, LOW — `lockedRef` used to be synced from `locked` in its own
  // passive effect, which only runs AFTER the triggering state update commits and this component
  // re-renders. The session-error handler below reads `lockedRef` from an event callback that can
  // run in that same window — between `lock()` being called and the effect actually running — so a
  // `session_invalid` processed in that gap would still see the stale (pre-lock) value and redirect,
  // exactly the race ADR-FE-010 exists to close. `lockNow`/`handleReSignInSuccess` are the only two
  // places `lock()`/`unlock()` are ever called from in this component, so setting the ref
  // synchronously, in the same call, right alongside each one, closes the gap completely rather than
  // narrowing it.
  const lockNow = useCallback(() => {
    lockedStaffIdRef.current = statusRef.current?.staffAccountId ?? null;
    setWarning(false);
    lockedRef.current = true;
    lock();
  }, [lock]);

  // TEST gate F1 (this pass) — a `session_invalid` reaching the handler below is NOT reliably a
  // genuine sign-out just because `lockedRef.current` is still `false` at that instant. The real
  // API ends a staff session on the FIRST request that detects the idle timeout and reports
  // `session_idle_timeout` only to THAT request; every OTHER staff-audience call racing it in the
  // same window-focus refetch batch — including this component's OWN `useStaffSession()` query, if
  // its request is the one that loses that race — sees `session_invalid` instead, indistinguishable
  // on its own from a genuinely revoked/unknown session. Deciding from this one response alone is
  // exactly the race: this callback runs the instant THIS request's response resolves (`client.ts`
  // calls it synchronously while parsing the 401 body, no React involved), while the direct
  // `session_idle_timeout` → `lockNow()` path below needs `useStaffSession()`'s own query to settle
  // AND a React re-render to commit — strictly more hops — so a bystander can win even when the
  // session query's own request reached the server first and is the one that actually holds
  // `session_idle_timeout`.
  //
  // Fix: route the decision through one authoritative, serialized check of the session-status
  // query itself, instead of trusting the bystander's own response. `queryClient.fetchQuery` on
  // `STAFF_SESSION_QUERY_KEY` reuses an already in-flight fetch for that key rather than starting a
  // second one (`Query.fetch()` in `@tanstack/query-core` returns the existing retryer's promise
  // when `fetchStatus !== 'idle'` — verified against the installed 5.103.2) — so in exactly the
  // scenario this race occurs in (the session query and the bystander query refetched by the same
  // focus event), this performs no extra network round trip and resolves to the one true outcome
  // for this idle window: if the session query's own request is the one that detected the idle
  // timeout, this throws `StaffSessionIdleTimeoutError` the same way the effect below would have,
  // and locks; if the session is genuinely gone (revoked, unknown, or any other real sign-out), the
  // authoritative check confirms that independently and the original clear-and-redirect still runs.
  // If the race resolves the other way in the meantime (the effect below wins and calls `lockNow()`
  // first), `lockedRef.current` is already `true` by the time this resolves and it is a no-op.
  //
  // CODE_REVIEW gate seventh pass, HIGH — `staleTime: 0` here is load-bearing, not decorative.
  // Without it, `fetchQuery`'s own `defaultedOptions` inherit the app-wide default
  // (`app/providers.tsx`, `staleTime: 30_000`), and `fetchQuery` only calls `query.fetch()` at all
  // when `query.isStaleByTime(staleTime)` is true (`queryClient.js`) — with the 30s default, a
  // session read that succeeded less than 30s ago (an entirely ordinary case: the countdown effect
  // below also runs off `useStaffSession()`'s own data, so a recent successful read is common) would
  // make `fetchQuery` skip the fetch ENTIRELY and resolve straight to that cached "still signed in"
  // data — silently ignoring a session that has genuinely just ended. `staleTime: 0` forces
  // `isStaleByTime` to `true` unconditionally, so this call always attempts a fetch — which is
  // exactly the dedupe path above when one is already in flight (the branch inside `Query.fetch()`
  // that returns the existing retryer's promise is unaffected by `staleTime`, since it is checked
  // BEFORE any options are consulted), and a genuine fresh fetch otherwise.
  const resolveBystanderSessionInvalid = useCallback(async () => {
    // CODE_REVIEW gate seventh pass, LOW — captured before the `await`, not after: if this
    // bystander loses the race (the direct `session_idle_timeout` path, or another bystander,
    // already navigated by the time this resolves), `window.location.pathname` would otherwise
    // read back `/staff/sign-in` itself, producing a useless `?next=%2Fstaff%2Fsign-in`.
    const next = typeof window !== 'undefined' ? window.location.pathname : pathname;
    try {
      const authoritative = await queryClient.fetchQuery<StaffSessionStatus | null>({
        queryKey: STAFF_SESSION_QUERY_KEY,
        queryFn: getStaffSessionStatus,
        staleTime: 0,
      });
      if (!mountedRef.current || lockedRef.current) return;
      if (authoritative) {
        // The session turned out to still be valid (e.g. it was renewed by the time this
        // authoritative check ran) — nothing to sign out of.
        setStatus(authoritative);
        return;
      }
      clearQueryCacheKeepingBranchFor(queryClient, statusRef.current?.staffAccountId);
      router.replace(`/staff/sign-in?next=${encodeURIComponent(next)}`);
    } catch (error) {
      if (!mountedRef.current) return;
      if (error instanceof StaffSessionIdleTimeoutError) {
        setIdleTimeoutMinutesFromError(error.idleTimeoutMinutes);
        lockNow();
        return;
      }
      if (lockedRef.current) return;
      clearQueryCacheKeepingBranchFor(queryClient, statusRef.current?.staffAccountId);
      router.replace(`/staff/sign-in?next=${encodeURIComponent(next)}`);
    }
  }, [lockNow, queryClient, router, pathname]);

  // ARCH gate HIGH 2 (first pass) — this IS the staff-audience `client.ts` session-error handler
  // (`CustomerSessionGuard`'s own comment explains why registration moved here, per-guard, rather
  // than staying one global handler with a guessed audience). Two distinct outcomes for the two
  // distinct error shapes, exactly per §8.4: `session_idle_timeout` (from ANY staff-audience call,
  // not only this component's own `useStaffSession()` — the effect below that one covers the
  // session-status endpoint specifically) locks in place; anything else (`session_invalid`) is
  // routed through `resolveBystanderSessionInvalid` above rather than acted on directly (TEST gate
  // F1 — see that function's own comment).
  //
  // ARCH gate HIGH (second pass) — the "anything else" branch used to fire even while ALREADY
  // locked. The real API ends the staff session on the first idle-timeout detection, so every later
  // staff-audience call — including a page query refetched on the same focus event that
  // re-triggered the lock, or a submit attempted just before it — reports `session_invalid`, not
  // `session_idle_timeout` again. That is not a new sign-out; it is the SAME idle session, already
  // handled. Reading `lockedRef` (not `locked` in the closure, so this handler does not need to be
  // re-registered on every lock/unlock) makes `session_invalid` a no-op while locked: no cache
  // clear, no redirect. The lock's own re-authentication (`handleReSignInSuccess` below) is what
  // actually resolves it, exactly like the direct idle-timeout path already does.
  useEffect(() => {
    setSessionErrorHandler((body) => {
      if (body.error === 'session_idle_timeout') {
        setIdleTimeoutMinutesFromError(body.idleTimeoutMinutes);
        lockNow();
        return;
      }
      if (lockedRef.current) return;
      void resolveBystanderSessionInvalid();
    });
    return () => setSessionErrorHandler(null);
  }, [lockNow, resolveBystanderSessionInvalid]);

  // ARCH gate HIGH 1 (first pass) — the OTHER path into the lock: `useStaffSession()`'s own
  // refetch (e.g. `refetchOnWindowFocus`, staleTime 10s) can surface a `session_idle_timeout`
  // directly, from the session-status endpoint itself (which the handler above does not cover —
  // it always sets `skipSessionErrorHandling`, §8.4). `StaffSessionGuard` deliberately keeps
  // rendering `children` (this component) through that error instead of redirecting — this effect
  // is what actually locks in response to it, and captures the server-reported
  // `idleTimeoutMinutes` for the dialog copy below rather than a hardcoded fallback.
  useEffect(() => {
    if (sessionError instanceof StaffSessionIdleTimeoutError) {
      setIdleTimeoutMinutesFromError(sessionError.idleTimeoutMinutes);
      lockNow();
    }
  }, [sessionError, lockNow]);

  // Local countdown, scheduled from the server-reported times — recomputed whenever a fresh
  // status arrives (initial load, an activity heartbeat, or "Stay signed in").
  useEffect(() => {
    if (!status) return;
    const clockOffsetMs = new Date(status.serverTime).getTime() - Date.now();
    const idleExpiresAtMs = new Date(status.idleExpiresOn).getTime();
    const correctedNow = () => Date.now() + clockOffsetMs;

    const msUntilWarning = idleExpiresAtMs - WARNING_LEAD_MS - correctedNow();
    const msUntilLock = idleExpiresAtMs - correctedNow();

    setWarning(msUntilWarning <= 0 && msUntilLock > 0);

    const warningTimer =
      msUntilWarning > 0 ? setTimeout(() => setWarning(true), msUntilWarning) : undefined;
    const lockTimer = msUntilLock > 0 ? setTimeout(lockNow, msUntilLock) : setTimeout(lockNow, 0);
    warningTimerRef.current = warningTimer;
    lockTimerRef.current = lockTimer;

    return () => {
      clearTimeout(warningTimerRef.current);
      clearTimeout(lockTimerRef.current);
    };
  }, [status, lockNow]);

  // Activity heartbeat — real interaction only, never while locked, never on a timer.
  useEffect(() => {
    if (locked) return;
    function onActivity() {
      const now = Date.now();
      if (now - lastActivityPostRef.current < ACTIVITY_THROTTLE_MS) return;
      lastActivityPostRef.current = now;
      void activityMutationRef.current
        .mutateAsync()
        .then((result) => {
          if (result) setStatus(result);
        })
        .catch(() => {
          // CODE_REVIEW LOW (first pass) — `recordStaffActivity()` already resolves to `null`
          // (never rejects) for the ordinary 401 cases; this only guards against something
          // genuinely unexpected (a network failure, a 5xx) so a heartbeat never produces an
          // unhandled-promise-rejection console warning. Nothing to react to here either way —
          // a real session-idle-timeout still reaches this component via `sessionError` above.
        });
    }
    ACTIVITY_EVENTS.forEach((eventName) => document.addEventListener(eventName, onActivity));
    return () =>
      ACTIVITY_EVENTS.forEach((eventName) => document.removeEventListener(eventName, onActivity));
  }, [locked]);

  // beforeunload warning while locked — nothing about the draft is copied, serialised or stored.
  useEffect(() => {
    if (!locked) return;
    function handler(event: BeforeUnloadEvent) {
      event.preventDefault();
      event.returnValue = '';
    }
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [locked]);

  function handleStaySignedIn() {
    lastActivityPostRef.current = Date.now();
    void activityMutation
      .mutateAsync()
      .then((result) => {
        if (result) setStatus(result);
        setWarning(false);
      })
      .catch(() => {
        // See the activity-listener effect's own comment — a real 401 is handled elsewhere.
      });
  }

  async function handleReSignInSuccess(result: CustomerSignInResult | StaffSignInResult) {
    const staffAccountId = 'staffAccountId' in result ? result.staffAccountId : undefined;
    if (staffAccountId && staffAccountId === lockedStaffIdRef.current) {
      await queryClient.refetchQueries({ queryKey: STAFF_SESSION_QUERY_KEY });
      const fresh = queryClient.getQueryData<StaffSessionStatus | null>(STAFF_SESSION_QUERY_KEY);
      if (fresh) setStatus(fresh);
      lockedRef.current = false;
      unlock();
      setPersonChanged(false);
      setIdleTimeoutMinutesFromError(undefined); // next lock (if any) starts fresh, not stale
    } else {
      setPersonChanged(true);
    }
  }

  function handleDiscardAndReload() {
    forgetPreservedBranch(queryClient);
    window.location.reload();
  }

  return (
    <>
      {children}

      {warning && !locked && (
        <div
          role="status"
          aria-live="polite"
          className="fixed inset-x-0 bottom-0 z-40 flex flex-wrap items-center justify-between gap-4 border-t border-warning-ink/30 bg-warning-bg px-6 py-3 text-warning-ink"
        >
          <p className="text-sm font-semibold">
            You&rsquo;ll be signed out for inactivity in 2 minutes.
          </p>
          <Button variant="secondary" size="sm" onClick={handleStaySignedIn}>
            Stay signed in
          </Button>
        </div>
      )}

      <Dialog open={locked} onOpenChange={() => {}}>
        <DialogContent
          hideClose
          // SEC gate LOW (first pass) — the locked page stays mounted and readable behind the
          // default translucent overlay (ADR-FE-010's own point: never unmount it), which on an
          // unattended reception terminal means a customer's name, phone numbers, addresses and
          // medical data stay legible on screen after the session has timed out. Opaque here
          // closes that; the DOM/form state underneath is completely untouched.
          overlayClassName="bg-surface-page"
          onEscapeKeyDown={(event) => event.preventDefault()}
          onPointerDownOutside={(event) => event.preventDefault()}
          onInteractOutside={(event) => event.preventDefault()}
        >
          {/* ARCH LOW (first pass) — `idleTimeoutMinutesFromError` (the value the 401 that
              actually triggered THIS lock just reported) wins over `status`'s own value (from the
              last successful `GET`, which can be stale by definition once a lock has happened)
              whenever both exist; the bare `?? 30` is a last resort only reachable if the lock is
              ever triggered with neither, and it matches the real backend default
              (`AccountOptions.StaffIdleTimeoutMinutes`), not an arbitrary guess. */}
          <IdleLockDialogContent
            personChanged={personChanged}
            onDiscardAndReload={handleDiscardAndReload}
            idleTimeoutMinutes={idleTimeoutMinutesFromError ?? status?.idleTimeoutMinutes ?? 30}
            reSignInMutation={reSignInMutation}
            onReSignInSuccess={handleReSignInSuccess}
          />
        </DialogContent>
      </Dialog>
    </>
  );
}
