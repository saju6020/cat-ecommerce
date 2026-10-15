'use client';

import { useEffect, useRef, useState } from 'react';
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
import type { IssueRefundRequest, PaymentMethod } from '@/lib/api/billing-staff';
import { submitBillingForm } from '@/lib/api/errors';
import type { Money } from '@/lib/format/money';
import { MONEY_FORMAT_MESSAGE, MONEY_PATTERN, normaliseMoney } from '@/lib/money';
import { useIdempotencyKey } from '@/lib/hooks/billing/useIdempotencyKey';
import { Amount } from './Amount';
import { FormRootError, MoneyField, SelectField, TextAreaField, TextField } from './Fields';
import { methodLabel } from './labels';
import { guardedText, requiredGuardedText } from './schemas';

/**
 * `components/billing/RefundDialog.tsx` — HO-BIL-002 §11.4 (FR-BIL-5, FR-ORD-15). Pay money back from
 * an accepted payment's unallocated amount, optionally linked to a credit note. The method list is the
 * payment method's `allowedRefundMethods` (server data); a reference is required when the server's
 * flags say that **refund method** needs one (transfers). Reference and reason run the C-5 guard. The
 * server refuses an over-large amount with a message naming what is available. One idempotency key
 * per open dialog, renewed after a success.
 */
const schema = z.object({
  method: z.string().min(1, 'Choose how to pay the money back.'),
  amount: z.string().trim().min(1, 'Enter the amount.').regex(MONEY_PATTERN, MONEY_FORMAT_MESSAGE),
  reference: guardedText(100),
  reason: requiredGuardedText(500, 'Enter a reason for the refund.'),
});
type Values = z.infer<typeof schema>;

export function RefundDialog({
  open,
  onOpenChange,
  paymentId,
  allowedMethods,
  methods,
  available,
  creditNoteId,
  defaultAmount,
  onRefund,
  onDone,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  paymentId: string;
  allowedMethods: string[];
  methods: PaymentMethod[];
  available: Money;
  creditNoteId?: string;
  defaultAmount?: string;
  onRefund: (input: { request: IssueRefundRequest; idempotencyKey: string }) => Promise<{ refundId: string }>;
  onDone?: () => void;
}) {
  const returnFocusRef = useDialogReturnFocus(open);
  const key = useIdempotencyKey();
  const [done, setDone] = useState<{ amount: string; method: string } | null>(null);
  // The request the current idempotency key belongs to: a different refund never reuses a key.
  const attempted = useRef<string | null>(null);
  const {
    register,
    handleSubmit,
    watch,
    setError,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    mode: 'onChange',
    defaultValues: { method: allowedMethods[0] ?? '', amount: defaultAmount ?? '', reference: '', reason: '' },
  });
  useEffect(() => {
    if (open) {
      key.renew(); // every opening is a new refund
      attempted.current = null;
      reset({ method: allowedMethods[0] ?? '', amount: defaultAmount ?? '', reference: '', reason: '' });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);
  const chosen = watch('method');
  const needsReference = methods.find((m) => m.name === chosen)?.requiresReference ?? false;

  const submit = handleSubmit(async (values) => {
    if (needsReference && !values.reference.trim()) {
      setError('reference', { message: 'Enter the transfer reference.' });
      return;
    }
    const request = {
      paymentId,
      creditNoteId,
      method: values.method,
      amount: normaliseMoney(values.amount),
      reference: values.reference.trim() || undefined,
      reason: values.reason.trim(),
    };
    // A retry of the SAME request (lost response) keeps its key; a changed request gets a new one.
    const signature = JSON.stringify(request);
    if (attempted.current !== null && attempted.current !== signature) key.renew();
    attempted.current = signature;
    const ok = await submitBillingForm(
      async () => {
        await onRefund({ request, idempotencyKey: key.current() });
        return true as const;
      },
      setError,
      { knownFields: ['method', 'amount', 'reference', 'reason'] },
    );
    if (ok) {
      key.renew();
      attempted.current = null;
      setDone({ amount: normaliseMoney(values.amount), method: values.method });
      onDone?.();
    }
  });

  function close() {
    key.renew();
    attempted.current = null;
    setDone(null);
    reset({ method: allowedMethods[0] ?? '', amount: defaultAmount ?? '', reference: '', reason: '' });
    onOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={(next) => (!next ? close() : onOpenChange(next))}>
      <DialogContent returnFocusRef={returnFocusRef} className="max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Refund</DialogTitle>
          <DialogDescription>
            Pays money back from this payment. Up to <Amount money={available} /> is available to refund.
            A refund is recorded with who made it and why; it cannot be edited.
          </DialogDescription>
        </DialogHeader>
        {done !== null ? (
          <div role="status" className="flex flex-col gap-3 text-sm">
            <p className="font-semibold text-ink">Refund recorded.</p>
            <p>
              Paid back: <Amount money={{ amount: done.amount, currencyCode: available.currencyCode }} className="font-semibold" /> by{' '}
              {methodLabel(done.method).toLowerCase()}.
            </p>
            <DialogFooter>
              <Button onClick={close}>Done</Button>
            </DialogFooter>
          </div>
        ) : (
          <>
            <form id="refund-form" onSubmit={submit} noValidate className="flex flex-col gap-4">
              <FormRootError message={errors.root?.message} />
              <SelectField id="refund-method" label="Refund method" required error={errors.method?.message} {...register('method')}>
                {allowedMethods.map((m) => (
                  <option key={m} value={m}>
                    {methodLabel(m)}
                  </option>
                ))}
              </SelectField>
              <MoneyField id="refund-amount" label="Amount" required error={errors.amount?.message} {...register('amount')} />
              <TextField
                id="refund-reference"
                label={needsReference ? 'Transfer reference' : 'Reference (optional)'}
                required={needsReference}
                autoComplete="off"
                error={errors.reference?.message}
                {...register('reference')}
              />
              <TextAreaField id="refund-reason" label="Reason" required error={errors.reason?.message} {...register('reason')} />
            </form>
            <DialogFooter>
              <Button variant="secondary" onClick={close} disabled={isSubmitting}>
                Cancel
              </Button>
              <Button type="submit" form="refund-form" disabled={isSubmitting} aria-busy={isSubmitting}>
                {isSubmitting ? 'Refunding…' : 'Refund'}
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
