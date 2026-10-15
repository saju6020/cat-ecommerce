'use client';

import * as React from 'react';
import { useDebouncedValue } from '@/lib/hooks/useDebouncedValue';
import { inputClass } from '@/components/inventory/fields';
import { cn } from '@/lib/utils';

/**
 * `components/formulary/ListboxPicker.tsx` — the type-ahead the pharmacy forms share (medicine,
 * linked SKU, diet product, cat). Same keyboard model as the inventory `SkuPicker` (ArrowDown/Up,
 * Enter, Escape) and the same ARIA shape (`combobox` + `listbox` + `option`), generic over the row. An
 * option can be DISABLED with its reason written out as text (never colour alone): it is announced,
 * shown, and cannot be chosen. The caller owns the data: `onSearch` receives the debounced term and the
 * caller runs its query and passes `items` back.
 */
export interface ListboxPickerProps<T> {
  id: string;
  label: string;
  items: T[];
  isLoading?: boolean;
  isError?: boolean;
  getKey: (item: T) => string;
  /** The text a screen reader reads for the option (also the visible first line). */
  getLabel: (item: T) => string;
  renderDetail?: (item: T) => React.ReactNode;
  /** A reason makes the option disabled. */
  getDisabledReason?: (item: T) => string | null;
  onSearch: (term: string) => void;
  onSelect: (item: T) => void;
  minLength?: number;
  placeholder?: string;
  hint?: string;
  error?: string;
  required?: boolean;
  disabled?: boolean;
  /** Clear the box after a choice (a picker that adds rows) or keep the chosen label. */
  clearOnSelect?: boolean;
  noun?: string;
}

export function ListboxPicker<T>({
  id,
  label,
  items,
  isLoading,
  isError,
  getKey,
  getLabel,
  renderDetail,
  getDisabledReason,
  onSearch,
  onSelect,
  minLength = 2,
  placeholder = 'Type to search',
  hint,
  error,
  required,
  disabled,
  clearOnSelect = true,
  noun = 'results',
}: ListboxPickerProps<T>) {
  const [term, setTerm] = React.useState('');
  const [open, setOpen] = React.useState(false);
  const [active, setActive] = React.useState(0);
  const debounced = useDebouncedValue(term, 250);
  const listId = `${id}-listbox`;
  const showList = open && term.trim().length >= minLength;

  React.useEffect(() => {
    onSearch(debounced.trim().length >= minLength ? debounced.trim() : '');
    setActive(0);
    // `onSearch` is the caller's state setter; running on the debounced term only is the intent.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debounced, minLength]);

  function choose(item: T) {
    if (getDisabledReason?.(item)) return;
    onSelect(item);
    setTerm(clearOnSelect ? '' : getLabel(item));
    setOpen(false);
  }

  function handleKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setOpen(true);
      setActive((index) => Math.min(index + 1, Math.max(items.length - 1, 0)));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      setActive((index) => Math.max(index - 1, 0));
    } else if (event.key === 'Enter' && showList && items[active]) {
      event.preventDefault();
      choose(items[active]!);
    } else if (event.key === 'Escape' && open) {
      event.preventDefault();
      event.stopPropagation();
      setOpen(false);
    }
  }

  const waiting = term.trim() !== debounced.trim();
  const status = !showList
    ? ''
    : isLoading || waiting
      ? 'Searching…'
      : isError
        ? 'Could not search just now.'
        : items.length === 0
          ? `No ${noun} match.`
          : `${items.length} ${noun} found. Use the arrow keys, then Enter.`;

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
        aria-activedescendant={showList && items[active] ? `${id}-opt-${active}` : undefined}
        aria-invalid={!!error}
        aria-describedby={[hint ? `${id}-hint` : '', error ? `${id}-error` : '', `${id}-status`]
          .filter(Boolean)
          .join(' ')}
        disabled={disabled}
        value={term}
        placeholder={placeholder}
        onChange={(event) => {
          setTerm(event.target.value);
          setOpen(true);
        }}
        onFocus={() => setOpen(true)}
        onBlur={() => setOpen(false)}
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
          {items.length === 0 ? (
            <li role="presentation" className="px-3 py-2 text-sm text-ink-muted">
              {status}
            </li>
          ) : (
            items.map((item, index) => {
              const reason = getDisabledReason?.(item) ?? null;
              return (
                <li
                  key={getKey(item)}
                  id={`${id}-opt-${index}`}
                  role="option"
                  aria-selected={index === active}
                  aria-disabled={reason ? true : undefined}
                  // mouseDown (not click) so the input does not blur and close the list first.
                  onMouseDown={(event) => {
                    event.preventDefault();
                    choose(item);
                  }}
                  className={cn(
                    'flex flex-col gap-1 border-b border-border px-3 py-2 text-sm last:border-b-0',
                    reason ? 'cursor-not-allowed text-ink-muted' : 'cursor-pointer',
                    index === active && !reason && 'bg-accent-soft',
                  )}
                >
                  <span className="font-semibold text-ink">{getLabel(item)}</span>
                  {renderDetail && (
                    <span className="flex flex-wrap items-center gap-2 text-xs text-ink-muted">
                      {renderDetail(item)}
                    </span>
                  )}
                  {reason && (
                    <span className="text-xs font-semibold text-warning-ink">{reason}</span>
                  )}
                </li>
              );
            })
          )}
        </ul>
      )}
    </div>
  );
}
