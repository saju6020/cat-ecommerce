'use client';

import { useEffect, type ReactNode } from 'react';
import { useForm } from 'react-hook-form';
import { TextAreaField } from '@/components/billing/Fields';
import { submitOrderingForm } from '@/lib/api/errors';
import { OrderDialog } from './OrderDialog';

/**
 * `components/orders/OrderReasonDialog.tsx` — HO-ORD-002 UI-3. A confirm-with-a-reason dialog for the
 * Ordering staff actions that need a note (cancel an order, reject a held payment, reject a return,
 * resolve a reconciliation issue): `consequence` states exactly what is affected, the note is
 * required, and the server's refusal lands under the field or as a message — the typed note is kept.
 */
export function OrderReasonDialog({
  open,
  onOpenChange,
  title,
  consequence,
  confirmLabel,
  reasonLabel = 'Reason',
  variant = 'destructive',
  onSubmit,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  consequence: ReactNode;
  confirmLabel: string;
  reasonLabel?: string;
  variant?: 'primary' | 'destructive';
  onSubmit: (note: string) => Promise<unknown>;
}) {
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<{ note: string }>({ defaultValues: { note: '' } });

  useEffect(() => {
    if (!open) reset();
  }, [open, reset]);

  const submit = handleSubmit(async (values) => {
    if (!values.note.trim()) {
      setError('note', { message: 'Enter a reason.' });
      return;
    }
    const done = await submitOrderingForm(
      async () => {
        await onSubmit(values.note.trim());
        return true as const;
      },
      setError,
      { knownFields: ['note'], fieldMap: { Note: 'note' } },
    );
    if (done) onOpenChange(false);
  });

  return (
    <OrderDialog
      open={open}
      onOpenChange={onOpenChange}
      title={title}
      description={consequence}
      onSubmit={submit}
      submitLabel={confirmLabel}
      pending={isSubmitting}
      variant={variant}
      rootError={errors.root?.message}
    >
      <TextAreaField
        id="order-reason"
        label={reasonLabel}
        required
        maxLength={500}
        error={errors.note?.message}
        {...register('note')}
      />
    </OrderDialog>
  );
}
