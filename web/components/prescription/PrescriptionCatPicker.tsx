'use client';

import { useId, useState } from 'react';
import { formatBestBefore } from '@/lib/format/shop';
import type { EligibleCat } from '@/lib/api/shop-customer';

export interface PrescriptionCatPickerProps {
  eligibleCats: EligibleCat[];
  /** Fires with the chosen cat's id; the picker itself has NO side effect (US-SHP-10 AC2). */
  onChange: (catId: string) => void;
  value?: string;
  /** Set by the host when it tried to continue without a choice — announced as an alert. */
  showRequiredError?: boolean;
}

/**
 * HO-SHP-002 §11.3 `PrescriptionCatPicker` — **delivered for HO-ORD-002**, which records the chosen
 * cat on the order line at add-to-cart. A required radio group (*Which cat is this for?*) operable by
 * keyboard (arrows move and select). It performs no request and has no cart in this slice (§4).
 */
export function PrescriptionCatPicker({
  eligibleCats,
  onChange,
  value,
  showRequiredError,
}: PrescriptionCatPickerProps) {
  const id = useId();
  const [internal, setInternal] = useState<string | undefined>(value);
  const selected = value ?? internal;
  return (
    <fieldset
      className="m-0 min-w-0 border-0 p-0"
      aria-required="true"
      aria-describedby={showRequiredError ? `${id}-error` : undefined}
    >
      <legend className="mb-2 p-0 text-sm font-bold text-ink">Which cat is this for?</legend>
      <div className="flex flex-col gap-1.5">
        {eligibleCats.map((cat) => (
          <label
            key={cat.catId}
            className="flex min-h-8 cursor-pointer items-center gap-2 text-sm text-ink"
          >
            <input
              type="radio"
              name={`${id}-cat`}
              value={cat.catId}
              required
              checked={selected === cat.catId}
              onChange={() => {
                setInternal(cat.catId);
                onChange(cat.catId);
              }}
              className="h-4 w-4 accent-[var(--accent)]"
            />
            <span>
              {cat.name}{' '}
              <span className="text-ink-muted">
                (authorised until {formatBestBefore(cat.validTo)})
              </span>
            </span>
          </label>
        ))}
      </div>
      {showRequiredError && !selected && (
        <p id={`${id}-error`} role="alert" className="mt-1 text-xs font-semibold text-danger-ink">
          Choose which cat this is for.
        </p>
      )}
    </fieldset>
  );
}
