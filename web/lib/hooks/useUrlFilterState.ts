'use client';

import { useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { useFilterUrlUpdate } from './useFilterUrlUpdate';

export type UrlFilterValues<K extends string> = Record<K, string>;

/** How long a debounced field (free-text inputs — see `debouncedFields` below) waits after the
 *  last keystroke before writing to the URL. `flush` (wired to blur/Enter by the caller) writes
 *  immediately, bypassing this wait. */
export const FILTER_WRITE_DEBOUNCE_MS = 300;

/**
 * HO-ADM-002 §8.3 — local-state display front for `useFilterUrlUpdate`'s URL-search-param-backed
 * filters (audit-log, content list). TEST gate HIGH, second and third passes, 2026-09-28.
 *
 * The URL stays the single source of truth for *sharing* (deep links) and for *history*
 * (Back/Forward, via `useFilterUrlUpdate`'s typing-burst rule) but is a poor **display** source of
 * truth on its own: every edit becomes an async client-side navigation, and a controlled
 * input/select bound straight to `useSearchParams()` snaps back to the pre-navigation value on
 * every render until that navigation commits — corrupting fast typing (characters dropped or
 * reordered) and silently losing quick successive edits to different fields.
 *
 * `values` is local React state, updated **synchronously** on every `setValue` call, so a
 * controlled field always shows exactly what the user just did, regardless of how long the
 * matching `router.push`/`replace` takes to land, and regardless of `debouncedFields` below (the
 * *display* is never debounced — only the URL write is).
 *
 * Re-synced from the URL only when the URL matches **none** of this hook's own recorded writes —
 * see `useFilterUrlUpdate`'s doc comment for why a list of every pending write, not a single
 * "last write" string: the second pass's single-string comparison could mistake an intermediate,
 * already-superseded write of its own for an external navigation and silently regress local state
 * by one edit (TEST gate HIGH, third pass — a single dropped character, non-deterministic, at
 * ordinary typing speed). A fresh deep link needs no special handling: it is a full page load, so
 * the initial `useState` read already has it.
 *
 * `debouncedFields` (third pass) — free-text inputs write to the URL ~`FILTER_WRITE_DEBOUNCE_MS`
 * after the last keystroke rather than on every one, so a burst of typing produces at most a
 * handful of navigations instead of one per character; `flush(name)` (wired to the field's own
 * `onBlur`/Enter by the caller) writes immediately, so the URL is never left stale once the person
 * has moved on. Fields not listed (selects, dates) write immediately, unchanged — they do not
 * receive rapid per-keystroke `setValue` calls the way a text input does, so debouncing them would
 * only add latency with no benefit.
 */
export function useUrlFilterState<K extends string>(
  fieldNames: readonly K[],
  defaults: Readonly<Partial<Record<string, string>>> = {},
  debouncedFields: readonly K[] = [],
): {
  values: UrlFilterValues<K>;
  setValue: (name: string, value: string) => void;
  /** Writes a debounced field's current value immediately, cancelling its pending wait. A no-op
   *  for a field not in `debouncedFields` or with nothing pending. */
  flush: (name: string) => void;
} {
  const searchParams = useSearchParams();
  const searchParamsQuery = searchParams.toString();
  const setUrlFilter = useFilterUrlUpdate();

  function readFromUrl(): UrlFilterValues<K> {
    const next = {} as UrlFilterValues<K>;
    for (const name of fieldNames) {
      next[name] = searchParams.get(name) ?? defaults[name] ?? '';
    }
    return next;
  }

  const [values, setValues] = useState<UrlFilterValues<K>>(readFromUrl);
  // Every query this hook has written to the router that the URL hasn't yet been observed to
  // reflect, oldest first — see `useFilterUrlUpdate`'s matching doc comment.
  const pendingWrites = useRef<string[]>([]);
  const mounted = useRef(false);
  // Each pending debounced write keeps the value captured when its timer was set, so `flush` and
  // the timer write the same thing regardless of which render's closure runs them (ADM backlog 36).
  const debounceTimers = useRef<
    Map<string, { timer: ReturnType<typeof setTimeout>; value: string }>
  >(new Map());
  const searchParamsQueryRef = useRef(searchParamsQuery);
  searchParamsQueryRef.current = searchParamsQuery;
  const debouncedFieldSet = new Set<string>(debouncedFields);

  useEffect(() => {
    if (!mounted.current) {
      // Initial render already read the starting values above.
      mounted.current = true;
      return;
    }
    const index = pendingWrites.current.indexOf(searchParamsQuery);
    if (index !== -1) {
      // One of this hook's own writes has landed — drop it and everything written before it.
      // Local state is already at least this fresh (updated synchronously in `setValue`, before
      // any navigation was even asked for), so there is nothing to resync.
      pendingWrites.current = pendingWrites.current.slice(index + 1);
      return;
    }
    // Not something this hook wrote — a genuine external navigation (Back, Forward). Resync every
    // field's local state from the real URL and discard every write this hook thought was still
    // pending; none of them apply any more.
    setValues(readFromUrl());
    pendingWrites.current = [];
    // ADM backlog 22: the field was just resynced to the URL, so a debounced write still waiting
    // from before the Back/Forward would push the abandoned value as a stray history entry.
    for (const { timer } of debounceTimers.current.values()) clearTimeout(timer);
    debounceTimers.current.clear();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParamsQuery]);

  useEffect(() => {
    // Nothing must write to the router once this component is gone.
    const timers = debounceTimers.current;
    return () => {
      for (const { timer } of timers.values()) clearTimeout(timer);
      timers.clear();
    };
  }, []);

  function writeToUrl(name: string, value: string) {
    // ADM backlog 33: `setUrlFilter` does nothing for a write that leaves the query unchanged, so
    // don't record it either — it would never drain.
    const baseline = pendingWrites.current.at(-1) ?? searchParamsQueryRef.current;
    const query = setUrlFilter(name, value);
    if (query !== baseline) pendingWrites.current.push(query);
  }

  function setValue(name: string, value: string) {
    setValues((current) => ({ ...current, [name as K]: value }));
    if (!debouncedFieldSet.has(name)) {
      writeToUrl(name, value);
      return;
    }
    const existing = debounceTimers.current.get(name);
    if (existing) clearTimeout(existing.timer);
    debounceTimers.current.set(name, {
      value,
      timer: setTimeout(() => {
        debounceTimers.current.delete(name);
        writeToUrl(name, value);
      }, FILTER_WRITE_DEBOUNCE_MS),
    });
  }

  function flush(name: string) {
    const pending = debounceTimers.current.get(name);
    if (!pending) return;
    clearTimeout(pending.timer);
    debounceTimers.current.delete(name);
    writeToUrl(name, pending.value);
  }

  return { values, setValue, flush };
}
