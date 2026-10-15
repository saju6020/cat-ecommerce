'use client';

import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { SelectField, TextField } from '@/components/billing/Fields';
import { hasOrderingCode, submitOrderingForm } from '@/lib/api/errors';
import { useDispatchCouriers, useDispatchOrder, useRecordTracking } from '@/lib/hooks/useStaffOrders';
import { OrderDialog } from './OrderDialog';

/**
 * `components/orders/DispatchDialog.tsx` + `TrackingDialog` — HO-ORD-002 §11.8, US-ORD-11. Dispatch needs
 * a courier; the tracking number is optional and can be entered later from the order. A refusal that is
 * about stock (`BatchRecalled`, `BatchExpired`, `OrderNotFullyAllocated`, BR-14) shows the server's
 * message with a **Reallocate** shortcut that closes this dialog and opens the reallocation one.
 */
interface DispatchValues {
  courierId: string;
  trackingNumber: string;
}

const STOCK_CODES = ['BatchRecalled', 'BatchExpired', 'OrderNotFullyAllocated'];

export function DispatchDialog({
  orderId,
  open,
  onOpenChange,
  onReallocate,
}: {
  orderId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onReallocate: () => void;
}) {
  const couriers = useDispatchCouriers(open);
  const dispatch = useDispatchOrder(orderId);
  const [stockRefusal, setStockRefusal] = useState(false);
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors },
  } = useForm<DispatchValues>({ defaultValues: { courierId: '', trackingNumber: '' } });

  useEffect(() => {
    if (!open) {
      reset();
      setStockRefusal(false);
    }
  }, [open, reset]);

  const submit = handleSubmit(async (values) => {
    if (!values.courierId) {
      setError('courierId', { message: 'Choose a courier.' });
      return;
    }
    setStockRefusal(false);
    const done = await submitOrderingForm(
      async () => {
        try {
          await dispatch.mutateAsync({
            courierId: values.courierId,
            trackingNumber: values.trackingNumber.trim() || undefined,
          });
        } catch (error) {
          if (STOCK_CODES.some((code) => hasOrderingCode(error, code))) setStockRefusal(true);
          throw error;
        }
        return true as const;
      },
      setError,
      { knownFields: ['courierId', 'trackingNumber'], fieldMap: { CourierId: 'courierId', TrackingNumber: 'trackingNumber' } },
    );
    if (done) onOpenChange(false);
  });

  return (
    <OrderDialog
      open={open}
      onOpenChange={onOpenChange}
      title="Dispatch order"
      description="The order moves to Dispatched and the customer can see the courier and tracking number."
      onSubmit={submit}
      submitLabel="Dispatch"
      pending={dispatch.isPending}
      rootError={errors.root?.message}
    >
      <SelectField id="dispatch-courier" label="Courier" required error={errors.courierId?.message} {...register('courierId')}>
        <option value="">Choose a courier</option>
        {(couriers.data ?? []).map((courier) => (
          <option key={courier.id} value={courier.id}>
            {courier.name}
          </option>
        ))}
      </SelectField>
      <TextField
        id="dispatch-tracking"
        label="Tracking number"
        maxLength={100}
        hint="Optional. If the courier hasn’t given you one yet, leave it empty and enter it later from the order."
        error={errors.trackingNumber?.message}
        {...register('trackingNumber')}
      />
      {stockRefusal && (
        <div className="flex flex-wrap items-center gap-3 rounded-[var(--radius-sm)] border border-border p-3 text-sm">
          <span>Move the affected line to another batch, then dispatch again.</span>
          <Button variant="secondary" size="sm" onClick={onReallocate}>
            Reallocate a line
          </Button>
        </div>
      )}
    </OrderDialog>
  );
}

export function TrackingDialog({
  orderId,
  open,
  onOpenChange,
  current,
}: {
  orderId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  current: string | null;
}) {
  const record = useRecordTracking(orderId);
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors },
  } = useForm<{ trackingNumber: string }>({ defaultValues: { trackingNumber: current ?? '' } });

  useEffect(() => {
    if (open) reset({ trackingNumber: current ?? '' });
  }, [open, current, reset]);

  const submit = handleSubmit(async (values) => {
    if (!values.trackingNumber.trim()) {
      setError('trackingNumber', { message: 'Enter the tracking number.' });
      return;
    }
    const done = await submitOrderingForm(
      async () => {
        await record.mutateAsync(values.trackingNumber.trim());
        return true as const;
      },
      setError,
      { knownFields: ['trackingNumber'] },
    );
    if (done) onOpenChange(false);
  });

  return (
    <OrderDialog
      open={open}
      onOpenChange={onOpenChange}
      title={current ? 'Change tracking number' : 'Enter tracking number'}
      description="The customer sees this number, and a tracking link when the courier has one."
      onSubmit={submit}
      submitLabel="Save tracking number"
      pending={record.isPending}
      rootError={errors.root?.message}
    >
      <TextField
        id="tracking-number"
        label="Tracking number"
        required
        maxLength={100}
        error={errors.trackingNumber?.message}
        {...register('trackingNumber')}
      />
    </OrderDialog>
  );
}
