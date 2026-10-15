'use client';

import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { Button } from '@/components/ui/button';
import type { CodCollection, RecordRemittanceRequest } from '@/lib/api/billing-reconciliation';
import { submitBillingForm } from '@/lib/api/errors';
import type { Money } from '@/lib/format/money';
import { formatDateOnly } from '@/lib/format/time';
import { MONEY_FORMAT_MESSAGE, MONEY_PATTERN, moneySign, normaliseMoney } from '@/lib/money';
import { Amount } from './Amount';
import { FormRootError, MoneyField, SelectField, TextField } from './Fields';
import { guardedText } from './schemas';

/**
 * `components/billing/RecordRemittanceForm.tsx` — HO-BIL-002 §11.7 (US-BIL-06 AC1). Record what a
 * courier handed over: the courier, the date, the **total received**, the method and a reference, then
 * tick the collections it covers and enter the amount remitted for each (*Remit all* copies the
 * server's outstanding string). The server returns what is **unapplied** (received but not matched to
 * a collection); a collection paid short stays listed with its shortfall. The reference runs the C-5
 * guard. Nothing is added up here.
 */
const REMITTANCE_METHODS = [
  { value: 'Cash', label: 'Cash' },
  { value: 'BankTransfer', label: 'Bank transfer' },
];

const schema = z.object({
  courierCode: z.string().min(1, 'Choose the courier.'),
  receivedOn: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Enter the date received.'),
  amount: z.string().trim().min(1, 'Enter the total received.').regex(MONEY_PATTERN, MONEY_FORMAT_MESSAGE),
  method: z.string().min(1),
  reference: guardedText(100),
  lines: z.array(z.object({ paymentId: z.string(), selected: z.boolean(), amount: z.string().trim() })),
});
type Values = z.infer<typeof schema>;

export function RecordRemittanceForm({
  collections,
  today,
  defaultCourier,
  onSubmit,
}: {
  collections: CodCollection[];
  today: string;
  defaultCourier: string;
  onSubmit: (request: RecordRemittanceRequest) => Promise<{ remittanceId: string; unapplied: Money }>;
}) {
  const open = collections.filter((c) => moneySign(c.outstanding.amount) === 'positive');
  const couriers = [...new Set(open.map((c) => c.courierCode).filter((c): c is string => !!c))];
  const [result, setResult] = useState<{ unapplied: Money } | null>(null);

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    setError,
    reset,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    mode: 'onChange',
    defaultValues: { courierCode: defaultCourier, receivedOn: today, amount: '', method: 'Cash', reference: '', lines: [] },
  });
  const values = watch();
  const courier = values.courierCode || couriers[0] || '';
  const rows = open.filter((c) => c.courierCode === courier);

  useEffect(() => {
    reset({
      courierCode: courier,
      receivedOn: today,
      amount: '',
      method: 'Cash',
      reference: '',
      lines: open.filter((c) => c.courierCode === courier).map((c) => ({ paymentId: c.paymentId, selected: false, amount: '' })),
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [courier, collections.length, today]);

  const submit = handleSubmit(async (form) => {
    const chosen = form.lines.filter((l) => l.selected);
    if (chosen.length === 0) {
      setError('root', { message: 'Tick at least one collection this remittance covers.' });
      return;
    }
    let bad = false;
    form.lines.forEach((l, index) => {
      if (l.selected && !MONEY_PATTERN.test(l.amount)) {
        setError(`lines.${index}.amount`, { message: MONEY_FORMAT_MESSAGE });
        bad = true;
      }
    });
    if (bad) return;
    const done = await submitBillingForm(
      () =>
        onSubmit({
          courierCode: form.courierCode,
          receivedOn: form.receivedOn,
          amount: normaliseMoney(form.amount),
          method: form.method,
          reference: form.reference.trim() || undefined,
          lines: chosen.map((l) => ({ paymentId: l.paymentId, amountRemitted: normaliseMoney(l.amount) })),
        }),
      setError,
      { knownFields: ['courierCode', 'receivedOn', 'amount', 'method', 'reference'] },
    );
    if (done) {
      setResult({ unapplied: done.unapplied });
      reset({ ...form, amount: '', reference: '', lines: form.lines.map((l) => ({ ...l, selected: false, amount: '' })) });
    }
  });

  if (couriers.length === 0 && !result) {
    return <p className="text-sm text-ink-muted">Nothing is waiting to be remitted.</p>;
  }

  return (
    <form onSubmit={submit} noValidate className="flex max-w-3xl flex-col gap-4" aria-label="Record a remittance">
      <UnsavedChangesGuard when={isDirty && !isSubmitting} />
      {result && (
        <p role="status" className="rounded-[var(--radius-sm)] border border-border bg-accent-soft p-3 text-sm text-ink">
          Remittance recorded. Received but not applied to a collection: <Amount money={result.unapplied} className="font-semibold" />. Any collection
          that was paid short is listed with its shortfall above.
        </p>
      )}
      <FormRootError message={errors.root?.message} />
      <div className="grid gap-4 sm:grid-cols-2">
        <SelectField id="rem-courier" label="Courier" required error={errors.courierCode?.message} {...register('courierCode')}>
          {couriers.map((c) => (
            <option key={c} value={c}>{c}</option>
          ))}
        </SelectField>
        <TextField id="rem-date" label="Date received" type="date" required max={today} error={errors.receivedOn?.message} {...register('receivedOn')} />
        <MoneyField id="rem-amount" label="Total received" required error={errors.amount?.message} {...register('amount')} />
        <SelectField id="rem-method" label="Method" required error={errors.method?.message} {...register('method')}>
          {REMITTANCE_METHODS.map((m) => (
            <option key={m.value} value={m.value}>{m.label}</option>
          ))}
        </SelectField>
        <TextField id="rem-reference" label="Reference" autoComplete="off" error={errors.reference?.message} {...register('reference')} />
      </div>
      <fieldset className="flex min-w-0 flex-col gap-2 border-0 p-0">
        <legend className="mb-1 text-sm font-semibold text-ink">Collections covered</legend>
        <ul className="flex flex-col gap-2">
          {rows.map((c, index) => (
            <li key={c.paymentId} className="flex min-w-0 flex-col gap-2 rounded-[var(--radius-sm)] border border-border p-3">
              <label className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
                <input type="checkbox" className="h-4 w-4" {...register(`lines.${index}.selected`)} />
                <span className="font-semibold text-ink">{c.receiptNumber}</span>
                <span className="text-ink-muted">
                  {c.customerName} · {formatDateOnly(c.receivedOn)} · outstanding <Amount money={c.outstanding} />
                </span>
              </label>
              {values.lines?.[index]?.selected && (
                <div className="flex flex-wrap items-end gap-2">
                  <MoneyField id={`rem-line-${index}`} label={`Remitted for ${c.receiptNumber}`} wrapperClassName="w-56" error={errors.lines?.[index]?.amount?.message} {...register(`lines.${index}.amount`)} />
                  <Button variant="secondary" size="sm" onClick={() => setValue(`lines.${index}.amount`, c.outstanding.amount, { shouldDirty: true })}>
                    Remit all<span className="sr-only"> of {c.receiptNumber}</span>
                  </Button>
                </div>
              )}
            </li>
          ))}
        </ul>
      </fieldset>
      <div>
        <Button type="submit" disabled={isSubmitting} aria-busy={isSubmitting}>
          {isSubmitting ? 'Recording…' : 'Record remittance'}
        </Button>
      </div>
    </form>
  );
}
