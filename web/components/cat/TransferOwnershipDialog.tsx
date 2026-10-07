'use client';

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
import { submitWithServerErrors } from '@/lib/api/errors';

/**
 * `components/cat/TransferOwnershipDialog.tsx` — US-CAT-08, UI-3 (HO-CAT-002 §11.7). States
 * exactly what transfers (all medical history) and what the previous owner loses (visibility —
 * with HO-CAT-002 §20.3's caveat that this is a query filter, not authorization, made explicit
 * in the copy rather than overclaiming security). Component test U27.
 */
const TRANSFER_FIELD_MAP = { NewOwnerId: 'newOwnerId', Reason: 'reason' } as const;
const TRANSFER_KNOWN_FIELDS = Object.values(TRANSFER_FIELD_MAP);

const transferOwnershipFormSchema = z.object({
  newOwnerId: z.string().trim().min(1, "Enter the new owner's customer ID."),
  reason: z.string().trim().min(1, 'Enter a reason.').max(1000),
});

export type TransferOwnershipFormValues = z.infer<typeof transferOwnershipFormSchema>;

export interface TransferOwnershipDialogProps {
  catName: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: (values: TransferOwnershipFormValues) => Promise<unknown>;
  isSubmitting?: boolean;
}

export function TransferOwnershipDialog({
  catName,
  open,
  onOpenChange,
  onSubmit,
  isSubmitting,
}: TransferOwnershipDialogProps) {
  const returnFocusRef = useDialogReturnFocus(open);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<TransferOwnershipFormValues>({
    resolver: zodResolver(transferOwnershipFormSchema),
    defaultValues: { newOwnerId: '', reason: '' },
  });

  return (
    <Dialog open={open} onOpenChange={(next) => !isSubmitting && onOpenChange(next)}>
      <DialogContent returnFocusRef={returnFocusRef}>
        <DialogHeader>
          <DialogTitle>Transfer ownership of {catName}</DialogTitle>
          <DialogDescription>
            All of {catName}&rsquo;s medical history — vaccinations, weights, allergies, everything
            recorded so far — moves to the new owner with the cat. The current owner will no longer
            see this record in their list (this is enforced by who the record is looked up for, not
            a login permission).
          </DialogDescription>
        </DialogHeader>

        <form
          id="transfer-ownership-form"
          onSubmit={handleSubmit((values) =>
            submitWithServerErrors(() => onSubmit(values), setError, {
              fieldMap: TRANSFER_FIELD_MAP,
              knownFields: TRANSFER_KNOWN_FIELDS,
            }),
          )}
          noValidate
          className="flex flex-col gap-4"
        >
          {errors.root && (
            <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
              {errors.root.message}
            </p>
          )}
          <div className="flex flex-col gap-1.5">
            <label htmlFor="newOwnerId" className="text-sm font-semibold text-ink">
              New owner customer ID{' '}
              <span aria-hidden="true" className="text-danger-ink">
                *
              </span>
            </label>
            <input
              id="newOwnerId"
              {...register('newOwnerId')}
              required
              aria-invalid={!!errors.newOwnerId}
              aria-describedby={errors.newOwnerId ? 'newOwnerId-error' : undefined}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
            />
            {errors.newOwnerId && (
              <p
                id="newOwnerId-error"
                role="alert"
                className="text-xs font-semibold text-danger-ink"
              >
                {errors.newOwnerId.message}
              </p>
            )}
          </div>
          <div className="flex flex-col gap-1.5">
            <label htmlFor="transfer-reason" className="text-sm font-semibold text-ink">
              Reason{' '}
              <span aria-hidden="true" className="text-danger-ink">
                *
              </span>
            </label>
            <textarea
              id="transfer-reason"
              rows={2}
              required
              {...register('reason')}
              aria-invalid={!!errors.reason}
              aria-describedby={errors.reason ? 'transfer-reason-error' : undefined}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
            />
            {errors.reason && (
              <p
                id="transfer-reason-error"
                role="alert"
                className="text-xs font-semibold text-danger-ink"
              >
                {errors.reason.message}
              </p>
            )}
          </div>
        </form>

        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button
            type="submit"
            form="transfer-ownership-form"
            variant="destructive"
            disabled={isSubmitting}
            aria-busy={isSubmitting}
          >
            {isSubmitting ? 'Transferring…' : 'Transfer ownership'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
