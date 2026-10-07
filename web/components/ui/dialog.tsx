'use client';

import * as React from 'react';
import * as DialogPrimitive from '@radix-ui/react-dialog';
import { X } from 'lucide-react';
import { cn } from '@/lib/utils';

/**
 * shadcn/ui-style Dialog — copied in and owned (HO-CAT-002 §3.1, §9), wrapping
 * `@radix-ui/react-dialog` so focus-trapping, `Escape`-to-close and focus-return-to-trigger come
 * for free (HO-CAT-002 §8.5 — "shadcn/ui's Radix primitives give this... hand-rolled dialogs do
 * not"). `components/common/ConfirmDialog.tsx` is built on this; area-specific dialogs
 * (MarkDeceasedDialog, TransferOwnershipDialog, WitnessDialog, ...) should be too rather than a
 * second hand-rolled modal.
 */
export const Dialog = DialogPrimitive.Root;
export const DialogTrigger = DialogPrimitive.Trigger;
export const DialogPortal = DialogPrimitive.Portal;
export const DialogClose = DialogPrimitive.Close;

export const DialogOverlay = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Overlay>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Overlay>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Overlay
    ref={ref}
    className={cn('fixed inset-0 z-50 bg-ink/40', className)}
    {...props}
  />
));
DialogOverlay.displayName = DialogPrimitive.Overlay.displayName;

/**
 * Radix's own focus-return only re-focuses an actual `<DialogTrigger>` (it reads a `triggerRef`
 * that only that component sets internally) — HO-CAT-002 §8.5's "returns focus to its trigger"
 * otherwise silently does nothing for the common case of a dialog opened from a plain button via
 * controlled `open` state (every `ConfirmDialog` caller; most area-specific dialogs). Call this
 * with the dialog's `open` prop and pass the returned ref to `<DialogContent returnFocusRef>`.
 *
 * Captures `document.activeElement` **during render**, the instant `open` flips to `true` — not
 * in a `useEffect`, because Radix's own focus-trap effect (a descendant of wherever this hook is
 * called) runs its "move focus into the dialog" effect first (child effects fire before parent
 * effects), which would have already stolen focus by the time a same-level effect here ran.
 */
export function useDialogReturnFocus(open: boolean): React.RefObject<HTMLElement | null> {
  const wasOpenRef = React.useRef(open);
  const returnFocusRef = React.useRef<HTMLElement | null>(null);

  if (open !== wasOpenRef.current) {
    wasOpenRef.current = open;
    if (open && typeof document !== 'undefined') {
      returnFocusRef.current = document.activeElement as HTMLElement | null;
    }
  }

  return returnFocusRef;
}

export const DialogContent = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Content>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Content> & {
    /** Hides the default top-right close control — ConfirmDialog does, since it always has an
     *  explicit Cancel button and a blocking dialog (US-ADM-07) must have no other way out. */
    hideClose?: boolean;
    /** From `useDialogReturnFocus(open)` — restores focus there on close instead of relying on
     *  Radix's `<DialogTrigger>`-only default. Omit only when this Content really does sit
     *  inside a `<DialogTrigger>`. */
    returnFocusRef?: React.RefObject<HTMLElement | null>;
    /** `'center'` (default) is the usual dialog, centred in the viewport. `'sheet-left'` docks
     *  Content to the left edge, full height, instead — StaffShell's below-`sm` nav sheet
     *  (HO-CAT-002 TEST gate, 7th pass) is the only current user. Everything else (portal,
     *  overlay, focus trap, `Escape`, focus return) is identical between the two; only position
     *  and sizing differ. */
    variant?: 'center' | 'sheet-left';
    /** SEC gate LOW (first pass), integrator-authorised, minimal and additive — overrides the
     *  default `bg-ink/40` (40% tint) overlay. `IdleLock`'s own lock overlay is the one current
     *  user: the locked page stays mounted and readable behind the default translucent overlay
     *  (ADR-FE-010), which on an unattended terminal means a customer's name, phone numbers,
     *  addresses and medical data stay legible on screen after the session has timed out — an
     *  opaque fill closes that without touching the DOM/focus-trap/Escape behaviour every other
     *  dialog in the app still gets unchanged. */
    overlayClassName?: string;
  }
>(
  (
    {
      className,
      children,
      hideClose,
      returnFocusRef,
      variant = 'center',
      overlayClassName,
      onCloseAutoFocus,
      ...props
    },
    ref,
  ) => (
    <DialogPortal>
      <DialogOverlay className={overlayClassName} />
      <DialogPrimitive.Content
        ref={ref}
        className={cn(
          'z-50 focus:outline-none',
          variant === 'center' &&
            'fixed left-1/2 top-1/2 w-full max-w-md -translate-x-1/2 -translate-y-1/2 rounded-[var(--radius-md)] border border-border bg-surface p-6 shadow-modal',
          variant === 'sheet-left' &&
            'fixed inset-y-0 left-0 h-full w-60 max-w-[85vw] overflow-y-auto border-r border-border bg-surface p-4 shadow-modal',
          className,
        )}
        onCloseAutoFocus={
          returnFocusRef
            ? (event) => {
                onCloseAutoFocus?.(event);
                if (!event.defaultPrevented) {
                  event.preventDefault();
                  returnFocusRef.current?.focus();
                }
              }
            : onCloseAutoFocus
        }
        {...props}
      >
        {children}
        {!hideClose && (
          <DialogPrimitive.Close
            className="absolute right-4 top-4 rounded-[var(--radius-sm)] p-1 text-ink-muted hover:bg-surface-sunken focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            aria-label="Close"
          >
            <X size={16} aria-hidden="true" />
          </DialogPrimitive.Close>
        )}
      </DialogPrimitive.Content>
    </DialogPortal>
  ),
);
DialogContent.displayName = DialogPrimitive.Content.displayName;

export function DialogHeader({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('mb-4 flex flex-col gap-1.5', className)} {...props} />;
}

export function DialogFooter({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('mt-6 flex flex-wrap justify-end gap-2', className)} {...props} />;
}

export const DialogTitle = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Title>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Title>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Title
    ref={ref}
    className={cn('font-display text-lg font-semibold text-ink', className)}
    {...props}
  />
));
DialogTitle.displayName = DialogPrimitive.Title.displayName;

export const DialogDescription = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Description>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Description>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Description
    ref={ref}
    className={cn('text-sm text-ink-muted', className)}
    {...props}
  />
));
DialogDescription.displayName = DialogPrimitive.Description.displayName;
