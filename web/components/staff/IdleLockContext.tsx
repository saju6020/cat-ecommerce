'use client';

import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { focusManager, onlineManager } from '@tanstack/react-query';

/**
 * `components/staff/IdleLockContext.tsx` — ADR-FE-010. React context is allowed here, and only
 * here, to pass idle-lock state through the `(staff)` layout — it holds no server data (§3.1/§9's
 * "no global store" is about state, not a function reference or a UI-only boolean).
 *
 * ARCH gate HIGH (second pass) — `locked` used to live only inside `IdleLock`'s own component
 * state, with a context (`IdleLockController`, `lock()` only) exposed downward from there. That
 * placement cannot fix the real HIGH this pass found: the real API ends the staff session on the
 * FIRST idle-timeout detection (`SessionAuthenticationMiddleware`'s `EndForIdleTimeout` plus
 * `cookieWriter.ExpireStaffSession`), so every later staff-session read naturally resolves to
 * `session_invalid`/`null` — exactly what `StaffSessionGuard` (`IdleLock`'s own PARENT,
 * `(staff)/layout.tsx` composes `StaffSessionGuard > IdleLock > children`) treats as "signed out"
 * and redirects on. A context `IdleLock` provides can never reach its own parent. `StaffLockState`
 * is provided one level higher instead (`StaffLockProvider`, wrapping `StaffSessionGuard` itself in
 * the layout), so both the guard (reads `locked`, to stop redirecting while the lock's own
 * re-authentication is what actually resolves things) and `IdleLock` (reads/writes `locked`, to
 * render the dialog and to lock/unlock) share the one value.
 *
 * ARCHITECTURE gate third pass, HIGH — a page's OWN query (e.g. `useStaffAccount(id)` on the
 * staff-account detail screen, or `useCat(id)` under `CatProfileDataShell`) can independently get
 * its own `401` on the SAME focus event that triggers the lock, keeping its stale `data` but
 * setting `isError: true` — a page branching on `isError` alone then swaps its whole form out,
 * losing the very draft `IdleLock` exists to protect, through a code path the session-error
 * handler was never positioned to see (a React Query observer's own retry bookkeeping, not a
 * `client.ts`-dispatched call).
 *
 * ARCHITECTURE gate fourth pass, HIGH — the third pass's own fix (toggling
 * `queryClient.setDefaultOptions({queries: {refetchOnWindowFocus: false, ...}})`) does not reach
 * an ALREADY-MOUNTED query observer: `useBaseQuery` computes `client.defaultQueryOptions(options)`
 * once and pushes it into the observer via `observer.setOptions(...)` inside a `useEffect` that
 * only re-runs when ITS OWN inputs change — not merely because the client's defaults changed
 * somewhere else. A page/layout whose own element identity is stable across a lock (which is the
 * whole point of `IdleLock` keeping `children` mounted) never re-renders just because `lock()` ran,
 * so its query keeps whatever `refetchOnWindowFocus` value it already had, and a focus event still
 * refetches it. Fixed instead by pausing TanStack's own global `focusManager`/`onlineManager`
 * singletons directly — every observer (mounted or not, past or future, HO-ACC-002 or any other
 * area) reads them (or is notified by them) at the moment a real `visibilitychange`/`online`/
 * `offline` event fires, not a value cached at render time, so overriding what those two
 * singletons themselves report reaches every observer with no per-query cooperation needed.
 *
 * Both managers are paused the SAME way — `setEventListener(...)` swapped to a no-op setup while
 * locked, detaching the real `window` listener entirely so no real event can reach either manager
 * at all — after ruling out, empirically, two more direct-looking alternatives that each turned out
 * to have a real, broader side effect than intended:
 *   - `focusManager.setFocused(false)`/`onlineManager.setOnline(false)` (manually overriding what
 *     each manager REPORTS, rather than detaching its listener) does stop a focus/reconnect-
 *     triggered refetch, but `focusManager.isFocused()` and `onlineManager.isOnline()` are ALSO
 *     consulted by TanStack's own retryer (`retryer.js`'s `canContinue()`) to decide whether an
 *     ALREADY-IN-PROGRESS retry may proceed after its delay elapses. Forcing either to a fixed
 *     value pauses that retry until the override lifts — proven live, during this round's own
 *     development, by a query with `retry: noRetryOn403` (`useStaffAccount`) hanging indefinitely
 *     once its first attempt failed while locked, never settling into the `isError` state
 *     `CatProfileDataShell`'s/the staff-account page's own `isError && !data` guard depends on.
 *   - `onlineManager.setOnline(false)` alone (without a listener swap) was tried first and also
 *     rejected: `onlineManager`'s own default event listener calls `setOnline(true)`/
 *     `setOnline(false)` DIRECTLY on a real `online`/`offline` event (unlike `focusManager`'s
 *     neutral `onFocus()` re-evaluation), so a real reconnect during the lock silently overrode the
 *     forced `false` — and, separately, forcing `isOnline()` to `false` at all paused every OTHER
 *     `networkMode: 'online'` (the default) operation while locked, including the re-sign-in
 *     `useMutation` call `IdleLock` itself uses to unlock, which would then never reach the network
 *     at all, permanently stuck behind the very lock it exists to lift.
 *
 * Neither manager's REPORTED value (`isFocused()`/`isOnline()`) is therefore ever touched by this
 * component while LOCKING — only which listener is installed. `focusManager`'s own default setup and
 * `onlineManager`'s own default setup (both reproduced below, since neither library class exposes a
 * way to read back whatever setup is currently installed, only to replace it — verified against
 * `@tanstack/query-core` 5.103.2's own `focusManager.js`/`onlineManager.js`; a unit test exercises
 * each directly, so a future TanStack upgrade changing either default fails loudly, not silently)
 * are reinstalled on unlock.
 *
 * ARCHITECTURE gate fifth pass, MEDIUM — freezing `onlineManager`'s LISTENER (above) also freezes
 * its REPORTED `isOnline()` value for the whole lock's duration, at whatever it happened to be the
 * instant `lock()` ran — `onlineManager` never reads `navigator.onLine` itself, only ever a real
 * `online`/`offline` event, which this component detaches. If the terminal is already offline at
 * that instant (Wi-Fi dropped, lid closed, the `offline` event already fired while the REAL listener
 * was still attached), `isOnline()` freezes at `false` and stays there even once the terminal
 * genuinely reconnects — the frozen listener can never learn about it. `useSignInStaff()`'s default
 * `networkMode: 'online'` then refuses to even START the re-sign-in mutation `IdleLock` uses to
 * unlock (`canFetch` false, the mutation pauses instead of running), and only `unlock()` would ever
 * reattach the listener — which runs only once that same mutation succeeds. A genuine deadlock: the
 * one operation capable of ending the lock can never run BECAUSE the lock is active. Fixed by giving
 * `useSignInStaff()` (`lib/hooks/useStaffAuthMutations.ts`) `networkMode: 'always'` instead — it runs
 * regardless of what `onlineManager` reports, exactly like it would with no lock in play at all; a
 * genuinely offline attempt still fails the normal way (the browser's own `fetch` rejects), just
 * never SILENTLY PAUSES first. `focusManager`/`onlineManager` stay paused as a pair for every OTHER
 * query, preserving this fix's original guarantee (no focus- or reconnect-triggered refetch at all
 * while locked) — only the one mutation that must always be reachable is exempted, not the pause
 * mechanism itself. `unlock()` additionally resyncs `onlineManager.setOnline(navigator.onLine)`
 * (below) once the real listener is reattached, so a genuine online/offline transition that happened
 * anywhere during the lock (not only the exact deadlock case above) is reflected immediately, rather
 * than waiting for the next real `online`/`offline` event to correct it.
 *
 * The fourth pass's own `cancelQueries(undefined, { revert: false })` call (added by the third
 * pass) is REMOVED, not kept alongside this fix: with `revert: false`, a cancelled query is
 * dispatched as `{type: 'error', error: CancelledError}` (`query.js`), turning every query that
 * happened to be in flight at the exact moment of locking into `isError: true` — including a
 * page's very FIRST load, which has no `data` yet to fall back on, so this call alone could make a
 * cat/staff-account form's loading state flash straight to an error screen. The comment that
 * shipped alongside it also had `revert` backwards: `revert: true` (TanStack's own default, which
 * this fix never needs to override) is what PRESERVES a query's prior state across a cancelled
 * fetch — including its `data` — precisely the safe, existing default. Pausing
 * `focusManager`/`onlineManager` already stops a NEW focus/reconnect-triggered fetch from ever
 * starting while locked; nothing further needs cancelling.
 */
