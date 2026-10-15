'use client';

import type { ReactNode } from 'react';
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

/**
 * `CatalogueDialog` — the shell the three catalogue editors share (HO-EMR-002 §11.8): a
 * `ui/dialog.tsx` dialog with a title, a description, a scrollable form body and Save / Cancel. The
 * form body owns its values and errors; this only frames it (focus is trapped and returns).
 */
export function CatalogueDialog({
  open,
  onOpenChange,
  title,
  description,
  formId,
  pending,
  rootError,
  children,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  formId: string;
  pending: boolean;
  rootError: string | null;
  children: ReactNode;
}) {
  const returnFocusRef = useDialogReturnFocus(open);
  return (
    <Dialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      <DialogContent
        returnFocusRef={returnFocusRef}
        className="max-h-[92vh] max-w-xl overflow-y-auto"
      >
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription className={description ? undefined : 'sr-only'}>
            {description ?? title}
          </DialogDescription>
        </DialogHeader>
        {children}
        {rootError && (
          <p role="alert" className="mt-3 text-sm font-semibold text-danger-ink">
            {rootError}
          </p>
        )}
        <DialogFooter className="mt-4">
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={pending}>
            Cancel
          </Button>
          <Button type="submit" form={formId} disabled={pending} aria-busy={pending}>
            {pending ? 'Saving…' : 'Save'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
