'use client';

import type { ReactNode } from 'react';
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
import { submitBillingForm } from '@/lib/api/errors';
import { FormRootError, TextAreaField } from './Fields';
import { requiredGuardedText } from './schemas';

/**
 * `components/billing/ReasonDialog.tsx` — HO-BIL-002 §7 UI-3. One confirm-with-a-reason dialog on
 * `components/ui/dialog.tsx`, used by every Billing action that needs one (void a charge, reject or
 * reverse a payment, release an earmark, unmatch a statement line). `consequence` is required and
 * states exactly what is affected; the reason runs the C-5 guard (it is copied into audit rows). A
 * server refusal lands under the field or as a form message and the typed reason is kept.
 */
const schema = z.object({ reason: requiredGuardedText(500, 'Enter a reason.') });
type Values = z.infer<typeof schema>;

export function ReasonDialog({
  open,
  onOpenChange,
  title,
  consequence,
  confirmLabel,
  pendingLabel = 'Working…',
  variant = 'destructive',
  reasonLabel = 'Reason',
  onSubmit,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  consequence: ReactNode;
  confirmLabel: string;
  pendingLabel?: string;
  variant?: 'primary' | 'destructive';
  reasonLabel?: string;
  onSubmit: (reason: string) => Promise<unknown>;
}) {
  const returnFocusRef = useDialogReturnFocus(open);
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { reason: '' },
    mode: 'onChange',
  });

  const submit = handleSubmit(async (values) => {
    const ok = await submitBillingForm(
      async () => {
        await onSubmit(values.reason);
        return true as const;
      },
      setError,
      { knownFields: ['reason'] },
    );
    if (ok) {
      reset();
      onOpenChange(false);
    }
  });

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (isSubmitting) return;
        if (!next) reset();
        onOpenChange(next);
      }}
    >
      <DialogContent returnFocusRef={returnFocusRef} className="max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{consequence}</DialogDescription>
        </DialogHeader>
        <form id="reason-dialog-form" onSubmit={submit} noValidate className="flex flex-col gap-4">
          <FormRootError message={errors.root?.message} />
          <TextAreaField
            id="reason-dialog-reason"
            label={reasonLabel}
            required
            error={errors.reason?.message}
            {...register('reason')}
          />
        </form>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button
            type="submit"
            form="reason-dialog-form"
            variant={variant}
            disabled={isSubmitting}
            aria-busy={isSubmitting}
          >
            {isSubmitting ? pendingLabel : confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
