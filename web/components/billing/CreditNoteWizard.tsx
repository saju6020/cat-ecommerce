'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/use-toast';
import {
  CREDIT_NOTE_KINDS,
  type CreditNoteKind,
  type InvoiceDetail,
  type IssuedCreditNote,
} from '@/lib/api/billing-staff';
import { describeBillingError, submitBillingForm } from '@/lib/api/errors';
import { MONEY_FORMAT_MESSAGE, MONEY_PATTERN, QUANTITY_PATTERN, a11yMoney } from '@/lib/money';
import { useIdempotencyKey } from '@/lib/hooks/billing/useIdempotencyKey';
import { useCreateCorrectedDraft, useIssueCreditNote, useIssueRefund } from '@/lib/hooks/billing/useBillingMutations';
import { Amount } from './Amount';
import { CreditNoteResult } from './CreditNoteResult';
import { FormRootError, SelectField, TextAreaField, TextField } from './Fields';
import { Section } from './PageHeader';
import { requiredGuardedText } from './schemas';

/**
 * `components/billing/CreditNoteWizard.tsx` — HO-BIL-002 §11.3 (US-BIL-05 AC2), ⭐. The only way to
 * correct an issued invoice (ADR-FE-083). Step 1: kind and reason (required, 1000). Step 2: credit the
 * **whole invoice** or **chosen lines** — per line a quantity or an amount, with the line's own
 * quantity and gross as the hint (the server caps against what remains and says so). Step 3: review,
 * then a UI-3 confirm; the result shows the credit note and what was released to customer credit.
 * Rates are the invoice's frozen ones — nothing is computed here.
 */
const lineSchema = z.object({
  selected: z.boolean(),
  mode: z.enum(['quantity', 'amount']),
  value: z.string().trim(),
});
const schema = z.object({
  kind: z.enum(['Correction', 'Return', 'Cancellation', 'Goodwill']),
  reason: requiredGuardedText(1000, 'Enter the reason for the credit note.'),
  scope: z.enum(['full', 'lines']),
  lines: z.array(lineSchema),
});
type Values = z.infer<typeof schema>;
type Step = 1 | 2 | 3;

