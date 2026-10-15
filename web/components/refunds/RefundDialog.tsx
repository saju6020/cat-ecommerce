'use client';

import { useEffect, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { Amount } from '@/components/billing/Amount';
import { MoneyField, SelectField, TextAreaField, TextField } from '@/components/billing/Fields';
import { OrderDialog } from '@/components/orders/OrderDialog';
import { ORDER_CODE_MESSAGES, submitOrderingForm } from '@/lib/api/errors';
import type { RefundMethod } from '@/lib/api/staff-refunds';
import { formatMoney } from '@/lib/format/money';
import { refundMethodLabel } from '@/lib/format/orders';
import { useIdempotencyKey } from '@/lib/hooks/useIdempotencyKey';
import { useIssueRefund, useRefundOptions } from '@/lib/hooks/useStaffReturns';

/**
 * `components/refunds/RefundDialog.tsx` — ⭐ HO-ORD-002 §11.9, US-ORD-15 AC1. Issue a refund: only the
 * methods the order's payment method allows are offered; a method that is allowed but not yet
 * available (store credit) is shown disabled with the server's reason. The suggested amount for a
 * received return and the most that can be refunded are the server's (`refund-options`); the amount
 * field is pre-filled from them as text and nothing is added or compared here. The second step restates
 * the method and the amount (UI-3). One idempotency key per attempt.
 */
interface Values {
  method: RefundMethod | '';
  returnRequestId: string;
  amount: string;
  reference: string;
  reason: string;
}

export function RefundDialog({
  orderId,
  orderNumber,
  open,
  onOpenChange,
  initialReturnId,
  onDone,
}: {
  orderId: string;
  orderNumber: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  initialReturnId?: string;
  onDone?: () => void;
}) {
  const options = useRefundOptions(orderId, open);
  const issue = useIssueRefund(orderId);
  const key = useIdempotencyKey();
  const [pending, setPending] = useState<Values | null>(null);
  const {
    register,
    control,
    handleSubmit,
    setError,
    setValue,
    reset,
    formState: { errors },
  } = useForm<Values>({
    defaultValues: { method: '', returnRequestId: initialReturnId ?? '', amount: '', reference: '', reason: '' },
  });
  const returnId = useWatch({ control, name: 'returnRequestId' });
  const data = options.data;
  const suggestion = data?.suggestedForReturns.find((s) => s.returnRequestId === returnId);

  useEffect(() => {
    if (open) {
      reset({ method: '', returnRequestId: initialReturnId ?? '', amount: '', reference: '', reason: '' });
      setPending(null);
    }
  }, [open, initialReturnId, reset]);

  // Pre-fill the amount from the server's figures once they arrive or the chosen return changes.
  useEffect(() => {
    if (!data) return;
    setValue('amount', suggestion ? suggestion.suggestedAmount.amount : data.maxRefundable.amount);
  }, [data, suggestion, setValue]);

  const review = handleSubmit((values) => {
    if (!values.method) {
      setError('method', { message: 'Choose how the refund is paid.' });
      return;
    }
    if (!/^\d+(\.\d{1,2})?$/.test(values.amount.trim())) {
      setError('amount', { message: 'Enter the amount as a number, like 500.00.' });
      return;
    }
    if (!values.reason.trim()) {
      setError('reason', { message: 'Enter a reason for the refund.' });
      return;
    }
    setPending(values);
  });

  async function send(values: Values) {
    if (!values.method) return;
    const request = {
      returnRequestId: values.returnRequestId || undefined,
      method: values.method,
      amount: values.amount.trim(),
      reference: values.reference.trim() || undefined,
      reason: values.reason.trim(),
    };
    const done = await submitOrderingForm(
      async () => {
        try {
          return await issue.mutateAsync({ request, idempotencyKey: key.keyFor(JSON.stringify(request)) });
        } catch (error) {
          key.settleFailure(error);
          throw error;
        }
      },
      setError,
      {
        knownFields: ['method', 'amount', 'reference', 'reason'],
        fieldMap: { Method: 'method', Amount: 'amount', Reference: 'reference', Reason: 'reason' },
      },
    );
    if (done) {
      key.renew();
      onOpenChange(false);
      onDone?.();
    } else {
      setPending(null);
    }
  }

  const currency = data?.maxRefundable.currencyCode ?? 'BDT';
  return (
    <OrderDialog
      open={open}
      onOpenChange={onOpenChange}
      title="Issue a refund"
      description={`Refund money to the customer for ${orderNumber}.`}
      onSubmit={pending ? (event) => { event.preventDefault(); void send(pending); } : review}
      submitLabel={pending ? 'Issue refund' : 'Review'}
      pending={issue.isPending}
      rootError={errors.root?.message}
      confirmation={
        pending && pending.method
          ? `Refund ${formatMoney({ amount: pending.amount.trim(), currencyCode: currency })} by ${refundMethodLabel(pending.method).toLowerCase()} for ${orderNumber}?`
          : undefined
      }
      onBack={() => setPending(null)}
    >
      {options.isLoading && <p role="status" className="text-sm text-ink-muted">Loading the refund options…</p>}
      {options.isError && <p role="alert" className="text-sm font-semibold text-danger-ink">We couldn’t load the refund options. Close this and try again.</p>}
      {data && (
        <>
          <p className="text-sm text-ink">
            Most that can be refunded: <strong><Amount money={data.maxRefundable} /></strong>
          </p>
          {data.suggestedForReturns.length > 0 && (
            <SelectField id="refund-return" label="For a received return" hint="Optional. Choosing one pre-fills the suggested amount." {...register('returnRequestId')}>
              <option value="">Not for a return</option>
              {data.suggestedForReturns.map((s) => (
                <option key={s.returnRequestId} value={s.returnRequestId}>
                  Return — suggested {formatMoney(s.suggestedAmount)}
                </option>
              ))}
            </SelectField>
          )}
          <fieldset className="m-0 flex flex-col gap-2 border-0 p-0" aria-describedby={errors.method ? 'refund-method-error' : undefined}>
            <legend className="mb-1 text-sm font-semibold text-ink">Refund by</legend>
            {data.methods.filter((m) => m.allowed).map((m) => (
              <label key={m.method} className="flex flex-wrap items-center gap-2 text-sm">
                <input type="radio" value={m.method} disabled={!m.available} className="h-4 w-4 accent-[var(--accent)]" {...register('method')} />
                {refundMethodLabel(m.method)}
                {!m.available && (
                  <span className="text-ink-muted">
                    — {(m.unavailableReason && ORDER_CODE_MESSAGES[m.unavailableReason]) || m.unavailableReason || 'Not available yet.'}
                  </span>
                )}
              </label>
            ))}
            {data.methods.every((m) => !m.allowed) && <p className="text-sm text-ink-muted">No refund method is allowed for this order.</p>}
            {errors.method && <p id="refund-method-error" role="alert" className="text-xs font-semibold text-danger-ink">{errors.method.message}</p>}
          </fieldset>
          <MoneyField id="refund-amount" label="Amount" currencyCode={currency} required error={errors.amount?.message} {...register('amount')} />
          <TextField id="refund-reference" label="Reference" maxLength={100} hint="The receipt number for cash, or the transfer reference for a bank transfer." error={errors.reference?.message} {...register('reference')} />
          <TextAreaField id="refund-reason" label="Reason" required maxLength={500} error={errors.reason?.message} {...register('reason')} />
        </>
      )}
    </OrderDialog>
  );
}
