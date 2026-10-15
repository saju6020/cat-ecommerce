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
import type { AllocateResult, InvoiceSummary } from '@/lib/api/billing-staff';
import { submitBillingForm } from '@/lib/api/errors';
import type { Money } from '@/lib/format/money';
import { MONEY_FORMAT_MESSAGE, MONEY_PATTERN, a11yMoney, normaliseMoney } from '@/lib/money';
import { Amount } from './Amount';
import { FormRootError, MoneyField, SelectField } from './Fields';

/**
 * `components/billing/AllocatePaymentDialog.tsx` — HO-BIL-002 §11.4 (US-BIL-03, US-BIL-04). Put the
 * unallocated part of an accepted payment towards an invoice. The server caps the amount and answers
 * with a message naming what is available; the result shows each invoice's new outstanding and what
 * is still unallocated — the server's figures.
 */
const schema = z.object({
  invoiceId: z.string().min(1, 'Choose an invoice.'),
  amount: z.string().trim().min(1, 'Enter the amount.').regex(MONEY_PATTERN, MONEY_FORMAT_MESSAGE),
});
type Values = z.infer<typeof schema>;

export function AllocatePaymentDialog({
  open,
  onOpenChange,
  invoices,
  unallocated,
  onAllocate,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  invoices: InvoiceSummary[];
  unallocated: Money;
  onAllocate: (allocations: Array<{ invoiceId: string; amount: string }>) => Promise<AllocateResult>;
}) {
  const returnFocusRef = useDialogReturnFocus(open);
  const [result, setResult] = useState<AllocateResult | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    setValue,
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
      () => onAllocate([{ invoiceId: values.invoiceId, amount: normaliseMoney(values.amount) }]),
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
          <DialogTitle>Allocate this payment</DialogTitle>
          <DialogDescription>
            <Amount money={unallocated} /> of this payment is not yet applied to an invoice.
          </DialogDescription>
        </DialogHeader>
        {result ? (
          <div className="flex flex-col gap-3" role="status">
            <ul className="flex flex-col gap-1 text-sm">
              {result.applied.map((a) => (
                <li key={`${a.invoiceId}-${a.amount.amount}`}>
                  Applied <Amount money={a.amount} className="font-semibold" /> to {a.number}; outstanding now{' '}
                  <Amount money={a.outstanding} />.
                </li>
              ))}
            </ul>
            <p className="text-sm font-semibold">
              Still unallocated: <Amount money={result.unallocated} />
            </p>
            <DialogFooter>
              <Button onClick={close}>Done</Button>
            </DialogFooter>
          </div>
        ) : (
          <>
            <form id="allocate-form" onSubmit={submit} noValidate className="flex flex-col gap-4">
              <FormRootError message={errors.root?.message} />
              <SelectField id="alloc-invoice" label="Invoice" required error={errors.invoiceId?.message} {...register('invoiceId')}>
                {invoices.map((invoice) => (
                  <option key={invoice.invoiceId} value={invoice.invoiceId}>
                    {invoice.number} — outstanding {a11yMoney(invoice.outstanding)}
                  </option>
                ))}
              </SelectField>
              <div className="flex flex-wrap items-end gap-2">
                <MoneyField id="alloc-amount" label="Amount" required error={errors.amount?.message} wrapperClassName="flex-1" {...register('amount')} />
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => setValue('amount', unallocated.amount, { shouldValidate: true })}
                >
                  Use the unallocated amount
                </Button>
              </div>
            </form>
            <DialogFooter>
              <Button variant="secondary" onClick={close} disabled={isSubmitting}>
                Cancel
              </Button>
              <Button type="submit" form="allocate-form" disabled={isSubmitting || invoices.length === 0} aria-busy={isSubmitting}>
                {isSubmitting ? 'Allocating…' : 'Allocate'}
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
