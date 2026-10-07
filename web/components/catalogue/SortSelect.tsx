'use client';

import { useId } from 'react';
import { CATALOGUE_SORTS, type CatalogueSort } from '@/lib/shop/catalogueParams';
import { SORT_LABELS } from '@/lib/shop/labels';

export interface SortSelectProps {
  value: CatalogueSort | null;
  /** *Relevance* is offered only with a search (HO-SHP-002 §11.2). */
  hasQuery: boolean;
  onChange: (sort: CatalogueSort | null) => void;
}

/** Changing the sort keeps every filter and the search — US-SHP-04 AC2. */
export function SortSelect({ value, hasQuery, onChange }: SortSelectProps) {
  const id = useId();
  const options = CATALOGUE_SORTS.filter((sort) => sort !== 'relevance' || hasQuery);
  const effective = value ?? (hasQuery ? 'relevance' : 'newest');
  return (
    <div className="flex items-center gap-2">
      <label htmlFor={id} className="text-sm font-semibold text-ink-muted">
        Sort by
      </label>
      <select
        id={id}
        value={effective}
        onChange={(event) => onChange(event.target.value as CatalogueSort)}
        className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
      >
        {options.map((sort) => (
          <option key={sort} value={sort}>
            {SORT_LABELS[sort]}
          </option>
        ))}
      </select>
    </div>
  );
}
