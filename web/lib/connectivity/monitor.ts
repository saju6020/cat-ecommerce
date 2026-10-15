import { ApiError, setConnectivityReporter } from '@/lib/api/client';
import { pingServer } from '@/lib/api/offline';

/**
 * `lib/connectivity/monitor.ts` — HO-XCT-002 §11.1, ADR-FE-142. Connectivity is the SERVER's
 * answer, not the browser's flag: `GET /api/system/ping` every 15 s while a staff route is
 * mounted, plus every network failure and success `client.ts` reports.
 *
 *  - two consecutive failures (or one failure while `navigator.onLine` is false) -> `offline`;
 *  - one success -> `online`;
 *  - a `5xx` -> `degraded`: the server is reachable, so the offline board is NOT offered.
 *
 * A tiny module-level store read through `useSyncExternalStore` (`lib/hooks/useConnectivity.ts`).
 * It is a device fact, not server data and not an app store: nothing else lives here.
 */

export type ConnectivityStatus = 'online' | 'degraded' | 'offline';

export interface ConnectivityState {
  status: ConnectivityStatus;
  /** Epoch ms of the last answer from the server; null until the first. */
  lastOnlineAt: number | null;
  /** Epoch ms the monitor last went from `offline` to reachable; null if it never did. */
  recoveredAt: number | null;
}

export const PING_INTERVAL_MS = 15_000;
export const PING_BACKOFF_INTERVAL_MS = 60_000;
export const PING_BACKOFF_AFTER_MS = 5 * 60_000;
export const PING_TIMEOUT_MS = 5_000;
export const PING_FAST_RECHECK_MS = 3_000;
export const PING_FAST_RECHECK_WINDOW_MS = 60_000;
const FAILURES_FOR_OFFLINE = 2;

let state: ConnectivityState = { status: 'online', lastOnlineAt: null, recoveredAt: null };
let consecutiveFailures = 0;
let lastReachableAt: number | null = null;
let offlineSince: number | null = null;

const listeners = new Set<() => void>();
const recoveryListeners = new Set<() => void>();

function emit(next: ConnectivityState): void {
  state = next;
  for (const listener of [...listeners]) listener();
}

export function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function getSnapshot(): ConnectivityState {
  return state;
}

/** Called once when the monitor first sees the server again after `offline` (ADR-FE-145). */
export function onRecovered(listener: () => void): () => void {
  recoveryListeners.add(listener);
  return () => {
    recoveryListeners.delete(listener);
  };
}

export function isOffline(): boolean {
  return state.status === 'offline';
}

export type ConnectivityOutcome = 'reachable' | 'unreachable' | 'server-error';

export function recordOutcome(outcome: ConnectivityOutcome, now: number = Date.now()): void {
  if (outcome === 'unreachable') {
    consecutiveFailures += 1;
    const browserSaysOffline = typeof navigator !== 'undefined' && navigator.onLine === false;
    if (consecutiveFailures >= FAILURES_FOR_OFFLINE || browserSaysOffline) {
      if (state.status !== 'offline') {
        offlineSince = now;
        emit({ ...state, status: 'offline', lastOnlineAt: lastReachableAt });
      }
    }
    // A failure brings the next check forward (confirm the outage, or see the blip is over).
    if (running && !inFlight) scheduleNext();
    return;
  }

  consecutiveFailures = 0;
  lastReachableAt = now;
  const wasOffline = state.status === 'offline';
  const status: ConnectivityStatus = outcome === 'server-error' ? 'degraded' : 'online';
  if (wasOffline) offlineSince = null;
  // Re-render subscribers only when something they show changed, not on every response.
  if (status !== state.status || wasOffline) {
    emit({
      status,
      lastOnlineAt: now,
      recoveredAt: wasOffline ? now : state.recoveredAt,
    });
  }
  if (wasOffline) for (const listener of [...recoveryListeners]) listener();
}

let inFlight: Promise<ConnectivityOutcome> | null = null;

/**
 * One ping. Aborts after `PING_TIMEOUT_MS`; any non-answer counts as a failure. A check already in
 * flight is shared, never doubled. Resolves what it found, so a caller (the offline board) can act
 * on the answer.
 */
