'use client';

import * as React from 'react';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { Banner, SelectField, TextAreaField, TextField } from './fields';
import { STORAGE_CONDITION_OPTIONS } from './labels';
import { describeUnexpectedError, inventoryViolations, toFieldName } from '@/lib/api/errors';
import { TEMPERATURE_PATTERN } from '@/lib/validation/inventory';
import type { SetStorageProfileRequest, Storage, StorageCondition } from '@/lib/api/inventory-lookup';

/** `String(7)`-style display of a stored temperature, without `Number`. */
function temperatureText(value: number | null): string {
  return value === null ? '' : String(value);
}

/**
 * `components/inventory/StorageProfileForm.tsx` — HO-IMP-002 §11.8, US-IMP-16. How a product must be
 * kept: condition, minimum and maximum °C and handling notes. The server's `InvalidTemperatureRange`
 * lands on the minimum field. Temperatures are decimal strings and are never turned into numbers here.
 */
export function StorageProfileForm({
  productLabel,
  initial,
  onSave,
  isPending,
}: {
  productLabel: string;
  initial: Storage | null;
  onSave: (request: SetStorageProfileRequest) => Promise<unknown>;
  isPending: boolean;
}) {
  const [condition, setCondition] = React.useState<StorageCondition>(initial?.condition ?? 'Ambient');
  const [min, setMin] = React.useState(temperatureText(initial?.minTemperatureC ?? null));
  const [max, setMax] = React.useState(temperatureText(initial?.maxTemperatureC ?? null));
  const [notes, setNotes] = React.useState(initial?.handlingNotes ?? '');
  const [errors, setErrors] = React.useState<Record<string, string>>({});
  const [general, setGeneral] = React.useState<string | null>(null);
  const [dirty, setDirty] = React.useState(false);
  const [saved, setSaved] = React.useState(false);

  const touch = () => {
    setDirty(true);
    setSaved(false);
  };

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const local: Record<string, string> = {};
    if (min.trim() !== '' && !TEMPERATURE_PATTERN.test(min.trim())) local.minTemperatureC = 'Enter a temperature in °C, such as 2 or 4.5.';
    if (max.trim() !== '' && !TEMPERATURE_PATTERN.test(max.trim())) local.maxTemperatureC = 'Enter a temperature in °C, such as 8 or 25.';
    setErrors(local);
    if (Object.keys(local).length > 0) return;
    setGeneral(null);
    try {
      await onSave({
        condition,
        minTemperatureC: min.trim() === '' ? null : min.trim(),
        maxTemperatureC: max.trim() === '' ? null : max.trim(),
        handlingNotes: notes.trim() === '' ? null : notes.trim(),
      });
      setDirty(false);
      setSaved(true);
    } catch (error) {
      const violations = inventoryViolations(error);
      if (violations.length === 0) {
        setGeneral(describeUnexpectedError(error));
        return;
      }
      const next: Record<string, string> = {};
      for (const v of violations) next[toFieldName(v.propertyName)] ??= v.message;
      setErrors(next);
      if (!next.minTemperatureC && !next.maxTemperatureC && !next.condition && !next.handlingNotes) setGeneral(violations[0]!.message);
    }
  }

  return (
    <form onSubmit={submit} noValidate className="flex max-w-xl flex-col gap-4">
      <UnsavedChangesGuard when={dirty} />
      <h2 className="font-display text-lg font-semibold text-ink">{productLabel}</h2>
      {general && <Banner tone="danger">{general}</Banner>}
      <SelectField id="storage-condition" label="Condition" required value={condition} error={errors.condition} onChange={(e) => { setCondition(e.target.value as StorageCondition); touch(); }}>
        {STORAGE_CONDITION_OPTIONS.map((o) => (<option key={o.value} value={o.value}>{o.label}</option>))}
      </SelectField>
      <div className="grid gap-4 sm:grid-cols-2">
        <TextField id="storage-min" label="Minimum temperature (°C)" inputMode="decimal" value={min} error={errors.minTemperatureC} onChange={(e) => { setMin(e.target.value); touch(); }} />
        <TextField id="storage-max" label="Maximum temperature (°C)" inputMode="decimal" value={max} error={errors.maxTemperatureC} onChange={(e) => { setMax(e.target.value); touch(); }} />
      </div>
      <TextAreaField id="storage-notes" label="Handling notes" value={notes} error={errors.handlingNotes} onChange={(e) => { setNotes(e.target.value); touch(); }} />
      {saved && <Banner tone="info">Saved.</Banner>}
      <div>
        <Button type="submit" disabled={isPending} aria-busy={isPending}>{isPending ? 'Saving…' : 'Save storage profile'}</Button>
      </div>
    </form>
  );
}
