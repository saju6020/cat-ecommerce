'use client';

import { useId } from 'react';
import { ArrowUpDown, ChevronDown } from 'lucide-react';
import { CATALOGUE_SORTS, type CatalogueSort } from '@/lib/shop/catalogueParams';
import { SORT_LABELS } from '@/lib/shop/labels';

export interface SortSelectProps {
  value: CatalogueSort | null;
  /** *Relevance* is offered only with a search (HO-SHP-002 §11.2). */
  hasQuery: boolean;
  onChange: (sort: CatalogueSort | null) => void;
}

/**
 * Changing the sort keeps every filter and the search — US-SHP-04 AC2.
 * 2026-10-16: one pill ("⇅ Sort: Newest ⌄") the height of the filter chips, instead of a bare
 * OS-styled select. It is still a native `<select>`, so the keyboard and the phone's own picker
 * work as before; the label "Sort by" stays for assistive tech.
 */
export function SortSelect({ value, hasQuery, onChange }: SortSelectProps) {
  const id = useId();
  const options = CATALOGUE_SORTS.filter((sort) => sort !== 'relevance' || hasQuery);
  const effective = value ?? (hasQuery ? 'relevance' : 'newest');
  return (
    <div className="relative inline-flex items-center">
      <label htmlFor={id} className="sr-only">
        Sort by
      </label>
      <span
        aria-hidden="true"
        className="pointer-events-none absolute left-4 flex items-center gap-1.5 text-sm text-ink-muted"
      >
        <ArrowUpDown size={15} />
        Sort:
      </span>
      <select
        id={id}
        value={effective}
        onChange={(event) => onChange(event.target.value as CatalogueSort)}
        className="min-h-11 cursor-pointer appearance-none rounded-pill border border-border-strong bg-surface pl-[5.1rem] pr-10 text-sm font-semibold text-ink transition-colors hover:border-ink-faint hover:bg-surface-sunken"
      >
        {options.map((sort) => (
          <option key={sort} value={sort}>
            {SORT_LABELS[sort]}
          </option>
        ))}
      </select>
      <ChevronDown
        size={16}
        aria-hidden="true"
        className="pointer-events-none absolute right-3.5 text-ink-muted"
      />
    </div>
  );
}
