'use client';

import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { MeasurementInput } from '../MeasurementInput';
import { VitalsConfirmDialog } from './VitalsConfirmDialog';
import { ApiValidationError } from '@/lib/api/client';
import { describeClinicalError, mapClinicalFormErrors } from '@/lib/api/errors';
import { useVitalSignRules } from '@/lib/hooks/useVisit';
import { timeFieldLabel, todayInZone } from '@/lib/format/time';
import {
  EMPTY_VITALS,
  MAX_VITALS_NOTE,
  VITAL_FIELDS,
  hasAnyVital,
  plausibleRangeHint,
  toVitalsRequest,
  type VitalsFormValues,
} from '@/lib/validation/clinical';
import type { OutOfRangeSign, RecordVitalsRequest, VitalSign } from '@/lib/api/visits';

/**
 * ⭐ `VitalsForm` — HO-EMR-002 §11.2 (US-CLN-11). Six `MeasurementInput`s (each with its unit in its
 * accessible name), a measured-at time in the BRANCH zone (defaults to now) and a note; at least one
 * value. As you type, a value outside the server's plausible range (`vital-sign-rules`) gets an early
 * hint — but the save is what decides: a `400 VitalsOutsidePlausibleRange` opens the confirm listing
 * each value, and *Confirm and save* re-posts with `confirmedOutOfRange` (ADR-FE-052). An absolute
 * refusal (`VitalSignOutOfRange`) lands on its field with no confirm offered.
 */
function nowInZone(zone: string): string {
  const time = new Intl.DateTimeFormat('en-GB', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone: zone,
  }).format(new Date());
  return `${todayInZone(zone)}T${time}`;
}

const FIELD_FOR_SIGN: Record<string, string> = Object.fromEntries(
  VITAL_FIELDS.map(({ sign, field }) => [sign, field]),
);

export function VitalsForm({
  zone,
  save,
  onSaved,
  idPrefix = 'vitals',
  submitLabel = 'Save vitals',
}: {
  zone: string;
  save: (request: RecordVitalsRequest) => Promise<unknown>;
  onSaved?: () => void;
  idPrefix?: string;
  submitLabel?: string;
}) {
  const rules = useVitalSignRules();
  const ruleFor = useMemo(
    () => new Map((rules.data ?? []).map((rule) => [rule.sign, rule])),
    [rules.data],
  );
  const [values, setValues] = useState<VitalsFormValues>(() => ({
    ...EMPTY_VITALS,
    measuredOn: nowInZone(zone),
  }));
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [rootError, setRootError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [confirm, setConfirm] = useState<{ items: OutOfRangeSign[]; error: string | null } | null>(
    null,
  );

  const set = (patch: Partial<VitalsFormValues>) =>
    setValues((previous) => ({ ...previous, ...patch }));

  async function submit(confirmed: VitalSign[]) {
    setPending(true);
    setRootError(null);
    setFieldErrors({});
    try {
      await save(toVitalsRequest(values, confirmed, zone));
      setConfirm(null);
      setValues({ ...EMPTY_VITALS, measuredOn: nowInZone(zone) });
      onSaved?.();
    } catch (error) {
      const code = error instanceof ApiValidationError ? error.response.errors[0]?.errorCode : null;
      if (code === 'VitalsOutsidePlausibleRange') {
        const items =
          ((error as ApiValidationError).response as unknown as { outOfRange?: OutOfRangeSign[] })
            .outOfRange ?? [];
        setConfirm({ items, error: null });
      } else if (confirm) {
        setConfirm({ ...confirm, error: describeClinicalError(error) });
      } else {
        const mapped = mapClinicalFormErrors(error, {
          ...FIELD_FOR_SIGN,
          MeasuredOn: 'measuredOn',
        });
        setFieldErrors(mapped.fields);
        setRootError(mapped.root);
      }
    } finally {
      setPending(false);
    }
  }

  function correct() {
    const first = confirm?.items[0];
    setConfirm(null);
    const field = first ? FIELD_FOR_SIGN[first.sign] : undefined;
    requestAnimationFrame(() =>
      document.getElementById(`${idPrefix}-${field ?? 'weight'}`)?.focus(),
    );
  }

  return (
    <form
      noValidate
      className="flex flex-col gap-4"
      onSubmit={(event) => {
        event.preventDefault();
        if (!hasAnyVital(values)) {
          setRootError('Enter at least one measurement.');
          return;
        }
        void submit([]);
      }}
    >
      <div className="grid grid-cols-1 gap-3 min-[420px]:grid-cols-2">
        {VITAL_FIELDS.map((field) => {
          const rule = ruleFor.get(field.sign);
          return (
            <MeasurementInput
              key={field.field}
              id={`${idPrefix}-${field.field}`}
              label={field.label}
              unitName={field.unitName}
              unitSymbol={rule?.unit ?? field.fallbackSymbol}
              decimalPlaces={rule?.decimalPlaces ?? field.fallbackDecimals}
              value={values[field.field]}
              onChange={(next) => set({ [field.field]: next })}
              warning={plausibleRangeHint(values[field.field], rule) ?? undefined}
              error={fieldErrors[field.field]}
            />
          );
        })}
      </div>
      <div className="flex flex-col gap-1.5">
        <label htmlFor={`${idPrefix}-measured`} className="text-sm font-semibold text-ink">
          {timeFieldLabel('Measured at', zone)}
        </label>
        <input
          id={`${idPrefix}-measured`}
          type="datetime-local"
          value={values.measuredOn}
          onChange={(event) => set({ measuredOn: event.target.value })}
          className="w-full max-w-xs rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        />
        {fieldErrors.measuredOn && (
          <p role="alert" className="text-xs font-semibold text-danger-ink">
            {fieldErrors.measuredOn}
          </p>
        )}
      </div>
      <div className="flex flex-col gap-1.5">
        <label htmlFor={`${idPrefix}-note`} className="text-sm font-semibold text-ink">
          Note <span className="font-normal text-ink-muted">(optional)</span>
        </label>
        <input
          id={`${idPrefix}-note`}
          type="text"
          maxLength={MAX_VITALS_NOTE}
          value={values.note}
          onChange={(event) => set({ note: event.target.value })}
          className="w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        />
      </div>
      {rootError && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {rootError}
        </p>
      )}
      <div>
        <Button type="submit" disabled={pending} aria-busy={pending}>
          {pending && !confirm ? 'Saving…' : submitLabel}
        </Button>
      </div>
      <VitalsConfirmDialog
        open={confirm !== null}
        items={confirm?.items ?? []}
        pending={pending}
        error={confirm?.error ?? null}
        onConfirm={() => void submit(confirm?.items.map((item) => item.sign) ?? [])}
        onCorrect={correct}
      />
    </form>
  );
}
