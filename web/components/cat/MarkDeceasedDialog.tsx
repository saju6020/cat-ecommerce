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
import { todayAsDateOnly } from '@/lib/dateOnly';

/**
 * `components/cat/MarkDeceasedDialog.tsx` — US-CAT-09, UI-3 (HO-CAT-002 §11.7). States the
 * actual consequences — reminders stop, the cat drops out of booking/ordering, the record is
 * **kept**, not deleted — rather than a generic "are you sure?". Component test U26.
 */
const MARK_DECEASED_FIELD_MAP = { DeceasedOn: 'deceasedOn', Note: 'note' } as const;
const MARK_DECEASED_KNOWN_FIELDS = Object.values(MARK_DECEASED_FIELD_MAP);

const markDeceasedFormSchema = z.object({
  deceasedOn: z.string().min(1, 'Enter the date.'),
  note: z.string().max(500).optional(),
});

export type MarkDeceasedFormValues = z.infer<typeof markDeceasedFormSchema>;

export interface MarkDeceasedDialogProps {
  catName: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: (values: MarkDeceasedFormValues) => Promise<unknown>;
  isSubmitting?: boolean;
}

export function MarkDeceasedDialog({
  catName,
  open,
  onOpenChange,
  onSubmit,
  isSubmitting,
}: MarkDeceasedDialogProps) {
  const returnFocusRef = useDialogReturnFocus(open);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<MarkDeceasedFormValues>({
    resolver: zodResolver(markDeceasedFormSchema),
    defaultValues: { deceasedOn: todayAsDateOnly(), note: '' },
  });

  return (
    <Dialog open={open} onOpenChange={(next) => !isSubmitting && onOpenChange(next)}>
      <DialogContent returnFocusRef={returnFocusRef}>
        <DialogHeader>
          <DialogTitle>Mark {catName} as deceased</DialogTitle>
          <DialogDescription>
            Reminders (vaccination and parasite-control due dates) stop, and {catName} is hidden
            from booking and ordering screens. The record itself is kept — it stays viewable, but
            can no longer be edited.
          </DialogDescription>
        </DialogHeader>

        <form
          id="mark-deceased-form"
          onSubmit={handleSubmit((values) =>
            submitWithServerErrors(() => onSubmit(values), setError, {
              fieldMap: MARK_DECEASED_FIELD_MAP,
              knownFields: MARK_DECEASED_KNOWN_FIELDS,
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
            <label htmlFor="deceasedOn" className="text-sm font-semibold text-ink">
              Date{' '}
              <span aria-hidden="true" className="text-danger-ink">
                *
              </span>
            </label>
            <input
              id="deceasedOn"
              type="date"
              {...register('deceasedOn')}
              required
              aria-invalid={!!errors.deceasedOn}
              aria-describedby={errors.deceasedOn ? 'deceasedOn-error' : undefined}
              className="w-fit rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
            />
            {errors.deceasedOn && (
              <p
                id="deceasedOn-error"
                role="alert"
                className="text-xs font-semibold text-danger-ink"
              >
                {errors.deceasedOn.message}
              </p>
            )}
          </div>
          <div className="flex flex-col gap-1.5">
            <label htmlFor="deceased-note" className="text-sm font-semibold text-ink">
              Note
            </label>
            <textarea
              id="deceased-note"
              rows={2}
              {...register('note')}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
            />
          </div>
        </form>

        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button
            type="submit"
            form="mark-deceased-form"
            variant="destructive"
            disabled={isSubmitting}
            aria-busy={isSubmitting}
          >
            {isSubmitting ? 'Saving…' : 'Mark as deceased'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