export function forceCheck(): Promise<ConnectivityOutcome> {
  if (inFlight) return inFlight;
  pingsSent += 1;
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), PING_TIMEOUT_MS);
  inFlight = (async (): Promise<ConnectivityOutcome> => {
    let outcome: ConnectivityOutcome;
    try {
      await pingServer(controller.signal);
      outcome = 'reachable';
    } catch (error) {
      if (error instanceof ApiError && error.status >= 500) outcome = 'server-error';
      // A 4xx (for example a rate-limit answer) still proves the server is there.
      else if (error instanceof ApiError && error.status > 0) outcome = 'reachable';
      else outcome = 'unreachable';
    } finally {
      clearTimeout(timeout);
    }
    inFlight = null;
    recordOutcome(outcome);
    return outcome;
  })();
  return inFlight;
}

/** Number of pings sent since the last reset; lets a test prove the loop never multiplies. */
let pingsSent = 0;
export function pingCountForTests(): number {
  return pingsSent;
}

/**
 * 15 s while all is well (OQ-3). After a failure the next check comes quickly (3 s; owner-confirmed
 * 2026-10-14, ADR-FE-142), both to confirm an outage and to notice a blip is over: while the monitor
 * says `offline`, TanStack pauses queries, so a staff page would otherwise sit on its loading screen
 * until the next 15 s tick after a transient pair of failures. Backs off to 15 s after a minute
 * offline and 60 s after five.
 */
function nextDelay(now: number): number {
  if (offlineSince !== null) {
    const elapsed = now - offlineSince;
    if (elapsed >= PING_BACKOFF_AFTER_MS) return PING_BACKOFF_INTERVAL_MS;
    if (elapsed < PING_FAST_RECHECK_WINDOW_MS) return PING_FAST_RECHECK_MS;
    return PING_INTERVAL_MS;
  }
  return consecutiveFailures > 0 ? PING_FAST_RECHECK_MS : PING_INTERVAL_MS;
}

let mounts = 0;
/** The ONE pending-check handle. Everything that schedules goes through `scheduleNext`. */
let timer: ReturnType<typeof setTimeout> | null = null;
let running = false;
let teardown: (() => void) | null = null;

function clearTimer(): void {
  if (timer) clearTimeout(timer);
  timer = null;
}

/** Replaces whatever check is pending with the next one. Never schedules once the monitor stopped. */
function scheduleNext(): void {
  clearTimer();
  if (!running) return;
  timer = setTimeout(() => {
    timer = null;
    void checkAndContinue();
  }, nextDelay(Date.now()));
}

async function checkAndContinue(): Promise<void> {
  if (!running) return;
  await forceCheck();
  scheduleNext(); // no-op if the monitor stopped while the ping was in flight
}

/**
 * Starts the ping loop for one mounted staff or offline route. Reference-counted: the loop runs
 * while at least one caller has not stopped it. Returns the stop function.
 */
export function startMonitor(): () => void {
  mounts += 1;
  if (mounts === 1) begin();
  let stopped = false;
  return () => {
    if (stopped) return;
    stopped = true;
    mounts -= 1;
    if (mounts === 0) end();
  };
}

function begin(): void {
  running = true;
  setConnectivityReporter({ isOffline, report: (outcome) => recordOutcome(outcome) });

  const checkNow = () => {
    if (!running) return;
    clearTimer();
    void checkAndContinue();
  };
  const onVisible = () => {
    if (document.visibilityState === 'visible') checkNow();
  };
  document.addEventListener('visibilitychange', onVisible);
  window.addEventListener('online', checkNow);
  // The device itself reports no network: one failure is enough (ADR-FE-142).
  window.addEventListener('offline', checkNow);

  checkNow();
  teardown = () => {
    running = false;
    document.removeEventListener('visibilitychange', onVisible);
    window.removeEventListener('online', checkNow);
    window.removeEventListener('offline', checkNow);
    clearTimer();
    setConnectivityReporter(null);
  };
}

function end(): void {
  teardown?.();
  teardown = null;
}

/** Test seam — back to a fresh `online` store with no listeners. */
export function resetConnectivityForTests(): void {
  teardown?.();
  teardown = null;
  mounts = 0;
  running = false;
  clearTimer();
  inFlight = null;
  pingsSent = 0;
  state = { status: 'online', lastOnlineAt: null, recoveredAt: null };
  consecutiveFailures = 0;
  lastReachableAt = null;
  offlineSince = null;
  listeners.clear();
  recoveryListeners.clear();
}
