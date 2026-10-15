'use client';

import { useEffect, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { Button } from '@/components/ui/button';
import type { PaymentRecordResult, RecordPaymentRequest } from '@/lib/api/billing-staff';
import { submitBillingForm } from '@/lib/api/errors';
import { MONEY_FORMAT_MESSAGE, MONEY_PATTERN, normaliseMoney } from '@/lib/money';
import { todayInZone } from '@/lib/format/time';
import { useActiveBranchZone } from '@/lib/hooks/billing/useActiveBranchZone';
import { useIdempotencyKey } from '@/lib/hooks/billing/useIdempotencyKey';
import { useCustomerInvoices, usePaymentMethods } from '@/lib/hooks/billing/useBillingQueries';
import { useRecordPayment } from '@/lib/hooks/billing/useBillingMutations';
import { useStaffSession } from '@/lib/hooks/useSession';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { AllocationRows } from './AllocationRows';
import { FormRootError, MoneyField, TextAreaField, TextField } from './Fields';
import { MethodPicker, PaymentMethodFields } from './PaymentMethodFields';
import { PaymentResultPanel } from './PaymentResultPanel';
import { PAYMENT_KNOWN_FIELDS, paymentSchema, type PaymentFormValues } from './recordPaymentSchema';
import { Amount } from './Amount';

/**
 * `components/billing/RecordPaymentForm.tsx` — HO-BIL-002 §11.4 (US-BIL-11, 04, 03), ⭐. Public props
 * `{ customerId, invoiceIds?, earmark?, onRecorded? }` so HO-ORD-002 (POS) and HO-BRD-002 (deposits)
 * reuse it. Method-driven (ADR-FE-082): the reference and collector fields come from the server's
 * flags and online methods are never offered; the reference runs the C-5 guard (ADR-FE-084); every
 * submit carries one idempotency key for the form's life, renewed only after a success (ADR-FE-081);
 * split tender is a loop with the server's new outstanding pre-filled (ADR-FE-086). The browser
 * computes no money: caps and validation are the server's, and its messages appear under the fields.
 */
export interface RecordPaymentFormProps {
  customerId: string;
  invoiceIds?: string[];
  earmark?: { kind: 'Order' | 'BoardingStay'; sourceId: string; label: string };
  onRecorded?: (result: PaymentRecordResult) => void;
}

export function RecordPaymentForm({ customerId, invoiceIds = [], earmark, onRecorded }: RecordPaymentFormProps) {
  const methods = usePaymentMethods();
  const invoices = useCustomerInvoices(customerId);
  const record = useRecordPayment();
  const key = useIdempotencyKey();
  const { zone, ready: zoneReady } = useActiveBranchZone();
  const { data: session } = useStaffSession();
  const today = todayInZone(zone);

  const open = (invoices.data?.items ?? []).filter((i) => i.status === 'Issued' || i.status === 'PartPaid');
  const offered = (methods.data ?? []).filter((m) => !m.isOnline);

  const [result, setResult] = useState<PaymentRecordResult | null>(null);
  const [history, setHistory] = useState<PaymentRecordResult[]>([]);
  const seeded = useRef(false);
  const [formReady, setFormReady] = useState(false);
  // Allocations are keyed by INVOICE ID, never by position: the rows render from the live (refetched)
  // invoice list, so what a row shows and what it submits can never diverge.
  const [alloc, setAlloc] = useState<Record<string, { selected: boolean; amount: string }>>({});
  const [rowErrors, setRowErrors] = useState<Record<string, string>>({});

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    setError,
    reset,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<PaymentFormValues>({
    resolver: zodResolver(paymentSchema),
    mode: 'onChange',
    defaultValues: {
      method: '',
      amount: '',
      receivedOn: '',
      externalReference: '',
      collectorKind: 'Courier',
      courierCode: '',
      courierName: '',
      notes: '',
    },
  });
  const values = watch();
  const method = offered.find((m) => m.name === values.method);

  useEffect(() => {
    if (seeded.current || !invoices.data || !methods.data || !zoneReady) return;
    seeded.current = true;
    setFormReady(true);
    const singleIndex = invoiceIds.length === 1 ? open.findIndex((i) => i.invoiceId === invoiceIds[0]) : -1;
    const single = singleIndex >= 0 ? open[singleIndex]!.outstanding.amount : '';
    reset({
      method: offered[0]?.name ?? '',
      amount: single,
      receivedOn: today,
      externalReference: '',
      collectorKind: 'Courier',
      courierCode: '',
      courierName: '',
      notes: '',
    });
    if (singleIndex >= 0) setAlloc({ [open[singleIndex]!.invoiceId]: { selected: true, amount: single } });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [invoices.data, methods.data, zoneReady]);

  const submit = handleSubmit(async (form) => {
    if (!method) return;
    if (method.requiresReference && !form.externalReference.trim()) {
      setError('externalReference', { message: `Enter the ${method.referenceLabel.toLowerCase()}.` });
      return;
    }
    if (method.requiresCollector && form.collectorKind === 'Courier') {
      let missing = false;
      if (!form.courierCode.trim()) { setError('courierCode', { message: 'Enter the courier code.' }); missing = true; }
      if (!form.courierName.trim()) { setError('courierName', { message: 'Enter the courier name.' }); missing = true; }
      if (missing) return;
    }
    // Built from the LIVE list, looked up by invoice id.
    const chosen = open.flatMap((i) =>
      alloc[i.invoiceId]?.selected ? [{ invoiceId: i.invoiceId, amount: alloc[i.invoiceId]!.amount }] : [],
    );
    const bad: Record<string, string> = {};
    for (const row of chosen) {
      if (!MONEY_PATTERN.test(row.amount)) bad[row.invoiceId] = MONEY_FORMAT_MESSAGE;
    }
    setRowErrors(bad);
    if (Object.keys(bad).length > 0) return;

    const request: RecordPaymentRequest = {
      customerId,
      method: form.method,
      amount: normaliseMoney(form.amount),
      receivedOn: form.receivedOn,
      externalReference: method.requiresReference || form.externalReference ? form.externalReference.trim() || undefined : undefined,
      collector: method.requiresCollector
        ? form.collectorKind === 'Staff'
          ? { kind: 'Staff', staffAccountId: session?.staffAccountId }
          : { kind: 'Courier', courierCode: form.courierCode.trim(), courierName: form.courierName.trim() }
        : undefined,
      earmark,
      allocations: chosen.map((row) => ({ invoiceId: row.invoiceId, amount: normaliseMoney(row.amount) })),
      notes: form.notes.trim() || undefined,
    };
    const done = await submitBillingForm(
      () => record.mutateAsync({ request, idempotencyKey: key.current() }),
      setError,
      { knownFields: PAYMENT_KNOWN_FIELDS },
    );
    if (done) {
      key.renew();
      setResult(done);
      setHistory((current) => [...current, done]);
      onRecorded?.(done);
      setAlloc({});
      reset({ ...form, amount: '', externalReference: '', notes: '' });
    }
  });

  function takeAnother(outstanding: string, invoiceId: string) {
    setResult(null);
    setValue('amount', outstanding, { shouldDirty: true });
    setAlloc((current) => ({ ...current, [invoiceId]: { selected: true, amount: outstanding } }));
  }

  if (methods.isError || invoices.isError) {
    return <ErrorState onRetry={() => { void methods.refetch(); void invoices.refetch(); }} />;
  }
  if (methods.isLoading || invoices.isLoading || !formReady) {
    return <LoadingState variant="card" rows={6} label="Loading the payment form…" />;
  }

  const canLoop = invoiceIds.length === 1 || history.some((h) => h.invoiceOutstanding.length === 1);

  return (
    <div className="flex max-w-2xl flex-col gap-5">
      <UnsavedChangesGuard when={isDirty && !isSubmitting} />
      {history.length > 0 && (
        <section aria-label="Payments recorded so far" className="flex flex-col gap-1 text-sm">
          <h2 className="font-semibold text-ink">Recorded so far</h2>
          <ul className="flex flex-col gap-1">
            {history.map((h) => (
              <li key={h.paymentId}>
                {h.receiptNumber ?? 'Awaiting approval'}
                {h.applied.map((a) => (
                  <span key={a.invoiceId}> <Amount money={a.amount} /> to {a.number}</span>
                ))}
              </li>
            ))}
          </ul>
        </section>
      )}
      {result && <PaymentResultPanel result={result} canLoop={canLoop} onTakeAnother={takeAnother} />}
      {result && (
        <div>
          <Button variant="ghost" onClick={() => { setResult(null); setHistory([]); }}>
            Record a different payment
          </Button>
        </div>
      )}
      {!result && (
        <form onSubmit={submit} noValidate className="flex flex-col gap-4" aria-label="Record a payment">
          <FormRootError message={errors.root?.message} />
          <MethodPicker
            methods={offered}
            value={values.method}
            onChange={(name) => setValue('method', name, { shouldDirty: true })}
            disabled={isSubmitting}
          />
          {earmark && (
            <p className="rounded-[var(--radius-sm)] border border-border bg-surface-sunken p-3 text-sm text-ink">
              Set aside as a deposit for: <strong>{earmark.label}</strong>
            </p>
          )}
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="flex min-w-0 flex-col gap-2">
              <MoneyField id="pay-amount" label="Amount" required error={errors.amount?.message} {...register('amount')} />
            </div>
            <TextField id="pay-date" label="Date received" type="date" required max={today} error={errors.receivedOn?.message} {...register('receivedOn')} />
          </div>
          <PaymentMethodFields method={method} register={register} errors={errors} staffName={session?.fullName} collectorKind={values.collectorKind} />
          <AllocationRows
            invoices={open}
            alloc={alloc}
            rowErrors={rowErrors}
            onChange={(invoiceId, patch) =>
              setAlloc((current) => ({ ...current, [invoiceId]: { selected: false, amount: '', ...current[invoiceId], ...patch } }))
            }
            onPayTheRest={(invoiceId, outstanding) => {
              setAlloc((current) => ({ ...current, [invoiceId]: { selected: true, amount: outstanding } }));
              const others = open.filter((i) => i.invoiceId !== invoiceId && alloc[i.invoiceId]?.selected);
              if (others.length === 0) setValue('amount', outstanding, { shouldDirty: true });
            }}
          />
          <TextAreaField id="pay-notes" label="Notes" error={errors.notes?.message} {...register('notes')} />
          <div className="flex flex-wrap items-center gap-3">
            <Button type="submit" disabled={isSubmitting || !method} aria-busy={isSubmitting}>
              {isSubmitting ? 'Recording…' : 'Record payment'}
            </Button>
          </div>
        </form>
      )}
    </div>
  );
}
