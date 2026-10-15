'use client';

import { useEffect } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { SelectField, TextAreaField } from '@/components/billing/Fields';
import { submitOrderingForm } from '@/lib/api/errors';
import type { StaffOrderLine } from '@/lib/api/staff-orders';
import { useBatches } from '@/lib/hooks/useInventory';
import { useReallocateLine } from '@/lib/hooks/useStaffOrders';
import { OrderDialog } from './OrderDialog';

/**
 * `components/orders/ReallocateLineDialog.tsx` — HO-ORD-002 §11.8, US-IMP-07 AC2. Move one line to
 * another batch of the same product, with a **mandatory reason**. The batches are Inventory's list for
 * the line's SKU; whether one is usable (recalled, expired, short) is the server's answer, shown as
 * its message — the dialog filters nothing itself.
 */
interface Values {
  lineId: string;
  targetBatchId: string;
  reason: string;
}

export function ReallocateLineDialog({
  orderId,
  lines,
  initialLineId,
  open,
  onOpenChange,
}: {
  orderId: string;
  lines: StaffOrderLine[];
  initialLineId?: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const reallocate = useReallocateLine(orderId);
  const first = initialLineId ?? lines[0]?.lineId ?? '';
  const {
    register,
    control,
    handleSubmit,
    setError,
    reset,
    formState: { errors },
  } = useForm<Values>({ defaultValues: { lineId: first, targetBatchId: '', reason: '' } });
  const lineId = useWatch({ control, name: 'lineId' });
  const line = lines.find((l) => l.lineId === lineId);
  const batches = useBatches({ skuId: line?.skuId }, open && !!line);

  useEffect(() => {
    if (open) reset({ lineId: first, targetBatchId: '', reason: '' });
  }, [open, first, reset]);

  const submit = handleSubmit(async (values) => {
    const done = await submitOrderingForm(
      async () => {
        await reallocate.mutateAsync(values);
        return true as const;
      },
      setError,
      { knownFields: ['targetBatchId', 'reason'], fieldMap: { TargetBatchId: 'targetBatchId', Reason: 'reason' } },
    );
    if (done) onOpenChange(false);
  });

  return (
    <OrderDialog
      open={open}
      onOpenChange={onOpenChange}
      title="Reallocate a line"
      description="Move this line to another batch. The reason is recorded in the audit trail."
      onSubmit={submit}
      submitLabel="Reallocate"
      pending={reallocate.isPending}
      rootError={errors.root?.message}
    >
      <SelectField id="realloc-line" label="Line" {...register('lineId')}>
        {lines.map((l) => (
          <option key={l.lineId} value={l.lineId}>
            {`${l.productName} ${l.variantLabel}`.trim()} — batch {l.allocations.map((a) => a.batchNumber).join(', ') || 'none'}
          </option>
        ))}
      </SelectField>
      <SelectField
        id="realloc-batch"
        label="New batch"
        required
        error={errors.targetBatchId?.message}
        {...register('targetBatchId', { required: 'Choose a batch.' })}
      >
        <option value="">Choose a batch</option>
        {(batches.data?.items ?? []).map((batch) => (
          <option key={batch.id} value={batch.id}>
            {batch.lotNumber}
            {batch.expiresOn ? ` · expires ${batch.expiresOn}` : ''} · {batch.status}
          </option>
        ))}
      </SelectField>
      <TextAreaField
        id="realloc-reason"
        label="Reason"
        required
        maxLength={500}
        error={errors.reason?.message}
        {...register('reason', {
          required: 'Enter a reason.',
          validate: (value) => value.trim().length > 0 || 'Enter a reason.',
        })}
      />
    </OrderDialog>
  );
}
