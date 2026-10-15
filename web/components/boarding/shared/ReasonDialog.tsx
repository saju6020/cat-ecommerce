'use client';

import { useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  useDialogReturnFocus,
} from '@/components/ui/dialog';
import { describeBoardingError } from '@/lib/api/errors';
import { FormErrorAlert, TextAreaRow } from './FormBits';

/**
 * `components/boarding/shared/ReasonDialog.tsx` — UI-3: a confirmation that **states what is
 * affected** (`consequence`) and, where the story demands it, asks for a **mandatory reason**
 * (relocate, cancel as staff, skip a task, resolve an incident, override the vaccination gate,
 * hide media…). The reason is validated here, the server's refusal is shown in words, and the
 * dialog keeps what was typed while a request fails (NFR-USA-5). Built on the shared Radix dialog,
 * so focus is trapped, Escape closes and focus returns to the opener.
 */
export interface ReasonDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  consequence: ReactNode;
  reasonLabel: string;
  requiredMessage: string;
  confirmLabel: string;
  pendingLabel?: string;
  variant?: 'primary' | 'destructive';
  /** Extra content between the consequence and the field (e.g. a unit picker). */
  children?: ReactNode;
  /** Extra check run before submit; return a message to block it. */
  validate?: () => string | null;
  onConfirm: (reason: string) => Promise<unknown>;
  submitDisabled?: boolean;
}

export function ReasonDialog({
  open,
  onOpenChange,
  title,
  consequence,
  reasonLabel,
  requiredMessage,
  confirmLabel,
  pendingLabel = 'Working…',
  variant = 'primary',
  children,
  validate,
  onConfirm,
  submitDisabled,
}: ReasonDialogProps) {
  const [reason, setReason] = useState('');
  const [fieldError, setFieldError] = useState<string | undefined>();
  const [serverError, setServerError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const returnFocusRef = useDialogReturnFocus(open);

  async function submit() {
    const trimmed = reason.trim();
    if (!trimmed) {
      setFieldError(requiredMessage);
      return;
    }
    const problem = validate?.() ?? null;
    if (problem) {
      setServerError(problem);
      return;
    }
    setFieldError(undefined);
    setServerError(null);
    setPending(true);
    try {
      await onConfirm(trimmed);
      setReason('');
      onOpenChange(false);
    } catch (error) {
      setServerError(describeBoardingError(error));
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      <DialogContent hideClose returnFocusRef={returnFocusRef}>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{consequence}</DialogDescription>
        </DialogHeader>
        <div className="flex flex-col gap-3">
          {children}
          <TextAreaRow
            id="reason-dialog-reason"
            label={reasonLabel}
            required
            value={reason}
            error={fieldError}
            onChange={(event) => setReason(event.target.value)}
          />
          <FormErrorAlert message={serverError} />
        </div>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={pending}>
            Cancel
          </Button>
          <Button
            variant={variant === 'destructive' ? 'destructive' : 'primary'}
            onClick={() => void submit()}
            disabled={pending || submitDisabled}
            aria-busy={pending}
          >
            {pending ? pendingLabel : confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
