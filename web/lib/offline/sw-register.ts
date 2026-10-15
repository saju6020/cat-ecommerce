/**
 * `lib/offline/sw-register.ts` — HO-XCT-002 §12.1, ADR-FE-139/140. When the offline service worker
 * may exist, and how it is removed.
 *
 * It is registered ONLY when all three hold: a production build, `NEXT_PUBLIC_USE_MOCKS` is not
 * `true`, and the device opt-in is on. In development and in mock mode (where MSW owns a service
 * worker at `/`, and two workers with overlapping scopes would stop MSW intercepting staff routes)
 * any previously registered `/staff/sw.js` is unregistered and its caches purged, so a developer
 * who ran a production build once is not left with a stale worker (§20.9).
 *
 * Only OUR registration is ever touched: scope `/staff/`, script `/staff/sw.js`. MSW's worker at
 * `/` is never unregistered here.
 */

export const SW_URL = '/staff/sw.js';
export const SW_SCOPE = '/staff/';
const CACHE_PREFIX = 'purrcare-';

export interface ServiceWorkerPolicyInput {
  nodeEnv: string | undefined;
  useMocks: string | undefined;
  deviceEnabled: boolean;
}

export function serviceWorkerAllowed(input: ServiceWorkerPolicyInput): boolean {
  return input.nodeEnv === 'production' && input.useMocks !== 'true' && input.deviceEnabled;
}

function workerSupported(): boolean {
  return typeof navigator !== 'undefined' && 'serviceWorker' in navigator;
}

async function ourRegistrations(): Promise<ServiceWorkerRegistration[]> {
  if (!workerSupported()) return [];
  const all = await navigator.serviceWorker.getRegistrations();
  return all.filter((registration) => {
    const script = (
      registration.active ??
      registration.waiting ??
      registration.installing
    )?.scriptURL;
    return (
      new URL(registration.scope).pathname === SW_SCOPE &&
      (script === undefined || new URL(script).pathname === SW_URL)
    );
  });
}

/** Deletes every cache this feature owns. Done from the page: `caches` is available here too. */
export async function purgeOfflineServiceWorker(): Promise<void> {
  try {
    if (typeof caches !== 'undefined') {
      const names = await caches.keys();
      await Promise.all(
        names.filter((name) => name.startsWith(CACHE_PREFIX)).map((name) => caches.delete(name)),
      );
    }
    for (const registration of await ourRegistrations()) {
      registration.active?.postMessage({ type: 'purge' });
    }
  } catch {
    // Nothing to purge, or the browser refused; the snapshot wipe is what protects data.
  }
}

export async function unregisterOfflineServiceWorker(): Promise<void> {
  try {
    await purgeOfflineServiceWorker();
    await Promise.all((await ourRegistrations()).map((registration) => registration.unregister()));
  } catch {
    // Same as above.
  }
}

/** Registers (or cleans up) according to the policy. Returns what it did, for tests. */
export async function reconcileServiceWorker(
  input: ServiceWorkerPolicyInput,
): Promise<'registered' | 'removed' | 'unsupported'> {
  if (!workerSupported()) return 'unsupported';
  if (serviceWorkerAllowed(input)) {
    try {
      await navigator.serviceWorker.register(SW_URL, { scope: SW_SCOPE });
      return 'registered';
    } catch {
      return 'removed';
    }
  }
  await unregisterOfflineServiceWorker();
  return 'removed';
}

/** After each successful snapshot write: refetch the offline shell so it matches the build. */
export async function warmOfflineShell(): Promise<void> {
  try {
    for (const registration of await ourRegistrations()) {
      registration.active?.postMessage({ type: 'warm-shell' });
    }
  } catch {
    // The worker will warm the shell on its next install.
  }
}
