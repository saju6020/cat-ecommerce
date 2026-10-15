'use client';

import type { ReactNode } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogTitle,
  useDialogReturnFocus,
} from '@/components/ui/dialog';
import { cn } from '@/lib/utils';

/**
 * `SideSheet` — HO-EMR-002 ADR-FE-049. The "right-side sheet" the history drawer and the diagnostic
 * order drawer share: `components/ui/dialog.tsx`'s Radix dialog (focus trapped, `Escape` closes, focus
 * returns to whatever opened it) anchored to the right edge, FULL SCREEN below 1024 px (UI-1). It sits
 * over the still-mounted workspace, so opening it is never a navigation and cannot cost the draft.
 */
export function SideSheet({
  open,
  onOpenChange,
  title,
  description,
  children,
  className,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  children: ReactNode;
  className?: string;
}) {
  const returnFocusRef = useDialogReturnFocus(open);
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        variant="sheet-left"
        returnFocusRef={returnFocusRef}
        className={cn(
          'left-auto right-0 w-full max-w-none border-l border-r-0 p-4 sm:p-6 lg:max-w-2xl',
          className,
        )}
      >
        <DialogTitle className="mb-1 pr-8 font-display text-xl font-semibold text-ink">
          {title}
        </DialogTitle>
        {description ? (
          <DialogDescription className="mb-4 text-sm text-ink-muted">
            {description}
          </DialogDescription>
        ) : (
          <DialogDescription className="sr-only">{title}</DialogDescription>
        )}
        {children}
      </DialogContent>
    </Dialog>
  );
}
