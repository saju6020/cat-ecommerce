'use client';

import { useId } from 'react';
import { Minus, Plus } from 'lucide-react';

const STEP_BUTTON =
  'flex h-[42px] w-11 items-center justify-center rounded-pill bg-transparent text-ink ' +
  'disabled:cursor-not-allowed disabled:text-border-strong';

/**
 * `components/cart/QuantityStepper.tsx` — HO-ORD-002 §8.5/§11.1. Real `−` / `+` buttons with names
 * (*"Decrease quantity of …"*) plus a number input, in the Hearth pill. `max` is a **display limit the
 * server gave** (`maxQuantityPerLine`, or the quantity left in stock): reaching it disables `+` and says
 * why in text, and the server remains the authority (it answers `QuantityOutOfRange` for anything else).
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
      <div className="inline-flex w-fit items-center rounded-pill border border-border-strong bg-surface">
        <button
          type="button"
          aria-label={`Decrease quantity of ${label}`}
          disabled={disabled || value <= 1}
          onClick={() => onChange(value - 1)}
          className={STEP_BUTTON}
        >
          <Minus size={16} aria-hidden="true" />
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
          className="h-10 w-9 appearance-none border-0 bg-transparent text-center text-[15px] font-bold tabular-nums text-ink [-moz-appearance:textfield] [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none"
        />
        <button
          type="button"
          aria-label={`Increase quantity of ${label}`}
          disabled={disabled || atMax}
          onClick={() => onChange(value + 1)}
          className={STEP_BUTTON}
        >
          <Plus size={16} aria-hidden="true" />
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
