'use client';

import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { TextAreaField } from '@/components/billing/Fields';
import { submitOrderingForm } from '@/lib/api/errors';
import type { DeliveryFailureReason } from '@/lib/api/staff-orders';
import { deliveryFailureLabel } from '@/lib/format/orders';
import { useRecordDeliveryFailure } from '@/lib/hooks/useStaffOrders';
import { OrderDialog } from './OrderDialog';

/**
 * `components/orders/DeliveryFailureDialog.tsx` — HO-ORD-002 §11.8, US-ORD-20 AC3. A parcel that was
 * refused or could not be delivered, or a pickup that was never collected. The reasons offered follow
 * the fulfilment method (the server refuses a mismatch with `DeliveryFailureReasonInvalid`); a refused
 * or undeliverable parcel opens a return and must be received into quarantine, which the text says.
 */
const BY_FULFILMENT: Record<string, DeliveryFailureReason[]> = {
  Delivery: ['Refused', 'Undeliverable'],
  Pickup: ['Uncollected'],
};

export function DeliveryFailureDialog({
  orderId,
  fulfilmentMethod,
  open,
  onOpenChange,
  onReturnOpened,
}: {
  orderId: string;
  fulfilmentMethod: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onReturnOpened?: (returnRequestId: string) => void;
}) {
  const record = useRecordDeliveryFailure(orderId);
  const reasons = BY_FULFILMENT[fulfilmentMethod] ?? ['Undeliverable'];
  const {
    register,
    watch,
    handleSubmit,
    setError,
    reset,
    formState: { errors },
  } = useForm<{ reason: DeliveryFailureReason; note: string }>({
    defaultValues: { reason: reasons[0], note: '' },
  });
  const reason = watch('reason');

  useEffect(() => {
    if (open) reset({ reason: (BY_FULFILMENT[fulfilmentMethod] ?? ['Undeliverable'])[0], note: '' });
  }, [open, fulfilmentMethod, reset]);

  const submit = handleSubmit(async (values) => {
    const result = await submitOrderingForm(
      async () => ({
        outcome: await record.mutateAsync({ reason: values.reason, note: values.note.trim() || undefined }),
      }),
      setError,
      { knownFields: ['reason', 'note'], fieldMap: { Reason: 'reason', Note: 'note' } },
    );
    if (!result) return;
    onOpenChange(false);
    if (result.outcome?.returnRequestId) onReturnOpened?.(result.outcome.returnRequestId);
  });

  return (
    <OrderDialog
      open={open}
      onOpenChange={onOpenChange}
      title={fulfilmentMethod === 'Pickup' ? 'Record not collected' : 'Record delivery failure'}
      description={
        reason === 'Uncollected'
          ? 'The items are released back to stock and the order is closed.'
          : 'A return will be opened and the parcel must be received into quarantine when it comes back.'
      }
      onSubmit={submit}
      submitLabel="Record"
      pending={record.isPending}
      rootError={errors.root?.message}
    >
      <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
        <legend className="mb-1 text-sm font-semibold text-ink">What happened?</legend>
        {reasons.map((value) => (
          <label key={value} className="flex items-center gap-2 text-sm">
            <input type="radio" value={value} className="h-4 w-4 accent-[var(--accent)]" {...register('reason')} />
            {deliveryFailureLabel(value)}
          </label>
        ))}
      </fieldset>
      <TextAreaField
        id="failure-note"
        label="Note"
        maxLength={500}
        hint="Optional."
        error={errors.note?.message}
        {...register('note')}
      />
    </OrderDialog>
  );
}
