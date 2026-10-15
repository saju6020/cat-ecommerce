'use client';

import { useEffect, useRef, useState } from 'react';

/** Below this CONTENT width (px) a table becomes stacked cards. The staff shell's sidebar takes
 *  240 px from `sm` up, so the viewport (768 px, UI-1) is a poor stand-in for the room a table
 *  actually has — the container is measured instead. */
export const NARROW_CONTENT_PX = 640;

/**
 * `lib/hooks/useIsNarrow.ts` — HO-IMP-002 UI-1. Returns a ref for the element to measure and
 * whether it is narrower than `threshold`. Starts `false` (server render and first client render
 * agree) and settles after mount; where `ResizeObserver` does not exist (jsdom) it stays `false`,
 * i.e. the full table.
 */
export function useNarrowContainer<T extends HTMLElement>(threshold = NARROW_CONTENT_PX) {
  const ref = useRef<T>(null);
  const [narrow, setNarrow] = useState(false);
  useEffect(() => {
    const element = ref.current;
    if (!element || typeof ResizeObserver === 'undefined') return;
    const measure = () => setNarrow(element.getBoundingClientRect().width < threshold);
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    return () => observer.disconnect();
  }, [threshold]);
  return [ref, narrow] as const;
}
