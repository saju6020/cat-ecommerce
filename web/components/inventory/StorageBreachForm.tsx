'use client';

import * as React from 'react';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { Banner, TextAreaField, TextField } from './fields';
import { LocationSelect } from './LocationSelect';
import { branchLocalToInstant } from '@/lib/format/inventory';
import { describeUnexpectedError, inventoryViolations, toFieldName } from '@/lib/api/errors';
import { TEMPERATURE_PATTERN } from '@/lib/validation/inventory';
import type { StockLocation } from '@/lib/api/inventory-lookup';
import type { ReportBreachRequest } from '@/lib/api/inventory-breaches';

/**
 * `components/inventory/StorageBreachForm.tsx` — HO-IMP-002 §11.8, US-IMP-16 AC2. Where it happened,
 * when it was observed (in the branch's time — never in the future), what happened, the temperature
 * and how long. Anyone who handles stock may report. On success the page shows the affected batches
 * the server flagged. Times are sent as ISO instants.
 */
export function StorageBreachForm({
  locations,
  nowLocal,
  timeZoneId,
  onReport,
  isPending,
}: {
  locations: StockLocation[] | undefined;
  /** "Now" as a `datetime-local` value in the branch's zone — the latest time that may be chosen. */
  nowLocal: string;
  timeZoneId: string;
  onReport: (request: ReportBreachRequest) => Promise<void>;
  isPending: boolean;
}) {
  const [locationId, setLocationId] = React.useState('');
  const [observed, setObserved] = React.useState(nowLocal);
  const [condition, setCondition] = React.useState('');
  const [temperature, setTemperature] = React.useState('');
  const [minutes, setMinutes] = React.useState('');
  const [errors, setErrors] = React.useState<Record<string, string>>({});
  const [general, setGeneral] = React.useState<string | null>(null);
  const [dirty, setDirty] = React.useState(false);
  const [submitting, setSubmitting] = React.useState(false);
  const touch = () => setDirty(true);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const local: Record<string, string> = {};
    if (!locationId) local.locationId = 'Choose where it happened.';
    if (observed === '') local.observedOn = 'Say when it was observed.';
    else if (observed > nowLocal) local.observedOn = 'The time it was observed cannot be in the future.';
    if (condition.trim() === '') local.condition = 'Say what happened.';
    if (temperature.trim() !== '' && !TEMPERATURE_PATTERN.test(temperature.trim())) local.temperatureC = 'Enter the temperature in °C, such as 11.5.';
    if (minutes.trim() !== '' && !/^\d+$/.test(minutes.trim())) local.durationMinutes = 'Enter the duration as whole minutes.';
    setErrors(local);
    const first = Object.keys(local)[0];
    if (first) {
      document.getElementById(`breach-${first}`)?.focus();
      return;
    }
    setGeneral(null);
    setSubmitting(true);
    try {
      await onReport({
        locationId,
        observedOn: branchLocalToInstant(observed, timeZoneId),
        condition: condition.trim(),
        temperatureC: temperature.trim() === '' ? null : temperature.trim(),
        durationMinutes: minutes.trim() === '' ? null : parseInt(minutes.trim(), 10),
      });
      setDirty(false);
    } catch (error) {
      setSubmitting(false);
      const violations = inventoryViolations(error);
      if (violations.length === 0) {
        setGeneral(describeUnexpectedError(error));
        return;
      }
      const next: Record<string, string> = {};
      for (const v of violations) next[toFieldName(v.propertyName)] ??= v.message;
      setErrors(next);
      const known = ['locationId', 'observedOn', 'condition', 'temperatureC', 'durationMinutes'];
      if (!Object.keys(next).some((k) => known.includes(k))) setGeneral(violations[0]!.message);
    }
  }

  return (
    <form onSubmit={submit} noValidate className="flex max-w-2xl flex-col gap-4">
      <UnsavedChangesGuard when={dirty && !submitting} />
      {general && <Banner tone="danger">{general}</Banner>}
      <LocationSelect id="breach-locationId" label="Where did it happen?" required locations={locations} value={locationId} error={errors.locationId} onChange={(v) => { setLocationId(v); touch(); }} />
      <TextField
        id="breach-observedOn"
        label={`When was it observed? (${timeZoneId})`}
        type="datetime-local"
        required
        max={nowLocal}
        value={observed}
        error={errors.observedOn}
        onChange={(e) => { setObserved(e.target.value); touch(); }}
      />
      <TextAreaField id="breach-condition" label="What happened?" required value={condition} error={errors.condition} onChange={(e) => { setCondition(e.target.value); touch(); }} />
      <div className="grid gap-4 sm:grid-cols-2">
        <TextField id="breach-temperatureC" label="Temperature reached (°C)" inputMode="decimal" value={temperature} error={errors.temperatureC} onChange={(e) => { setTemperature(e.target.value); touch(); }} />
        <TextField id="breach-durationMinutes" label="For how long (minutes)" inputMode="numeric" value={minutes} error={errors.durationMinutes} onChange={(e) => { setMinutes(e.target.value); touch(); }} />
      </div>
      <div>
        <Button type="submit" disabled={isPending || submitting} aria-busy={isPending}>Report the breach</Button>
      </div>
    </form>
  );
}
