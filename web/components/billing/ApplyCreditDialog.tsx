'use client';

import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  useDialogReturnFocus,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import type { ApplyCreditResult, InvoiceSummary } from '@/lib/api/billing-staff';
import { submitBillingForm } from '@/lib/api/errors';
import type { Money } from '@/lib/format/money';
import { MONEY_FORMAT_MESSAGE, MONEY_PATTERN, a11yMoney, normaliseMoney } from '@/lib/money';
import { Amount } from './Amount';
import { FormRootError, MoneyField, SelectField } from './Fields';

/**
 * `components/billing/ApplyCreditDialog.tsx` — HO-BIL-002 §11.1 (US-BIL-08). Pick an invoice with
 * something outstanding and (optionally) an amount — empty means *"As much as possible"*; the server
 * caps it and, if it is too much, answers with a message naming what can be applied. The result lists
 * what was applied and the **free credit that remains** — both the server's figures.
 */
const schema = z.object({
  invoiceId: z.string().min(1, 'Choose an invoice.'),
  amount: z
    .string()
    .trim()
    .refine((value) => value === '' || MONEY_PATTERN.test(value), MONEY_FORMAT_MESSAGE),
});
type Values = z.infer<typeof schema>;

export function ApplyCreditDialog({
  open,
  onOpenChange,
  invoices,
  freeCredit,
  onApply,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  invoices: InvoiceSummary[];
  freeCredit: Money;
  onApply: (input: { invoiceId: string; amount?: string }) => Promise<ApplyCreditResult>;
}) {
  const returnFocusRef = useDialogReturnFocus(open);
  const [result, setResult] = useState<ApplyCreditResult | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { invoiceId: invoices[0]?.invoiceId ?? '', amount: '' },
  });

  useEffect(() => {
    if (open) reset({ invoiceId: invoices[0]?.invoiceId ?? '', amount: '' });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const submit = handleSubmit(async (values) => {
    const done = await submitBillingForm(
      () =>
        onApply({
          invoiceId: values.invoiceId,
          amount: values.amount ? normaliseMoney(values.amount) : undefined,
        }),
      setError,
      { knownFields: ['invoiceId', 'amount'] },
    );
    if (done) setResult(done);
  });

  function close() {
    setResult(null);
    reset({ invoiceId: invoices[0]?.invoiceId ?? '', amount: '' });
    onOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={(next) => (!next ? close() : onOpenChange(next))}>
      <DialogContent returnFocusRef={returnFocusRef} className="max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Apply customer credit</DialogTitle>
          <DialogDescription>
            Uses the customer’s free credit (<Amount money={freeCredit} />) against one invoice,
            oldest payment first. Deposits set aside for an order or a stay are not touched.
          </DialogDescription>
        </DialogHeader>
        {result ? (
          <div className="flex flex-col gap-3" role="status">
            <ul className="flex flex-col gap-1 text-sm">
              {result.applied.map((a) => (
                <li key={`${a.invoiceId}-${a.amount.amount}`}>
                  Applied <Amount money={a.amount} className="font-semibold" /> to{' '}
                  {a.number ?? 'the invoice'}.
                </li>
              ))}
            </ul>
            <p className="text-sm">
              Invoice outstanding now: <Amount money={result.invoiceOutstanding} className="font-semibold" />
            </p>
            <p className="text-sm font-semibold">
              Free credit remaining: <Amount money={result.freeCreditRemaining} />
            </p>
            <DialogFooter>
              <Button onClick={close}>Done</Button>
            </DialogFooter>
          </div>
        ) : (
          <>
            <form id="apply-credit-form" onSubmit={submit} noValidate className="flex flex-col gap-4">
              <FormRootError message={errors.root?.message} />
              <SelectField id="apply-credit-invoice" label="Invoice" required error={errors.invoiceId?.message} {...register('invoiceId')}>
                {invoices.map((invoice) => (
                  <option key={invoice.invoiceId} value={invoice.invoiceId}>
                    {invoice.number ?? 'Draft'} — outstanding {a11yMoney(invoice.outstanding)}
                  </option>
                ))}
              </SelectField>
              <MoneyField
                id="apply-credit-amount"
                label="Amount"
                hint="Leave empty to apply as much as possible."
                error={errors.amount?.message}
                {...register('amount')}
              />
            </form>
            <DialogFooter>
              <Button variant="secondary" onClick={close} disabled={isSubmitting}>
                Cancel
              </Button>
              <Button type="submit" form="apply-credit-form" disabled={isSubmitting || invoices.length === 0} aria-busy={isSubmitting}>
                {isSubmitting ? 'Applying…' : 'Apply credit'}
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
