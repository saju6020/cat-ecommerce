'use client';

import { useEffect, useState } from 'react';

/**
 * `lib/hooks/useMediaQuery.ts` — HO-BRD-002 §13 (new hook). `true` while the media query matches;
 * `false` on the server and first paint, so a desktop layout is what hydrates and a narrow one swaps
 * in after. The occupancy board uses it to become a day list below 768 px (UI-1, ADR-FE-102).
 */
export function useMediaQuery(query: string): boolean {
  const [matches, setMatches] = useState(false);
  useEffect(() => {
    const media = window.matchMedia(query);
    const update = () => setMatches(media.matches);
    update();
    media.addEventListener('change', update);
    return () => media.removeEventListener('change', update);
  }, [query]);
  return matches;
}
