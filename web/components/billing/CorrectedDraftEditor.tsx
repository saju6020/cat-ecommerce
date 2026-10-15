'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/use-toast';
import type { TaxClass } from '@/lib/api/admin-catalogue';
import type { CorrectedLineInput, InvoiceDetail } from '@/lib/api/billing-staff';
import { submitBillingForm } from '@/lib/api/errors';
import { MONEY_FORMAT_MESSAGE, MONEY_PATTERN, QUANTITY_PATTERN, a11yMoney, normaliseMoney } from '@/lib/money';
import { CARD_NUMBER_MESSAGE, looksLikeCardNumber } from '@/lib/validation/cardNumberGuard';
import { useIdempotencyKey } from '@/lib/hooks/billing/useIdempotencyKey';
import { Amount } from './Amount';
import { ComposerAlert } from './ComposerAlert';
import { FormRootError, MoneyField, SelectField, TextField } from './Fields';
import { Section } from './PageHeader';

/**
 * `components/billing/CorrectedDraftEditor.tsx` — HO-BIL-002 §11.3 (US-BIL-05 AC2). A draft created
 * from a credit note holds **copies of the original lines** (it has no charges): edit the description,
 * quantity, unit price, discount and tax class, save, and the server re-computes the draft (shown
 * below as an estimate), then issue it (UI-3 confirm, one `Idempotency-Key`). It is linked to the
 * original invoice and its credit note both ways by the server.
 */
const lineSchema = z.object({
  description: z.string().trim().min(1, 'Describe the line.').max(200)
    .refine((v) => !looksLikeCardNumber(v), { message: CARD_NUMBER_MESSAGE }),
  quantity: z.string().trim().regex(QUANTITY_PATTERN, 'Enter a quantity such as 2 or 1.5.'),
  unitPrice: z.string().trim().regex(MONEY_PATTERN, MONEY_FORMAT_MESSAGE),
  lineDiscount: z.string().trim().refine((v) => v === '' || MONEY_PATTERN.test(v), MONEY_FORMAT_MESSAGE),
  taxClassId: z.string().min(1, 'Choose a tax class.'),
});
const schema = z.object({ lines: z.array(lineSchema).min(1) });
type Values = z.infer<typeof schema>;

