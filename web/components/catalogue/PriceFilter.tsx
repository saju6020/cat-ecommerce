'use client';

import { useEffect, useId, useState } from 'react';
import { Button } from '@/components/ui/button';
import { parseMoneyForCompare } from '@/lib/format/shop';
import { pillInputClass } from '@/components/hearth/fields';
import { FilterGroup } from './FilterGroup';

export interface PriceFilterProps {
  minPrice: string;
  maxPrice: string;
  currencyCode: string;
  /** `button`: applied by *Apply price* (desktop). `live`: applied as the values become valid (the
   *  mobile sheet, which commits everything with *Show results*). */
  mode: 'button' | 'live';
  onApply: (range: { minPrice: string; maxPrice: string }) => void;
}

/**
 * HO-SHP-002 §11.2, U-A5 — min and max inputs labelled with the currency. `min <= max` is checked
 * here (`parseMoneyForCompare`, comparison only) BEFORE anything navigates; a bad range shows an
 * inline error and changes nothing.
 */
export function PriceFilter({ minPrice, maxPrice, currencyCode, mode, onApply }: PriceFilterProps) {
  const id = useId();
  const [min, setMin] = useState(minPrice);
  const [max, setMax] = useState(maxPrice);
  const [error, setError] = useState('');

  // Follow the URL/draft when it changes from outside (Clear all, a chip, Back) WITHOUT remounting
  // the inputs: a `key` on the value destroyed the focused input on every valid keystroke in the sheet.
  useEffect(() => {
    setMin(minPrice);
    setMax(maxPrice);
    setError('');
  }, [minPrice, maxPrice]);

  function validate(nextMin: string, nextMax: string): boolean {
    const bad = (value: string) => value !== '' && Number.isNaN(parseMoneyForCompare(value));
    if (bad(nextMin) || bad(nextMax)) {
      setError('Enter prices as plain numbers, for example 500 or 1200.50.');
      return false;
    }
    if (
      nextMin !== '' &&
      nextMax !== '' &&
      parseMoneyForCompare(nextMin) > parseMoneyForCompare(nextMax)
    ) {
      setError('The minimum price can’t be more than the maximum price.');
      return false;
    }
    setError('');
    return true;
  }

  function change(nextMin: string, nextMax: string) {
    setMin(nextMin);
    setMax(nextMax);
    if (mode === 'live' && validate(nextMin, nextMax)) {
      onApply({ minPrice: nextMin, maxPrice: nextMax });
    }
  }

  const inputClass = `${pillInputClass} aria-[invalid=true]:border-danger-ink`;
  return (
    <FilterGroup legend="Price" defaultOpen={false} active={!!(minPrice || maxPrice)}>
      <div className="flex items-end gap-2">
        <div className="flex-1">
          <label htmlFor={`${id}-min`} className="text-xs font-semibold text-ink-muted">
            Minimum ({currencyCode})
          </label>
          <input
            id={`${id}-min`}
            inputMode="decimal"
            value={min}
            aria-invalid={!!error}
            aria-describedby={error ? `${id}-error` : undefined}
            onChange={(event) => change(event.target.value, max)}
            className={inputClass}
          />
        </div>
        <div className="flex-1">
          <label htmlFor={`${id}-max`} className="text-xs font-semibold text-ink-muted">
            Maximum ({currencyCode})
          </label>
          <input
            id={`${id}-max`}
            inputMode="decimal"
            value={max}
            aria-invalid={!!error}
            aria-describedby={error ? `${id}-error` : undefined}
            onChange={(event) => change(min, event.target.value)}
            className={inputClass}
          />
        </div>
      </div>
      {error && (
        <p id={`${id}-error`} role="alert" className="text-xs font-semibold text-danger-ink">
          {error}
        </p>
      )}
      {mode === 'button' && (
        <Button
          variant="pillSecondary"
          size="pillMd"
          className="self-start"
          onClick={() => validate(min, max) && onApply({ minPrice: min, maxPrice: max })}
        >
          Apply price
        </Button>
      )}
    </FilterGroup>
  );
}
