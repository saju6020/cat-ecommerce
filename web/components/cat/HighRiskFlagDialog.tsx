'use client';

import * as React from 'react';
import { useForm, type UseFormRegister } from 'react-hook-form';
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

const FLAG_FIELD_MAP = { HandlingNote: 'handlingNote', Reason: 'reason' } as const;
const FLAG_KNOWN_FIELDS = Object.values(FLAG_FIELD_MAP);
const CLEAR_FIELD_MAP = { Reason: 'reason' } as const;
const CLEAR_KNOWN_FIELDS = Object.values(CLEAR_FIELD_MAP);

/**
 * `components/cat/HighRiskFlagDialog.tsx` — US-CAT-04 (HO-CAT-002 §11.7). Set and clear.
 * `reason` is required in both directions — the API rejects without it (`FlagCatHighRiskCommand`/
 * `ClearCatHighRiskFlagCommand`, HO-CAT-001 §11.3) — and `handlingNote` is additionally required
 * to set the flag. Clearing confirms (UI-3), stating exactly what stops: the warning banner and
 * the handling note both disappear from every screen that shows this cat.
 */
const flagSchema = z.object({
  handlingNote: z.string().trim().min(1, 'Enter a handling note.').max(500),
  reason: z.string().trim().min(1, 'Enter a reason.').max(1000),
});
const clearSchema = z.object({
  reason: z.string().trim().min(1, 'Enter a reason.').max(1000),
});

export type HighRiskFlagDialogValues =
  { mode: 'flag'; handlingNote: string; reason: string } | { mode: 'clear'; reason: string };

export interface HighRiskFlagDialogProps {
  mode: 'flag' | 'clear';
  catName: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: (values: HighRiskFlagDialogValues) => Promise<unknown>;
  isSubmitting?: boolean;
}

export function HighRiskFlagDialog({
  mode,
  catName,
  open,
  onOpenChange,
  onSubmit,
  isSubmitting,
}: HighRiskFlagDialogProps) {
  const returnFocusRef = useDialogReturnFocus(open);
  const flagForm = useForm<z.infer<typeof flagSchema>>({
    resolver: zodResolver(flagSchema),
    defaultValues: { handlingNote: '', reason: '' },
  });
  const clearForm = useForm<z.infer<typeof clearSchema>>({
    resolver: zodResolver(clearSchema),
    defaultValues: { reason: '' },
  });

  React.useEffect(() => {
    if (open) {
      flagForm.reset({ handlingNote: '', reason: '' });
      clearForm.reset({ reason: '' });
    }
  }, [open, flagForm, clearForm]);

  return (
    <Dialog open={open} onOpenChange={(next) => !isSubmitting && onOpenChange(next)}>
      <DialogContent returnFocusRef={returnFocusRef}>
        <DialogHeader>
          <DialogTitle>
            {mode === 'flag' ? `Flag ${catName} as high-risk` : `Clear ${catName}'s high-risk flag`}
          </DialogTitle>
          <DialogDescription>
            {mode === 'flag'
              ? 'This shows a high-risk warning and the handling note on every screen for this cat — the profile header, the cat list, and clinic/boarding/order screens that show the badge.'
              : 'This removes the high-risk warning and handling note from every screen for this cat, including the cat list. Staff will no longer be alerted before handling this cat.'}
          </DialogDescription>
        </DialogHeader>

        {mode === 'flag' ? (
          <form
            id="high-risk-flag-form"
            onSubmit={flagForm.handleSubmit((values) =>
              submitWithServerErrors(
                () => onSubmit({ mode: 'flag', ...values }),
                flagForm.setError,
                { fieldMap: FLAG_FIELD_MAP, knownFields: FLAG_KNOWN_FIELDS },
              ),
            )}
            noValidate
            className="flex flex-col gap-4"
          >
            {flagForm.formState.errors.root && (
              <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
                {flagForm.formState.errors.root.message}
              </p>
            )}
            <div className="flex flex-col gap-1.5">
              <label htmlFor="handlingNote" className="text-sm font-semibold text-ink">
                Handling note{' '}
                <span aria-hidden="true" className="text-danger-ink">
                  *
                </span>
              </label>
              <textarea
                id="handlingNote"
                rows={3}
                required
                {...flagForm.register('handlingNote')}
                aria-invalid={!!flagForm.formState.errors.handlingNote}
                aria-describedby={
                  flagForm.formState.errors.handlingNote ? 'handlingNote-error' : undefined
                }
                className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
              />
              {flagForm.formState.errors.handlingNote && (
                <p
                  id="handlingNote-error"
                  role="alert"
                  className="text-xs font-semibold text-danger-ink"
                >
                  {flagForm.formState.errors.handlingNote.message}
                </p>
              )}
            </div>
            <ReasonField register={flagForm.register} error={flagForm.formState.errors.reason} />
          </form>
        ) : (
          <form
            id="high-risk-flag-form"
            onSubmit={clearForm.handleSubmit((values) =>
              submitWithServerErrors(
                () => onSubmit({ mode: 'clear', ...values }),
                clearForm.setError,
                { fieldMap: CLEAR_FIELD_MAP, knownFields: CLEAR_KNOWN_FIELDS },
              ),
            )}
            noValidate
            className="flex flex-col gap-4"
          >
            {clearForm.formState.errors.root && (
              <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
                {clearForm.formState.errors.root.message}
              </p>
            )}
            <ReasonField register={clearForm.register} error={clearForm.formState.errors.reason} />
          </form>
        )}

        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button
            type="submit"
            form="high-risk-flag-form"
            variant={mode === 'clear' ? 'destructive' : 'primary'}
            disabled={isSubmitting}
            aria-busy={isSubmitting}
          >
            {isSubmitting
              ? 'Saving…'
              : mode === 'flag'
                ? 'Flag as high-risk'
                : 'Clear high-risk flag'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// Accepts either internal form's `register`/`errors.reason` directly (not the whole
// `UseFormReturn`, whose methods like `watch` are invariant in TFieldValues and so don't
// structurally match between the flag and clear forms' differently-shaped value types).
function ReasonField({
  register,
  error,
}: {
  // Accepts either the flag or the clear form's differently-shaped register, which register's
  // own option types are not covariant enough to unify without widening here.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  register: UseFormRegister<any>;
  error?: { message?: string };
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor="reason" className="text-sm font-semibold text-ink">
        Reason{' '}
        <span aria-hidden="true" className="text-danger-ink">
          *
        </span>
      </label>
      <textarea
        id="reason"
        rows={2}
        required
        {...register('reason')}
        aria-invalid={!!error}
        aria-describedby={error ? 'reason-error' : undefined}
        className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
      />
      {error && (
        <p id="reason-error" role="alert" className="text-xs font-semibold text-danger-ink">
          {error.message}
        </p>
      )}
    </div>
  );
}
