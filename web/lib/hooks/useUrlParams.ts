'use client';

import { useCallback } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';

/**
 * `lib/hooks/useUrlParams.ts` — HO-ORD-002 §8.3. The URL is where a list's filters and page live
 * (UI-4, NFR-USA-5): reload-safe, back-button-safe and linkable. `set` replaces the URL (no new history
 * entry per keystroke), resetting `page` unless the patch is the page itself.
 */
export function useUrlParams(): {
  get: (name: string) => string;
  set: (patch: Record<string, string | undefined>) => void;
} {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const query = searchParams.toString();

  const get = useCallback((name: string) => new URLSearchParams(query).get(name) ?? '', [query]);

  const set = useCallback(
    (patch: Record<string, string | undefined>) => {
      const next = new URLSearchParams(window.location.search);
      for (const [key, value] of Object.entries(patch)) {
        if (value === undefined || value === '') next.delete(key);
        else next.set(key, value);
      }
      if (!('page' in patch)) next.delete('page');
      const text = next.toString();
      router.replace(text ? `${pathname}?${text}` : pathname, { scroll: false });
    },
    [pathname, router],
  );

  return { get, set };
}
