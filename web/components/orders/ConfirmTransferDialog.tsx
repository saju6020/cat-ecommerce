'use client';

import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { MoneyField, TextField } from '@/components/billing/Fields';
import { submitOrderingForm } from '@/lib/api/errors';
import type { Money } from '@/lib/format/money';
import { formatMoney } from '@/lib/format/money';
import { branchLocalToInstant, nowInBranchLocal } from '@/lib/format/orders';
import { useIdempotencyKey } from '@/lib/hooks/useIdempotencyKey';
import { useConfirmBankTransfer } from '@/lib/hooks/useStaffOrders';
import { OrderDialog } from './OrderDialog';

/**
 * `components/orders/ConfirmTransferDialog.tsx` — ⭐ HO-ORD-002 §11.9, US-ORD-19 AC2. Staff record that
 * a customer's bank transfer arrived: the amount (pre-filled from the order, must match — the server
 * says so if not), the bank reference and when it was received. The second step restates it
 * (*"Confirm BDT 1,927.50 received against ORD-…?"*, UI-3). A reply of `awaitingApproval: true` means the
 * amount is over the approval threshold and someone else must approve it; the caller shows that.
 */
interface Values {
  amount: string;
  bankReference: string;
  receivedOn: string;
}

export interface TransferTarget {
  orderId: string;
  orderNumber: string;
  version: number;
  amount: Money;
  zone: string;
  /** What the customer reported paying with, shown as a hint. */
  customerReference?: string | null;
}

export function ConfirmTransferDialog({
  target,
  open,
  onOpenChange,
  onDone,
}: {
  target: TransferTarget;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onDone: (outcome: { awaitingApproval: boolean }) => void;
}) {
  const confirm = useConfirmBankTransfer(target.orderId);
  const key = useIdempotencyKey();
  const [pending, setPending] = useState<Values | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors },
  } = useForm<Values>({
    defaultValues: { amount: target.amount.amount, bankReference: '', receivedOn: nowInBranchLocal(target.zone) },
  });

  useEffect(() => {
    if (open) {
      reset({ amount: target.amount.amount, bankReference: '', receivedOn: nowInBranchLocal(target.zone) });
      setPending(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, target.orderId]);

  const review = handleSubmit((values) => {
    if (!/^\d+(\.\d{1,2})?$/.test(values.amount.trim())) {
      setError('amount', { message: 'Enter the amount as a number, like 1927.50.' });
      return;
    }
    if (!values.bankReference.trim()) {
      setError('bankReference', { message: 'Enter the bank reference.' });
      return;
    }
    setPending(values);
  });

  async function send(values: Values) {
    const request = {
      expectedVersion: target.version,
      amount: values.amount.trim(),
      bankReference: values.bankReference.trim(),
      receivedOn: branchLocalToInstant(values.receivedOn, target.zone),
    };
    const result = await submitOrderingForm(
      async () => {
        try {
          return await confirm.mutateAsync({ request, idempotencyKey: key.keyFor(JSON.stringify(request)) });
        } catch (error) {
          key.settleFailure(error);
          throw error;
        }
      },
      setError,
      {
        knownFields: ['amount', 'bankReference', 'receivedOn'],
        fieldMap: { Amount: 'amount', BankReference: 'bankReference', ReceivedOn: 'receivedOn' },
      },
    );
    if (result) {
      key.renew();
      onOpenChange(false);
      onDone({ awaitingApproval: result.awaitingApproval });
    } else {
      setPending(null);
    }
  }

  return (
    <OrderDialog
      open={open}
      onOpenChange={onOpenChange}
      title="Confirm transfer"
      description={`Record that the bank transfer for ${target.orderNumber} has arrived. Times are in ${target.zone}.`}
      onSubmit={pending ? (event) => { event.preventDefault(); void send(pending); } : review}
      submitLabel={pending ? 'Confirm' : 'Review'}
      pending={confirm.isPending}
      rootError={errors.root?.message}
      confirmation={
        pending
          ? `Confirm ${formatMoney({ amount: pending.amount.trim(), currencyCode: target.amount.currencyCode })} received against ${target.orderNumber}?`
          : undefined
      }
      onBack={() => setPending(null)}
    >
      <MoneyField id="transfer-amount" label="Amount received" currencyCode={target.amount.currencyCode} required error={errors.amount?.message} {...register('amount')} />
      <TextField
        id="transfer-bank-ref"
        label="Bank reference"
        required
        maxLength={100}
        hint={target.customerReference ? `The customer reported: ${target.customerReference}` : undefined}
        error={errors.bankReference?.message}
        {...register('bankReference')}
      />
      <TextField id="transfer-received-on" label="Received on" type="datetime-local" required error={errors.receivedOn?.message} {...register('receivedOn', { required: 'Enter when it arrived.' })} />
    </OrderDialog>
  );
}