export interface StaffLockState {
  locked: boolean;
  /** Locks the terminal immediately — `IdleLock` calls this from its own `client.ts`
   *  session-error handler when any staff-audience call reports `session_idle_timeout`, and from
   *  its own `useStaffSession()` query error; available to any staff descendant that might need it
   *  later too. Idempotent — a second call while already locked (a realistic race: the local
   *  countdown timer locks while the tab is hidden, then the first request after return also
   *  detects `session_idle_timeout` and calls this again) is a no-op beyond the redundant
   *  `setLocked(true)`. */
  lock: () => void;
  /** Lifts the lock — called only after a successful re-authentication as the SAME staff member
   *  (`IdleLock`'s own `handleReSignInSuccess`); a different person discards and reloads instead of
   *  unlocking. Restores `focusManager`/`onlineManager` to their pre-lock behaviour exactly. */
  unlock: () => void;
}

const StaffLockContext = createContext<StaffLockState | null>(null);

/** A no-op event-listener setup, shared by both managers while locked — no `window` listener is
 *  ever attached, so no real event can reach either manager's own notify path at all. Returning a
 *  (no-op) cleanup matches `setup`'s own contract; `setEventListener` calls it unconditionally when
 *  swapping to the next setup. */
function noopEventSetup() {
  return () => {};
}