export function CreditNoteWizard({ invoice }: { invoice: InvoiceDetail }) {
  const router = useRouter();
  const { toast } = useToast();
  const key = useIdempotencyKey();
  const issue = useIssueCreditNote(invoice.invoiceId);
  const refund = useIssueRefund();
  const corrected = useCreateCorrectedDraft(invoice.invoiceId);
  const [step, setStep] = useState<Step>(1);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [issued, setIssued] = useState<IssuedCreditNote | null>(null);
  const [leaving, setLeaving] = useState(false);

  const {
    register,
    control,
    handleSubmit,
    watch,
    trigger,
    setError,
    formState: { errors, isDirty, isSubmitting },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    mode: 'onChange',
    defaultValues: {
      kind: 'Correction',
      reason: '',
      scope: 'full',
      lines: invoice.lines.map(() => ({ selected: false, mode: 'quantity' as const, value: '' })),
    },
  });
  const { fields } = useFieldArray({ control, name: 'lines' });
  const values = watch();

  async function next() {
    if (step === 1 && (await trigger(['kind', 'reason']))) setStep(2);
    else if (step === 2) {
      if (values.scope === 'lines') {
        const chosen = values.lines.filter((l) => l.selected);
        if (chosen.length === 0) {
          setError('root', { message: 'Choose at least one line to credit, or credit the whole invoice.' });
          return;
        }
        const bad = chosen.some((l) => !(l.mode === 'quantity' ? QUANTITY_PATTERN : MONEY_PATTERN).test(l.value));
        if (bad) {
          setError('root', { message: `Enter a quantity or an amount for every ticked line. ${MONEY_FORMAT_MESSAGE}` });
          return;
        }
      }
      setStep(3);
    }
  }

  const submit = handleSubmit(async (form) => {
    const request = {
      kind: form.kind as CreditNoteKind,
      reason: form.reason.trim(),
      fullCredit: form.scope === 'full',
      lines:
        form.scope === 'lines'
          ? form.lines.flatMap((l, index) =>
              l.selected
                ? [{ invoiceLineId: invoice.lines[index]!.lineId, [l.mode]: l.value }]
                : [],
            )
          : undefined,
    };
    const done = await submitBillingForm(
      () => issue.mutateAsync({ request, idempotencyKey: key.current() }),
      setError,
      { knownFields: ['kind', 'reason'] },
    );
    if (done) {
      key.renew();
      setLeaving(true);
      setIssued(done);
    }
  });

  if (issued) {
    return (
      <CreditNoteResult
        invoice={invoice}
        issued={issued}
        onRefund={(input) => refund.mutateAsync(input)}
        reissuing={corrected.isPending}
        onCorrectedReissue={async () => {
          try {
            const created = await corrected.mutateAsync(issued.creditNoteId);
            router.push(`/staff/billing/invoices/${created.invoiceId}`);
          } catch (failure) {
            toast({ title: describeBillingError(failure) });
          }
        }}
      />
    );
  }

  const kindLabel = values.kind;
  return (
    <div className="flex max-w-3xl flex-col gap-5">
      <UnsavedChangesGuard when={isDirty && !leaving} />
      <ol aria-label="Steps" className="flex flex-wrap gap-2 text-sm">
        {['Kind and reason', 'What to credit', 'Review'].map((label, index) => (
          <li
            key={label}
            aria-current={step === index + 1 ? 'step' : undefined}
            className={step === index + 1 ? 'font-semibold text-accent-ink' : 'text-ink-muted'}
          >
            {index + 1}. {label}
          </li>
        ))}
      </ol>
      <FormRootError message={errors.root?.message} />

      {step === 1 && (
        <Section title="Kind and reason" description="A credit note corrects an issued invoice; the invoice itself never changes.">
          <SelectField id="cn-kind" label="Kind" required error={errors.kind?.message} {...register('kind')}>
            {CREDIT_NOTE_KINDS.map((k) => (
              <option key={k} value={k}>{k}</option>
            ))}
          </SelectField>
          <TextAreaField id="cn-reason" label="Reason" required hint="Up to 1000 characters." error={errors.reason?.message} {...register('reason')} />
        </Section>
      )}

      {step === 2 && (
        <Section title="What to credit" description={`Invoice ${invoice.number ?? ''}: total ${a11yMoney(invoice.totals.total)}, outstanding ${a11yMoney(invoice.outstanding)}.`}>
          <fieldset className="flex flex-col gap-2 border-0 p-0">
            <legend className="mb-1 text-sm font-semibold text-ink">Scope</legend>
            <label className="inline-flex items-center gap-2 text-sm">
              <input type="radio" value="full" {...register('scope')} />
              The whole invoice
            </label>
            <label className="inline-flex items-center gap-2 text-sm">
              <input type="radio" value="lines" {...register('scope')} />
              Chosen lines only
            </label>
          </fieldset>
          {values.scope === 'lines' && (
            <ul className="flex flex-col gap-3">
              {fields.map((field, index) => {
                const line = invoice.lines[index]!;
                const selected = values.lines[index]?.selected;
                const mode = values.lines[index]?.mode ?? 'quantity';
                return (
                  <li key={field.id} className="flex min-w-0 flex-col gap-2 rounded-[var(--radius-sm)] border border-border p-3">
                    <label className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
                      <input type="checkbox" className="h-4 w-4" {...register(`lines.${index}.selected`)} />
                      <span className="font-semibold text-ink [overflow-wrap:anywhere]">{line.description}</span>
                      <span className="text-ink-muted">
                        quantity {line.quantity}, gross <Amount money={line.grossAmount} /> at {line.taxRatePercent}%
                      </span>
                    </label>
                    {selected && (
                      <div className="flex flex-wrap items-end gap-3">
                        <fieldset className="flex gap-3 border-0 p-0 text-sm">
                          <legend className="sr-only">Credit {line.description} by</legend>
                          <label className="inline-flex items-center gap-1"><input type="radio" value="quantity" {...register(`lines.${index}.mode`)} />Quantity</label>
                          <label className="inline-flex items-center gap-1"><input type="radio" value="amount" {...register(`lines.${index}.mode`)} />Amount</label>
                        </fieldset>
                        <TextField
                          id={`cn-line-${index}`}
                          label={mode === 'quantity' ? `Quantity to credit (up to ${line.quantity})` : `Amount to credit (${line.grossAmount.currencyCode})`}
                          inputMode="decimal"
                          autoComplete="off"
                          wrapperClassName="w-56"
                          {...register(`lines.${index}.value`)}
                        />
                      </div>
                    )}
                  </li>
                );
              })}
            </ul>
          )}
        </Section>
      )}

      {step === 3 && (
        <Section title="Review" description="Check this before you issue it. A credit note cannot be changed afterwards.">
          <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
            <dt className="text-ink-muted">Invoice</dt>
            <dd>{invoice.number}</dd>
            <dt className="text-ink-muted">Kind</dt>
            <dd>{kindLabel}</dd>
            <dt className="text-ink-muted">Reason</dt>
            <dd className="[overflow-wrap:anywhere]">{values.reason}</dd>
            <dt className="text-ink-muted">Credits</dt>
            <dd>
              {values.scope === 'full' ? (
                'The whole invoice (everything not yet credited)'
              ) : (
                <ul>
                  {values.lines.flatMap((l, index) =>
                    l.selected ? [<li key={index}>{invoice.lines[index]!.description} — {l.mode} {l.value}</li>] : [],
                  )}
                </ul>
              )}
            </dd>
          </dl>
        </Section>
      )}

      <div className="flex flex-wrap gap-2">
        {step > 1 && (
          <Button variant="secondary" onClick={() => setStep((step - 1) as Step)} disabled={isSubmitting}>
            Back
          </Button>
        )}
        {step < 3 ? (
          <Button onClick={() => void next()}>Continue</Button>
        ) : (
          <Button disabled={isSubmitting} onClick={() => setConfirmOpen(true)}>
            Issue credit note
          </Button>
        )}
      </div>

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Issue this credit note"
        variant="destructive"
        consequence={`Issue a ${kindLabel.toLowerCase()} credit note on ${invoice.number ?? 'this invoice'}${values.scope === 'full' ? ' for everything not yet credited' : ` for ${values.lines.filter((l) => l.selected).length} line(s)`}? It gets the next credit note number and cannot be changed. If the invoice was paid, the credit is released to the customer.`}
        confirmLabel="Issue credit note"
        pendingLabel="Issuing…"
        onConfirm={async () => {
          await submit();
          setConfirmOpen(false);
        }}
      />
    </div>
  );
}
