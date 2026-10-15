'use client';

import { useCallback, useEffect, useState } from 'react';

/**
 * `lib/hooks/useHashTab.ts` — HO-SRG-002 §8.1. The surgery page's tabs are driven by the URL hash
 * (`#pre-op`, `#anaesthesia`, …) so a tab is linkable and survives a reload. With no (or an unknown)
 * hash the default tab applies. `select` replaces the history entry's hash without scrolling.
 */
export function useHashTab(
  valid: readonly string[],
  fallback: string,
): [string, (id: string) => void] {
  const read = useCallback(() => {
    if (typeof window === 'undefined') return fallback;
    const hash = window.location.hash.replace(/^#/, '');
    return valid.includes(hash) ? hash : fallback;
  }, [valid, fallback]);
  const [tab, setTab] = useState(fallback);

  useEffect(() => {
    setTab(read());
    const onChange = () => setTab(read());
    window.addEventListener('hashchange', onChange);
    return () => window.removeEventListener('hashchange', onChange);
  }, [read]);

  const select = useCallback((id: string) => {
    setTab(id);
    window.history.replaceState(null, '', `#${id}`);
  }, []);

  return [tab, select];
}
