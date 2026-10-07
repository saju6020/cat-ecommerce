'use client';

import { useCallback, useEffect, useRef } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';

/** Edits to the same field closer together than this are one "typing burst". */
export const FILTER_TYPING_BURST_MS = 1000;

/**
 * HO-ADM-002 §8.3 — list filters live in URL search params and are **back-button safe**: each
 * filter change adds a browser-history entry (`router.push`), so Back restores the previous
 * filters. A burst of keystrokes in one text field replaces its own entry instead of adding one
 * per character, so Back steps over whole edits, not letters.
 *
 * ADM backlog 33: a write whose resulting query equals the URL as it already stands (including
 * every write still in flight) changes nothing, so it neither navigates nor is recorded in
 * `pendingWrites` — an entry for a URL that never changes would never drain, and a later external
 * navigation to exactly that URL would then be mistaken for one of this hook's own writes. It still
 * returns the (unchanged) query string.
 *
 * Returns `setFilter(name, value)`: an empty value removes the param. Any change also drops
 * `page`, since a new filter starts from the first page. Returns the query string it just wrote
 * (without the leading `?`), so a caller that tracks its own pending writes (`useUrlFilterState`)
 * can do so without re-deriving it.
 *
 * TEST gate HIGH (second pass, 2026-09-28): quick successive edits to *different* fields (e.g. two
 * `<select>`s changed back to back) used to be computed from `useSearchParams()` — a snapshot that
 * is stale until the previous edit's async navigation commits — so the second edit silently
 * overwrote the first instead of merging with it. Fixed by building every new URL from
 * `pendingParams`, a ref updated **synchronously** inside this hook's own callback on every call,
 * never from a possibly-stale `searchParams` read.
 *
 * TEST gate HIGH (third pass, 2026-09-28): the second pass's own fix compared the live URL against
 * a *single* "last write" string (`expectedQuery`) to decide whether `pendingParams` needed
 * resyncing from an external navigation. That is not enough: `router.push`/`replace`'s actual
 * commit to the real History API can lag several hundred ms, and — critically — `useSearchParams()`
 * does not necessarily skip straight from one of this hook's writes to the next; it can pass
 * through an **intermediate, already-superseded** write of this hook's own on the way there (e.g.
 * mid-burst, the browser briefly reports the query from three keystrokes ago). Compared against a
 * single "last write" string, that intermediate value never matches, so it was wrongly treated as
 * an external navigation and used to reset `pendingParams` — silently losing whatever had been
 * typed since. Fixed by tracking **every** write this hook has made that the URL hasn't yet been
 * observed to reflect, oldest first (`pendingWrites`), not just the latest one. When the URL
 * matches *any* entry in that list — not only the newest — it is unambiguously this hook's own
 * write landing (an intermediate one, a coalesced one, or the final one), and both it and every
 * write recorded before it are dropped without ever touching `pendingParams`: a later write
 * landing implies every earlier one is superseded, even one the URL is never itself observed to
 * pass through (the browser can coalesce two close-together pushes into one History entry — see
 * `useUrlFilterState`'s own doc comment). Only a query that matches **nothing** in `pendingWrites`
 * is genuinely external (Back, Forward) and resyncs `pendingParams`, clearing every write this hook
 * thought was still in flight — none of them apply once the URL has moved somewhere this hook never
 * asked for.
 *
 * A real browser's native `popstate` event was tried, in the second pass, as the "was this external"
 * signal and rejected: confirmed directly against this app that the App Router can update
 * `useSearchParams()` — and this effect can already have re-run — *before* `popstate` reaches a
 * `window` listener (Chromium prefers the Navigation API internally, whose `navigate` event fires
 * ahead of the legacy `popstate` per spec), so a listener-based flag could still read as "not
 * external yet" on the one render that mattered. Comparing query strings has no such ordering
 * dependency: it only cares what the URL *is* relative to what this hook itself has asked for,
 * never *why* or *when* it got there.
 */
export function useFilterUrlUpdate(): (name: string, value: string) => string {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const searchParamsQuery = searchParams.toString();
  const lastEdit = useRef<{ name: string; at: number } | null>(null);
  const pendingParams = useRef<URLSearchParams>(new URLSearchParams(searchParamsQuery));
  // Every query this hook has written to the router that the URL hasn't yet been observed to
  // reflect, oldest first. See the doc comment above for why a list, not a single "last write".
  const pendingWrites = useRef<string[]>([]);
  const mounted = useRef(false);

  useEffect(() => {
    if (!mounted.current) {
      // The initial render already seeded `pendingParams` from `searchParams` above — nothing to
      // resync on the effect that merely confirms the first render happened.
      mounted.current = true;
      return;
    }
    const index = pendingWrites.current.indexOf(searchParamsQuery);
    if (index !== -1) {
      // One of this hook's own writes has landed — drop it and everything written before it.
      // `pendingParams` already reflects at least this state (built synchronously in the callback
      // below, before `router.push`/`replace` was even called), so there is nothing to resync.
      pendingWrites.current = pendingWrites.current.slice(index + 1);
      return;
    }
    // Not something this hook wrote — a genuine external navigation (Back, Forward). Resync from
    // the real URL and discard every write this hook thought was still pending; none of them apply
    // any more.
    pendingParams.current = new URLSearchParams(searchParamsQuery);
    pendingWrites.current = [];
  }, [searchParamsQuery]);

  return useCallback(
    (name: string, value: string) => {
      const baseline = pendingParams.current.toString();
      const params = new URLSearchParams(baseline);
      if (value) params.set(name, value);
      else params.delete(name);
      params.delete('page');
      const query = params.toString();
      if (query === baseline) return query; // nothing changes: no history entry, nothing to drain
      const url = query ? `${pathname}?${query}` : pathname;

      pendingParams.current = params;
      pendingWrites.current.push(query);

      const now = Date.now();
      const sameBurst =
        lastEdit.current?.name === name && now - lastEdit.current.at < FILTER_TYPING_BURST_MS;
      lastEdit.current = { name, at: now };

      if (sameBurst) router.replace(url);
      else router.push(url);

      return query;
    },
    [router, pathname],
  );
}
