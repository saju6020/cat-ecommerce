'use client';

import { useEffect, useState } from 'react';
import { usePathname, useSearchParams } from 'next/navigation';

/** Give up after this long, so a navigation that never completes (offline, an error) cannot leave the bar stuck. */
const SAFETY_TIMEOUT_MS = 15_000;
const START_EVENT = 'purrcare:navigation-start';

/**
 * Starts the top progress bar for a navigation the click listener cannot see — a `router.push`
 * from a button handler. Links and GET forms are picked up automatically.
 */
export function startNavigationProgress() {
  window.dispatchEvent(new Event(START_EVENT));
}

function isSamePageTarget(url: URL) {
  return url.pathname === window.location.pathname && url.search === window.location.search;
}

/**
 * `components/common/NavigationProgress.tsx` — Issues.md #1/#6. A thin bar across the top of every
 * area that appears the moment an internal link is followed and goes when the new route renders,
 * so a 2-3 s page change never looks like an ignored click. It covers what `loading.tsx` cannot: a
 * change inside one layout, and the dev server compiling a page. Purely visual (`aria-hidden`); the
 * pages' own `LoadingState` carries the screen-reader "Loading…" status.
 */
export function NavigationProgress() {
  const pathname = usePathname();
  // Compared as a string: the query only "changes" when its text does, never on a re-render.
  const query = useSearchParams().toString();
  const [active, setActive] = useState(false);

  // The route rendered: the navigation is over.
  useEffect(() => {
    setActive(false);
  }, [pathname, query]);

  useEffect(() => {
    const start = () => setActive(true);

    const onClick = (event: MouseEvent) => {
      if (event.button !== 0) return;
      if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
      const anchor = (event.target as Element | null)?.closest?.('a[href]');
      if (!(anchor instanceof HTMLAnchorElement)) return;
      if (anchor.target && anchor.target !== '_self') return;
      if (anchor.hasAttribute('download')) return;
      const url = new URL(anchor.href, window.location.href);
      if (url.origin !== window.location.origin || isSamePageTarget(url)) return;
      start();
    };

    const onSubmit = (event: SubmitEvent) => {
      const form = event.target as HTMLFormElement;
      if ((form.method || 'get').toLowerCase() !== 'get' || !form.action) return;
      const url = new URL(form.action, window.location.href);
      if (url.origin === window.location.origin) start();
    };

    // Capture phase: `next/link` cancels the click (preventDefault) to navigate client-side, so a
    // bubble-phase "was it cancelled?" check would miss every Link. A click that ends up going
    // nowhere is cleared by the safety timeout.
    document.addEventListener('click', onClick, true);
    document.addEventListener('submit', onSubmit, true);
    window.addEventListener(START_EVENT, start);
    return () => {
      document.removeEventListener('click', onClick, true);
      document.removeEventListener('submit', onSubmit, true);
      window.removeEventListener(START_EVENT, start);
    };
  }, []);

  useEffect(() => {
    if (!active) return;
    const timer = window.setTimeout(() => setActive(false), SAFETY_TIMEOUT_MS);
    return () => window.clearTimeout(timer);
  }, [active]);

  if (!active) return null;
  return (
    <div
      aria-hidden="true"
      data-testid="navigation-progress"
      className="pointer-events-none fixed inset-x-0 top-0 z-[100] h-[3px] overflow-hidden bg-[var(--accent-soft)]"
    >
      <div className="h-full w-1/3 bg-[var(--accent)] motion-safe:animate-[nav-progress_1.1s_ease-in-out_infinite]" />
    </div>
  );
}