export function CorrectedDraftEditor({
  invoice,
  taxClasses,
  onSaveLines,
  onIssue,
  onReload,
}: {
  invoice: InvoiceDetail;
  taxClasses: TaxClass[] | undefined;
  onSaveLines: (input: { expectedVersion: number; lines: CorrectedLineInput[] }) => Promise<unknown>;
  onIssue: (input: { expectedVersion: number; idempotencyKey: string }) => Promise<{ invoiceId: string; number: string }>;
  onReload: () => Promise<unknown>;
}) {
  const router = useRouter();
  const { toast } = useToast();
  const issueKey = useIdempotencyKey();
  const [issueOpen, setIssueOpen] = useState(false);
  const [issueError, setIssueError] = useState<unknown>(null);
  const [leaving, setLeaving] = useState(false);
  const [target, setTarget] = useState<string | null>(null);
  useEffect(() => {
    if (target) router.push(target);
  }, [target, router]);
  const {
    register,
    control,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    mode: 'onChange',
    defaultValues: {
      lines: invoice.lines.map((l) => ({
        description: l.description,
        quantity: l.quantity,
        unitPrice: l.unitPrice.amount,
        lineDiscount: l.lineDiscount.amount,
        taxClassId: l.taxClassId,
      })),
    },
  });
  const { fields } = useFieldArray({ control, name: 'lines' });

  const save = handleSubmit(async (values) => {
    const ok = await submitBillingForm(
      async () => {
        await onSaveLines({
          expectedVersion: invoice.version,
          lines: values.lines.map((l) => ({
            description: l.description,
            quantity: l.quantity,
            unitPrice: normaliseMoney(l.unitPrice),
            lineDiscount: l.lineDiscount ? normaliseMoney(l.lineDiscount) : '0.00',
            taxClassId: l.taxClassId,
          })),
        });
        return true as const;
      },
      setError,
      { knownFields: ['lines'] },
    );
    if (ok) {
      reset(values);
      toast({ title: 'Corrected draft saved' });
    }
  });

  async function issue() {
    setIssueError(null);
    try {
      const result = await onIssue({ expectedVersion: invoice.version, idempotencyKey: issueKey.current() });
      issueKey.renew();
      toast({ title: `Issued as ${result.number}` });
      setLeaving(true);
      setTarget(`/staff/billing/invoices/${result.invoiceId}`);
    } catch (failure) {
      setIssueError(failure);
      await onReload();
    }
    setIssueOpen(false);
  }

  return (
    <div className="flex flex-col gap-5">
      {!leaving && <UnsavedChangesGuard when={isDirty} />}
      <ComposerAlert error={issueError} />
      <form onSubmit={save} noValidate className="flex flex-col gap-4">
        <FormRootError message={errors.root?.message} />
        <Section title="Corrected lines" description="Edit what changes; the server re-computes the tax and totals when you save.">
          {fields.map((field, index) => {
            const e = errors.lines?.[index];
            return (
              <fieldset key={field.id} className="flex min-w-0 flex-col gap-3 rounded-[var(--radius-md)] border border-border p-3">
                <legend className="px-1 text-sm font-semibold text-ink">Line {index + 1}</legend>
                <TextField id={`cd-desc-${index}`} label="Description" required error={e?.description?.message} {...register(`lines.${index}.description`)} />
                <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                  <TextField id={`cd-qty-${index}`} label="Quantity" inputMode="decimal" required error={e?.quantity?.message} {...register(`lines.${index}.quantity`)} />
                  <MoneyField id={`cd-price-${index}`} label="Unit price" required error={e?.unitPrice?.message} {...register(`lines.${index}.unitPrice`)} />
                  <MoneyField id={`cd-discount-${index}`} label="Discount" error={e?.lineDiscount?.message} {...register(`lines.${index}.lineDiscount`)} />
                  <SelectField id={`cd-tax-${index}`} label="Tax class" required error={e?.taxClassId?.message} {...register(`lines.${index}.taxClassId`)}>
                    {(taxClasses ?? []).map((t) => (
                      <option key={t.id} value={t.id}>
                        {t.code} — {t.name}
                      </option>
                    ))}
                    {!taxClasses && <option value={invoice.lines[index]?.taxClassId}>{invoice.lines[index]?.taxClassCode ?? 'Current class'}</option>}
                  </SelectField>
                </div>
              </fieldset>
            );
          })}
        </Section>
        <div>
          <Button type="submit" variant="secondary" disabled={isSubmitting || !isDirty} aria-busy={isSubmitting}>
            {isSubmitting ? 'Saving…' : 'Save corrected lines'}
          </Button>
        </div>
      </form>

      <Section title="Estimate">
        <p className="text-sm font-semibold text-warning-ink">
          Estimate at today&rsquo;s tax rates. Rates are fixed when the invoice is issued.
        </p>
        <div aria-live="polite" className="flex flex-col gap-1 text-sm">
          <span>
            Estimated total: <Amount money={invoice.totals.total} className="font-semibold" />
          </span>
          <span className="text-ink-muted">
            Net <Amount money={invoice.totals.net} /> · Tax <Amount money={invoice.totals.tax} />
          </span>
        </div>
        <div>
          <Button disabled={isDirty || isSubmitting} onClick={() => setIssueOpen(true)}>
            Issue corrected invoice
          </Button>
          {isDirty && <p className="mt-1 text-xs text-ink-muted">Save your changes before issuing.</p>}
        </div>
      </Section>

      <ConfirmDialog
        open={issueOpen}
        onOpenChange={setIssueOpen}
        title="Issue the corrected invoice"
        consequence={`Issue invoice for ${a11yMoney(invoice.totals.total)}? It gets the next number and can no longer be changed.`}
        confirmLabel="Issue invoice"
        pendingLabel="Issuing…"
        onConfirm={issue}
      />
    </div>
  );
}
