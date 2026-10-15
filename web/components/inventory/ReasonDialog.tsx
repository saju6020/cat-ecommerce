'use client';

import * as React from 'react';
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
import { describeInventoryError } from '@/lib/api/errors';
import { TextAreaField } from './fields';

/**
 * `components/inventory/ReasonDialog.tsx` — HO-IMP-002 UI-3. The confirm-with-a-reason dialog every
 * "say why" action uses (quarantine, write off, cancel/close a PO, withdraw a recall, close a
 * breach, reject, acknowledge…). Built on `components/ui/dialog.tsx` (focus trap, Escape, focus
 * return). `consequence` states exactly what the action does; the server's refusal is shown inline
 * and the typed reason is kept so a retry never loses it (NFR-USA-5).
 */
export interface ReasonDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  consequence: React.ReactNode;
  reasonLabel?: string;
  /** When false the reason may be left blank (e.g. an optional acknowledge note). */
  required?: boolean;
  confirmLabel: string;
  pendingLabel?: string;
  variant?: 'primary' | 'destructive';
  /** Throws on failure — the message is shown inside the dialog. */
  onConfirm: (reason: string) => Promise<unknown>;
  hint?: string;
  /** Words a refusal; defaults to the server's own message. */
  describeError?: (error: unknown) => string;
}

export function ReasonDialog({
  open,
  onOpenChange,
  title,
  consequence,
  reasonLabel = 'Reason',
  required = true,
  confirmLabel,
  pendingLabel = 'Working…',
  variant = 'primary',
  onConfirm,
  hint,
  describeError = describeInventoryError,
}: ReasonDialogProps) {
  const [reason, setReason] = React.useState('');
  const [error, setError] = React.useState<string | null>(null);
  const [fieldError, setFieldError] = React.useState<string | undefined>();
  const [pending, setPending] = React.useState(false);
  const returnFocusRef = useDialogReturnFocus(open);
  const idBase = React.useId();

  React.useEffect(() => {
    if (!open) {
      setReason('');
      setError(null);
      setFieldError(undefined);
    }
  }, [open]);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (required && reason.trim() === '') {
      setFieldError(`Enter the ${reasonLabel.toLowerCase()}.`);
      return;
    }
    setFieldError(undefined);
    setError(null);
    setPending(true);
    try {
      await onConfirm(reason.trim());
      onOpenChange(false);
    } catch (caught) {
      setError(describeError(caught));
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      <DialogContent hideClose returnFocusRef={returnFocusRef}>
        <form onSubmit={submit} noValidate>
          <DialogHeader>
            <DialogTitle>{title}</DialogTitle>
            <DialogDescription>{consequence}</DialogDescription>
          </DialogHeader>
          <TextAreaField
            id={`${idBase}-reason`}
            label={reasonLabel}
            required={required}
            value={reason}
            hint={hint}
            error={fieldError}
            onChange={(event) => setReason(event.target.value)}
          />
          {error && (
            <p role="alert" className="mt-3 text-sm font-semibold text-danger-ink">
              {error}
            </p>
          )}
          <DialogFooter>
            <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={pending}>
              Cancel
            </Button>
            <Button
              type="submit"
              variant={variant === 'destructive' ? 'destructive' : 'primary'}
              disabled={pending}
              aria-busy={pending}
            >
              {pending ? pendingLabel : confirmLabel}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

/** A confirm dialog whose action can fail: shows the server's refusal inline (the shared
 *  `ConfirmDialog` stays open on a rejection but has nowhere to say why). */
export function ActionDialog({
  open,
  onOpenChange,
  title,
  consequence,
  confirmLabel,
  pendingLabel = 'Working…',
  variant = 'primary',
  onConfirm,
  children,
  confirmDisabled,
  describeError = describeInventoryError,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  consequence: React.ReactNode;
  confirmLabel: string;
  pendingLabel?: string;
  variant?: 'primary' | 'destructive';
  onConfirm: () => Promise<unknown>;
  children?: React.ReactNode;
  confirmDisabled?: boolean;
  describeError?: (error: unknown) => string;
}) {
  const [error, setError] = React.useState<string | null>(null);
  const [pending, setPending] = React.useState(false);
  const returnFocusRef = useDialogReturnFocus(open);

  React.useEffect(() => {
    if (!open) setError(null);
  }, [open]);

  async function run() {
    setError(null);
    setPending(true);
    try {
      await onConfirm();
      onOpenChange(false);
    } catch (caught) {
      setError(describeError(caught));
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
        {children}
        {error && (
          <p role="alert" className="mt-3 text-sm font-semibold text-danger-ink">
            {error}
          </p>
        )}
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={pending}>
            Cancel
          </Button>
          <Button
            variant={variant === 'destructive' ? 'destructive' : 'primary'}
            onClick={() => void run()}
            disabled={pending || confirmDisabled}
            aria-busy={pending}
          >
            {pending ? pendingLabel : confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
