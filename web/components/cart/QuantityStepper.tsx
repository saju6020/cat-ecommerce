'use client';

import { useId } from 'react';
import { Minus, Plus } from 'lucide-react';

/**
 * `components/cart/QuantityStepper.tsx` — HO-ORD-002 §8.5/§11.1. Real `−` / `+` buttons with names
 * (*"Decrease quantity of …"*) plus a number input. `max` is a **display limit the server gave**
 * (`maxQuantityPerLine`, or the quantity left in stock): reaching it disables `+` and says why in
 * text, and the server remains the authority (it answers `QuantityOutOfRange` for anything else).
 */
export function QuantityStepper({
  label,
  value,
  max,
  maxReason,
  disabled,
  onChange,
}: {
  label: string;
  value: number;
  max: number;
  /** Why `+` is disabled at `max`, e.g. "You can't add more — only 2 are in stock." */
  maxReason: string;
  disabled?: boolean;
  onChange: (quantity: number) => void;
}) {
  const hintId = useId();
  const atMax = value >= max;
  return (
    <div className="flex flex-col gap-1">
      <div className="inline-flex w-fit items-center overflow-hidden rounded-[var(--radius-sm)] border border-border-strong bg-surface">
        <button
          type="button"
          aria-label={`Decrease quantity of ${label}`}
          disabled={disabled || value <= 1}
          onClick={() => onChange(value - 1)}
          className="flex h-9 w-9 items-center justify-center bg-surface-sunken text-ink disabled:cursor-not-allowed disabled:text-ink-faint"
        >
          <Minus size={14} aria-hidden="true" />
        </button>
        <input
          type="number"
          inputMode="numeric"
          min={1}
          max={max}
          value={value}
          disabled={disabled}
          aria-label={`Quantity of ${label}`}
          aria-describedby={atMax ? hintId : undefined}
          onChange={(event) => {
            const next = event.target.valueAsNumber;
            if (Number.isInteger(next) && next >= 1) onChange(Math.min(next, max));
          }}
          className="h-9 w-12 border-x border-border bg-surface text-center text-sm tabular-nums text-ink"
        />
        <button
          type="button"
          aria-label={`Increase quantity of ${label}`}
          disabled={disabled || atMax}
          onClick={() => onChange(value + 1)}
          className="flex h-9 w-9 items-center justify-center bg-surface-sunken text-ink disabled:cursor-not-allowed disabled:text-ink-faint"
        >
          <Plus size={14} aria-hidden="true" />
        </button>
      </div>
      {atMax && (
        <p id={hintId} className="text-xs text-ink-muted">
          {maxReason}
        </p>
      )}
    </div>
  );
}
