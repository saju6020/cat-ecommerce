'use client';

import type { FormEventHandler, ReactNode } from 'react';
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
import { FormRootError } from '@/components/billing/Fields';

/**
 * `components/orders/OrderDialog.tsx` — HO-ORD-002 §11.8/§11.9, UI-3. The frame every staff Ordering
 * dialog shares: a heading, a sentence saying exactly what the action affects, the form, the server's
 * message (`rootError`, kept until the next try), and Cancel / submit buttons that are disabled while a
 * request is in flight. Money dialogs add a second step: when `confirmation` is set, the form is hidden
 * (its values stay, ready for the correction the person may need to make) and the sentence restates the
 * amount; **Back** returns to the form, the primary button sends it. The form's own `id` is
 * `order-dialog-form` — there is only ever one open.
 */
export function OrderDialog({
  open,
  onOpenChange,
  title,
  description,
  onSubmit,
  submitLabel,
  pendingLabel = 'Working…',
  pending,
  rootError,
  confirmation,
  onBack,
  variant = 'primary',
  children,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: ReactNode;
  onSubmit: FormEventHandler<HTMLFormElement>;
  submitLabel: string;
  pendingLabel?: string;
  pending: boolean;
  rootError?: string;
  /** The restated consequence (UI-3); while set, the form is hidden. */
  confirmation?: ReactNode;
  onBack?: () => void;
  variant?: 'primary' | 'destructive';
  children: ReactNode;
}) {
  const returnFocusRef = useDialogReturnFocus(open);
  return (
    <Dialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      <DialogContent returnFocusRef={returnFocusRef} className="max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        <form id="order-dialog-form" onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
          <FormRootError message={rootError} />
          {confirmation && (
            <p className="rounded-[var(--radius-sm)] bg-accent-soft p-3 text-sm font-semibold text-accent-ink">
              {confirmation}
            </p>
          )}
          <div hidden={!!confirmation} className="flex flex-col gap-4">
            {children}
          </div>
        </form>
        <DialogFooter>
          {confirmation && onBack ? (
            <Button variant="secondary" onClick={onBack} disabled={pending}>
              Back
            </Button>
          ) : (
            <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={pending}>
              Cancel
            </Button>
          )}
          <Button
            type="submit"
            form="order-dialog-form"
            variant={variant}
            disabled={pending}
            aria-busy={pending}
          >
            {pending ? pendingLabel : submitLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
