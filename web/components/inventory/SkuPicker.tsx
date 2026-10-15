'use client';

import * as React from 'react';
import { useDebouncedValue } from '@/lib/hooks/useDebouncedValue';
import { useSkuSearch } from '@/lib/hooks/useInventoryLookup';
import type { SkuLookup } from '@/lib/api/inventory-lookup';
import { StorageChip } from './StorageChip';
import { stockClassLabel } from './labels';
import { inputClass } from './fields';
import { cn } from '@/lib/utils';

/**
 * `components/inventory/SkuPicker.tsx` — HO-IMP-002 §11.1, OQ-1. Type-ahead over
 * `GET /api/staff/inventory/skus?search=` (≤ 20 rows). Keyboard first: type, ArrowDown/Up to move,
 * Enter to choose, Escape to close. Each option shows code, name, unit, class and a storage chip.
 * Emits the whole lookup row (id, unit, expiry flag, storage) so the form never refetches it.
 * After a choice the box clears, so the same picker adds the next line.
 */
export function SkuPicker({
  id,
  label = 'Find an item',
  onSelect,
  disabled,
  error,
  hint,
  required,
}: {
  id: string;
  label?: string;
  onSelect: (sku: SkuLookup) => void;
  disabled?: boolean;
  error?: string;
  hint?: string;
  required?: boolean;
}) {
  const [term, setTerm] = React.useState('');
  const [open, setOpen] = React.useState(false);
  const [active, setActive] = React.useState(0);
  const debounced = useDebouncedValue(term, 250);
  const search = useSkuSearch(debounced);
  const listId = `${id}-listbox`;
  const results = search.data ?? [];
  const showList = open && term.trim().length >= 2;

  React.useEffect(() => {
    setActive(0);
  }, [debounced]);

  function choose(sku: SkuLookup) {
    onSelect(sku);
    setTerm('');
    setOpen(false);
  }

  function handleKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setOpen(true);
      setActive((index) => Math.min(index + 1, Math.max(results.length - 1, 0)));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      setActive((index) => Math.max(index - 1, 0));
    } else if (event.key === 'Enter' && showList && results[active]) {
      event.preventDefault();
      choose(results[active]!);
    } else if (event.key === 'Escape' && open) {
      event.preventDefault();
      event.stopPropagation();
      setOpen(false);
    }
  }

  const status = !showList
    ? ''
    : search.isLoading || term.trim() !== debounced.trim()
      ? 'Searching…'
      : search.isError
        ? 'Could not search just now.'
        : results.length === 0
          ? 'No items match.'
          : `${results.length} ${results.length === 1 ? 'item' : 'items'} found. Use the arrow keys, then Enter.`;

  return (
    <div className="relative flex min-w-0 flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-semibold text-ink">
        {label}
        {required && (
          <span aria-hidden="true" className="text-danger-ink">
            {' '}
            *
          </span>
        )}
      </label>
      <input
        id={id}
        type="text"
        role="combobox"
        autoComplete="off"
        aria-expanded={showList}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={showList && results[active] ? `${id}-opt-${active}` : undefined}
        aria-invalid={!!error}
        aria-describedby={[hint ? `${id}-hint` : '', error ? `${id}-error` : '', `${id}-status`]
          .filter(Boolean)
          .join(' ')}
        disabled={disabled}
        value={term}
        placeholder="Type a name or code"
        onChange={(event) => {
          setTerm(event.target.value);
          setOpen(true);
        }}
        onFocus={() => setOpen(true)}
        onKeyDown={handleKeyDown}
        className={cn(inputClass, error && 'border-danger-ink')}
      />
      {hint && (
        <p id={`${id}-hint`} className="text-xs text-ink-muted">
          {hint}
        </p>
      )}
      {error && (
        <p id={`${id}-error`} role="alert" className="text-xs font-semibold text-danger-ink">
          {error}
        </p>
      )}
      <p id={`${id}-status`} role="status" aria-live="polite" className="sr-only">
        {status}
      </p>
      {showList && (
        <ul
          id={listId}
          role="listbox"
          aria-label={`${label} results`}
          className="absolute left-0 right-0 top-full z-20 mt-1 max-h-72 overflow-y-auto rounded-[var(--radius-md)] border border-border bg-surface shadow-modal"
        >
          {results.length === 0 ? (
            <li role="presentation" className="px-3 py-2 text-sm text-ink-muted">
              {status}
            </li>
          ) : (
            results.map((sku, index) => (
              <li
                key={sku.skuId}
                id={`${id}-opt-${index}`}
                role="option"
                aria-selected={index === active}
                // mouseDown (not click) so the input does not blur and close the list first.
                onMouseDown={(event) => {
                  event.preventDefault();
                  choose(sku);
                }}
                className={cn(
                  'flex cursor-pointer flex-col gap-1 border-b border-border px-3 py-2 text-sm last:border-b-0',
                  index === active && 'bg-accent-soft',
                )}
              >
                <span className="font-semibold text-ink">
                  {sku.displayName} <span className="font-normal text-ink-muted">— {sku.skuCode}</span>
                </span>
                <span className="flex flex-wrap items-center gap-2 text-xs text-ink-muted">
                  <span>Counted in {sku.stockUnit}</span>
                  <span>{stockClassLabel(sku.stockClass)}</span>
                  {!sku.isActive && <span className="font-semibold text-danger-ink">Inactive</span>}
                  <StorageChip storage={sku.storage} />
                </span>
              </li>
            ))
          )}
        </ul>
      )}
    </div>
  );
}
