import { useSyncExternalStore } from 'react';

/**
 * A `next/navigation` stand-in for the checkout tests: the URL lives in `window.location` (jsdom's
 * History API), because `useCheckoutUrlState().update` builds the next URL from
 * `window.location.search`. `router.replace` rewrites it and notifies the hooks so the screen
 * re-renders, as in the browser. Used as `vi.mock('next/navigation', () => import('./nav'))`.
 */
const listeners = new Set<() => void>();

export function setCheckoutUrl(url: string): void {
  window.history.replaceState(null, '', url);
  listeners.forEach((listener) => listener());
}

export function currentCheckoutUrl(): string {
  return `${window.location.pathname}${window.location.search}`;
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

const snapshotSearch = () => window.location.search;
const snapshotPath = () => window.location.pathname;

export function useSearchParams() {
  const search = useSyncExternalStore(subscribe, snapshotSearch, snapshotSearch);
  return new URLSearchParams(search);
}

export function usePathname() {
  return useSyncExternalStore(subscribe, snapshotPath, snapshotPath);
}

export const pushed: string[] = [];

const router = {
  push: (url: string) => {
    pushed.push(url);
  },
  replace: (url: string) => setCheckoutUrl(url),
  back: () => undefined,
  forward: () => undefined,
  refresh: () => undefined,
  prefetch: () => undefined,
};

export function useRouter() {
  return router;
}
