'use client';

import { useMemo } from 'react';
import { useSearchParams } from 'next/navigation';
import { useDebouncedValue } from './useDebouncedValue';
import { useUrlFilterState, FILTER_WRITE_DEBOUNCE_MS } from './useUrlFilterState';

/**
 * `lib/hooks/useInventoryListState.ts` — HO-IMP-002 §8.3, UI-4. List filters live in URL search
 * params (shareable, back-button safe) through the same `useUrlFilterState` the audit log uses: the
 * fields are local state synced from the URL, free-text fields write to the URL (and the request)
 * after a short pause, and the page number comes from `?page=`. Returns the request-ready `params`
 * (empty values dropped).
 */
export function useInventoryListState<K extends string>(
  fields: readonly K[],
  options: {
    defaults?: Readonly<Partial<Record<string, string>>>;
    debounced?: readonly K[];
    pageSize?: number;
  } = {},
) {
  const { defaults = {}, debounced = [], pageSize = 25 } = options;
  const searchParams = useSearchParams();
  const state = useUrlFilterState(fields, defaults, debounced);
  const urlPage = parseInt(searchParams.get('page') ?? '1', 10) || 1;

  // `state.values` runs ahead of the URL while a filter is being written; paging restarts at 1.
  const ahead = fields.some(
    (name) => (searchParams.get(name) ?? defaults[name] ?? '') !== state.values[name],
  );
  const pageNumber = ahead ? 1 : urlPage;

  const settled = useDebouncedValue(state.values, FILTER_WRITE_DEBOUNCE_MS);
  const params = useMemo(() => {
    const out: Record<string, string | number> = { pageNumber, pageSize };
    for (const name of fields) {
      const value = settled[name];
      if (value) out[name] = value;
    }
    return out;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [settled, pageNumber, pageSize]);

  return { ...state, params, pageNumber, pageSize };
}
