import { useSyncExternalStore } from 'react';

/**
 * A reactive stand-in for `next/navigation` for the Appointments component tests: the URL's search
 * params live in a tiny external store, so `useFilterUrlUpdate`'s `router.push(...)` really changes
 * what `useSearchParams()` returns and the flow re-renders — just as in the browser. Used as
 * `vi.mock('next/navigation', () => import('./nav-mock'))`.
 */
let pathname = '/book';
let params = new URLSearchParams();
const listeners = new Set<() => void>();

export function setUrl(url: string): void {
  const [path, query = ''] = url.split('?');
  pathname = path || '/';
  params = new URLSearchParams(query);
  listeners.forEach((listener) => listener());
}

export function currentUrl(): string {
  const query = params.toString();
  return query ? `${pathname}?${query}` : pathname;
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function useSearchParams() {
  return useSyncExternalStore(
    subscribe,
    () => params,
    () => params,
  );
}

export function usePathname() {
  return useSyncExternalStore(
    subscribe,
    () => pathname,
    () => pathname,
  );
}

export const routerPush = (url: string) => setUrl(url);
export const routerReplace = (url: string) => setUrl(url);

const router = {
  push: (url: string) => routerPush(url),
  replace: (url: string) => routerReplace(url),
  back: () => undefined,
  forward: () => undefined,
  refresh: () => undefined,
  prefetch: () => undefined,
};

export function useRouter() {
  return router;
}
