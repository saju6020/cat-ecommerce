import * as React from 'react';
import type { StockLocation } from '@/lib/api/inventory-lookup';
import { locationKindLabel, movementLabel } from './labels';
import { SelectField } from './fields';

/**
 * `components/inventory/LocationSelect.tsx` — HO-IMP-002 §11.1. The branch's locations with their
 * kind. An option is DISABLED, with the reason in the option's own text, when the location's
 * `permittedMovements` (the server's table, HO-IMP-001 §8.3) exclude the screen's movement, when it
 * is inactive, or when the caller marks it (e.g. the other end of a transfer). Never colour alone.
 */
export interface LocationSelectProps {
  id: string;
  label: string;
  locations: StockLocation[] | undefined;
  value: string;
  onChange: (locationId: string) => void;
  /** The `MovementKind` name this screen performs (`Receipt`, `TransferOut`, `AdjustmentOut`…). */
  movement?: string;
  /** Extra reasons, keyed by location id (e.g. "Already the source"). */
  disabledReasons?: Record<string, string>;
  error?: string;
  hint?: string;
  required?: boolean;
  disabled?: boolean;
  placeholder?: string;
  className?: string;
  /** The first option's text when nothing is chosen. */
  allowEmpty?: boolean;
}

export function locationDisabledReason(
  location: StockLocation,
  movement: string | undefined,
  extra?: Record<string, string>,
): string | null {
  if (extra?.[location.id]) return extra[location.id]!;
  if (!location.isActive) return 'inactive';
  if (movement && !location.permittedMovements.includes(movement)) {
    return `a ${locationKindLabel(location.kind).toLowerCase()} doesn't take ${movementLabel(movement)}`;
  }
  return null;
}

export function LocationSelect({
  id,
  label,
  locations,
  value,
  onChange,
  movement,
  disabledReasons,
  error,
  hint,
  required,
  disabled,
  placeholder = 'Choose a location',
  className,
  allowEmpty = true,
}: LocationSelectProps) {
  return (
    <SelectField
      id={id}
      label={label}
      value={value}
      onChange={(event) => onChange(event.target.value)}
      error={error}
      hint={hint}
      required={required}
      disabled={disabled || !locations}
      wrapperClassName={className}
    >
      {allowEmpty && <option value="">{placeholder}</option>}
      {(locations ?? []).map((location) => {
        const reason = locationDisabledReason(location, movement, disabledReasons);
        return (
          <option key={location.id} value={location.id} disabled={reason !== null}>
            {location.name} ({locationKindLabel(location.kind)})
            {reason ? ` — not available: ${reason}` : ''}
          </option>
        );
      })}
    </SelectField>
  );
}