/** `focusManager`'s own default setup (`@tanstack/query-core` 5.103.2, `focusManager.js`),
 *  reproduced here so it can be reinstalled on unlock. */
function defaultFocusEventSetup(onFocus: () => void) {
  if (typeof window !== 'undefined' && window.addEventListener) {
    const listener = () => onFocus();
    window.addEventListener('visibilitychange', listener, false);
    return () => {
      window.removeEventListener('visibilitychange', listener);
    };
  }
  return undefined;
}

/** `onlineManager`'s own default setup (`@tanstack/query-core` 5.103.2, `onlineManager.js`),
 *  reproduced here so it can be reinstalled on unlock. */
function defaultOnlineEventSetup(setOnline: (online: boolean) => void) {
  if (typeof window !== 'undefined' && window.addEventListener) {
    const onlineListener = () => setOnline(true);
    const offlineListener = () => setOnline(false);
    window.addEventListener('online', onlineListener, false);
    window.addEventListener('offline', offlineListener, false);
    return () => {
      window.removeEventListener('online', onlineListener);
      window.removeEventListener('offline', offlineListener);
    };
  }
  return undefined;
}

/** Reinstalls both managers' real default listeners, then resyncs `onlineManager`'s REPORTED value
 *  with the browser's own current status (ARCHITECTURE gate fifth pass, MEDIUM — see the doc comment
 *  above `StaffLockProvider`) — while paused, no real `online`/`offline` event could ever have
 *  reached `onlineManager`, so its frozen value may be stale by the time this runs, in either
 *  direction (stuck `false` after a genuine reconnect, the deadlock case; or stuck `true` after a
 *  genuine disconnect, harmless but still wrong). `navigator.onLine` is the browser's own live
 *  answer, always available with no listener needed. Guarded for SSR/non-browser test environments
 *  that may lack `navigator` entirely, the same way `defaultFocusEventSetup`/
 *  `defaultOnlineEventSetup` above guard for a missing `window`. */
function restoreManagers() {
  focusManager.setEventListener(defaultFocusEventSetup);
  onlineManager.setEventListener(defaultOnlineEventSetup);
  if (typeof navigator !== 'undefined') {
    onlineManager.setOnline(navigator.onLine);
  }
}

export function StaffLockProvider({ children }: { children: React.ReactNode }) {
  const [locked, setLocked] = useState(false);
  // `true` exactly while this provider has swapped focusManager/onlineManager to the no-op
  // listener — guards both lock()'s idempotency (a second call while already paused is a no-op
  // beyond the redundant `setLocked(true)`) and unlock()'s/the unmount cleanup's own idempotency
  // (never restore something that was never paused).
  const pausedRef = useRef(false);

  const lock = useCallback(() => {
    setLocked(true);
    if (pausedRef.current) return;
    pausedRef.current = true;
    focusManager.setEventListener(noopEventSetup);
    onlineManager.setEventListener(noopEventSetup);
  }, []);

  const unlock = useCallback(() => {
    setLocked(false);
    if (!pausedRef.current) return;
    pausedRef.current = false;
    restoreManagers();
  }, []);

  // ARCHITECTURE gate fourth pass, MEDIUM — if this provider unmounts while still locked (a client
  // navigation away from every `(staff)/**` route, the only place `locked` can ever be `true`,
  // ARCHITECTURE gate second pass), the override must not outlive it — otherwise every OTHER area's
  // queries would silently stop refetching on focus/reconnect for the rest of the session.
  useEffect(() => {
    return () => {
      if (!pausedRef.current) return;
      pausedRef.current = false;
      restoreManagers();
    };
  }, []);

  return (
    <StaffLockContext.Provider value={{ locked, lock, unlock }}>
      {children}
    </StaffLockContext.Provider>
  );
}

export function useStaffLockState(): StaffLockState {
  const context = useContext(StaffLockContext);
  if (!context) {
    throw new Error('useStaffLockState must be used within StaffLockProvider');
  }
  return context;
}
