'use client';

import { useSyncExternalStore } from 'react';

const subscribe = () => () => {};

/**
 * `false` on the server and during hydration, `true` from the next render on. A screen whose data can already be in
 * the client cache when its page hydrates (the shop header's CartBadge loads `['cart']` first) renders its loading
 * state until then, so its first client render matches the server HTML (2026-10-16, a hydration mismatch on /cart).
 */
export function useHydrated(): boolean {
  return useSyncExternalStore(
    subscribe,
    () => true,
    () => false,
  );
}
