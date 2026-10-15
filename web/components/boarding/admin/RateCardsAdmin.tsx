'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { FormFieldRow } from '@/components/cat/FormFieldRow';
import { FormErrorAlert, SelectRow } from '@/components/boarding/shared/FormBits';
import { QueryGate } from '@/components/boarding/shared/QueryGate';
import { UNIT_TYPE_LABEL } from '@/components/boarding/shared/labels';
import { describeBoardingError } from '@/lib/api/errors';
import type { BoardingMoney, BoardingUnitType, EffectiveDatedHistory } from '@/lib/api/types';
import { formatInZone, nowAsDateTimeLocal } from '@/lib/format/time';
import { formatMoney } from '@/lib/format/money';
import { useTaxClasses } from '@/lib/hooks/useAdminCatalogue';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import {
  useBoardingRateCards,
  useDaycareRateCards,
  useScheduleDaycareRateCard,
  useScheduleRateCard,
} from '@/lib/hooks/useStaffBoarding';
import { toMoneyText } from '@/lib/validation/boarding';
import { daycareRateCardSchema, rateCardSchema } from '@/lib/validation/boarding';

type Field = { key: string; label: string };

function History<T extends { id: string; effectiveFrom: string; taxClassName: string | null }>({
  history,
  rows,
  zone,
}: {
  history: EffectiveDatedHistory<T>;
  rows: (card: T) => Array<[string, BoardingMoney]>;
  zone: string;
}) {
  const section = (title: string, cards: T[]) => (
    <section aria-label={title} className="flex flex-col gap-2">
      <h3 className="font-display text-base font-semibold text-ink">{title}</h3>
      {cards.length === 0 ? (
        <p className="text-sm text-ink-muted">None.</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {cards.map((c) => (
            <li
              key={c.id}
              className="rounded-[var(--radius-sm)] border border-border bg-surface p-3 text-sm"
            >
              <p className="font-semibold text-ink">From {formatInZone(c.effectiveFrom, zone)}</p>
              <p className="text-ink">
                {rows(c)
                  .map(([label, m]) => `${label} ${formatMoney(m)}`)
                  .join(' · ')}
                {c.taxClassName ? ` · ${c.taxClassName}` : ''}
              </p>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
  return (
    <div className="flex flex-col gap-4">
      {section('Current', history.current ? [history.current] : [])}
      {section('Scheduled', history.scheduled)}
      {section('Past', history.past)}
    </div>
  );
}

function ScheduleDialog({
  title,
  fields,
  onClose,
  onSubmit,
  schema,
}: {
  title: string;
  fields: Field[];
  onClose: () => void;
  onSubmit: (values: Record<string, string>) => Promise<void>;
  schema: typeof rateCardSchema | typeof daycareRateCardSchema;
}) {
  const tax = useTaxClasses();
  const [values, setValues] = useState<Record<string, string>>({
    taxClassId: '',
    effectiveFrom: '',
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function save() {
    const parsed = schema.safeParse(values);
    if (!parsed.success) {
      const next: Record<string, string> = {};
      for (const i of parsed.error.issues) next[String(i.path[0])] ??= i.message;
      setErrors(next);
      return;
    }
    setErrors({});
    setServerError(null);
    setBusy(true);
    try {
      await onSubmit(values);
      onClose();
    } catch (e) {
      setServerError(describeBoardingError(e));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent hideClose>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>
            A rate card is never edited — a new one takes over at the date you choose. It can’t
            start in the past.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-3 sm:grid-cols-2">
          {fields.map((f) => (
            <FormFieldRow
              key={f.key}
              id={`rc-${f.key}`}
              label={f.label}
              inputMode="decimal"
              value={values[f.key] ?? ''}
              error={errors[f.key]}
              onChange={(e) => setValues({ ...values, [f.key]: e.target.value })}
            />
          ))}
          <SelectRow
            id="rc-tax"
            label="Tax class"
            value={values.taxClassId}
            error={errors.taxClassId}
            onChange={(e) => setValues({ ...values, taxClassId: e.target.value })}
          >
            <option value="">Choose…</option>
            {(tax.data ?? [])
              .filter((t) => t.isActive)
              .map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name}
                </option>
              ))}
          </SelectRow>
          <FormFieldRow
            id="rc-from"
            label="Starts"
            type="datetime-local"
            min={nowAsDateTimeLocal()}
            value={values.effectiveFrom}
            error={errors.effectiveFrom}
            onChange={(e) => setValues({ ...values, effectiveFrom: e.target.value })}
          />
        </div>
        <FormErrorAlert message={serverError} />
        <DialogFooter>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={() => void save()} disabled={busy}>
            {busy ? 'Scheduling…' : 'Schedule'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** `RateCardEditor` — HO-BRD-002 §11.11, US-BRD-03, HO-ADM-002 ADR-FE-018: Current / Scheduled / Past per
 *  unit type, **never editable**; a new card is scheduled with nightly, weekly, monthly and additional-cat
 *  rates, a tax class and a start time. The daycare card is the same pattern. A backdated start is
 *  refused by the server and shown in words. */
export function RateCardsAdmin() {
  const zone = useBranchTimeZone();
  const [unitType, setUnitType] = useState<BoardingUnitType>('Standard');
  const cards = useBoardingRateCards(unitType);
  const daycare = useDaycareRateCards();
  const schedule = useScheduleRateCard();
  const scheduleDaycare = useScheduleDaycareRateCard();
  const [dialog, setDialog] = useState<'boarding' | 'daycare' | null>(null);
  const currency = cards.data?.current?.nightlyRate.currencyCode ?? 'BDT';
  const money = (v: string): BoardingMoney => ({ amount: toMoneyText(v), currencyCode: currency });

  return (
    <div className="flex flex-col gap-8">
      <section aria-labelledby="rc-boarding" className="flex flex-col gap-4">
        <h2 id="rc-boarding" className="font-display text-lg font-semibold text-ink">
          Boarding rate cards
        </h2>
        <div className="flex flex-wrap items-end gap-3">
          <SelectRow
            id="rc-type"
            label="Unit type"
            value={unitType}
            onChange={(e) => setUnitType(e.target.value as BoardingUnitType)}
          >
            {Object.entries(UNIT_TYPE_LABEL).map(([k, v]) => (
              <option key={k} value={k}>
                {v}
              </option>
            ))}
          </SelectRow>
          <Button onClick={() => setDialog('boarding')}>Schedule a new rate card</Button>
        </div>
        <QueryGate query={cards} loadingLabel="Loading rate cards…">
          {(history) => (
            <History
              history={history}
              zone={zone}
              rows={(c) => [
                ['Nightly', c.nightlyRate],
                ['Weekly', c.weeklyRate],
                ['Monthly', c.monthlyRate],
                ['Extra cat/night', c.additionalCatNightlyRate],
              ]}
            />
          )}
        </QueryGate>
      </section>

      <section aria-labelledby="rc-daycare" className="flex flex-col gap-4">
        <h2 id="rc-daycare" className="font-display text-lg font-semibold text-ink">
          Daycare rate card
        </h2>
        <div>
          <Button variant="secondary" onClick={() => setDialog('daycare')}>
            Schedule a new daycare card
          </Button>
        </div>
        <QueryGate query={daycare} loadingLabel="Loading daycare rates…">
          {(history) => (
            <History
              history={history}
              zone={zone}
              rows={(c) => [
                ['Hourly', c.hourlyRate],
                ['Day', c.dayRate],
              ]}
            />
          )}
        </QueryGate>
      </section>

      {dialog === 'boarding' && (
        <ScheduleDialog
          title={`New ${UNIT_TYPE_LABEL[unitType]} rate card`}
          schema={rateCardSchema}
          fields={[
            { key: 'nightlyRate', label: 'Nightly rate' },
            { key: 'weeklyRate', label: 'Weekly rate (7 nights)' },
            { key: 'monthlyRate', label: 'Monthly rate (30 nights)' },
            { key: 'additionalCatNightlyRate', label: 'Extra cat, per night' },
          ]}
          onClose={() => setDialog(null)}
          onSubmit={(v) =>
            schedule.mutateAsync({
              unitType,
              nightlyRate: money(v.nightlyRate!),
              weeklyRate: money(v.weeklyRate!),
              monthlyRate: money(v.monthlyRate!),
              additionalCatNightlyRate: money(v.additionalCatNightlyRate!),
              taxClassId: v.taxClassId!,
              effectiveFrom: new Date(v.effectiveFrom!).toISOString(),
            })
          }
        />
      )}
      {dialog === 'daycare' && (
        <ScheduleDialog
          title="New daycare rate card"
          schema={daycareRateCardSchema}
          fields={[
            { key: 'hourlyRate', label: 'Hourly rate' },
            { key: 'dayRate', label: 'Day rate' },
          ]}
          onClose={() => setDialog(null)}
          onSubmit={(v) =>
            scheduleDaycare.mutateAsync({
              hourlyRate: money(v.hourlyRate!),
              dayRate: money(v.dayRate!),
              taxClassId: v.taxClassId!,
              effectiveFrom: new Date(v.effectiveFrom!).toISOString(),
            })
          }
        />
      )}
    </div>
  );
}
