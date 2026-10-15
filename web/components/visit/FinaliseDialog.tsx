'use client';

import { useRef, useState } from 'react';
import { CircleCheck, Circle, ShieldAlert } from 'lucide-react';
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
import { ApiForbiddenError } from '@/lib/api/client';
import { describeClinicalError } from '@/lib/api/errors';
import type { ChecklistItem } from '@/lib/validation/clinical';

/**
 * ⭐ `FinaliseDialog` — HO-EMR-002 §11.1 (US-CLN-17, 22), one dialog in two states (blocked /
 * ready, per the design). A checklist from the CURRENT form (≥ 1 problem, Assessment, Plan — a
 * hint; the server decides, §20.2) whose missing items link to their field; a warning when
 * diagnostic orders are still pending; unsaved changes are saved first by the caller; and the UI-3
 * sentence naming what becomes permanent. A server refusal (`ProblemRequired`, a stale `409`, `403`)
 * is shown inline — a `403` as the permission text, never as a dead button. The dialog is built on
 * `components/ui/dialog.tsx`, so focus is trapped and returns to the trigger.
 */
export function FinaliseDialog({
  open,
  onOpenChange,
  catName,
  checklist,
  pendingOrders,
  hasUnsavedChanges,
  onConfirm,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  catName: string;
  checklist: ChecklistItem[];
  pendingOrders: number;
  hasUnsavedChanges: boolean;
  /** Saves first if needed, then finalises. Rejects with the server's error. */
  onConfirm: () => Promise<void>;
}) {
  const returnFocusRef = useDialogReturnFocus(open);
  // A checklist link moves focus to a form field; the dialog must not then hand focus back to the
  // Finalise button it was opened from.
  const goingToField = useRef(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<{ text: string; forbidden: boolean } | null>(null);
  const ready = checklist.every((item) => item.done);

  async function confirm() {
    setPending(true);
    setError(null);
    try {
      await onConfirm();
      onOpenChange(false);
    } catch (caught) {
      setError({
        text:
          caught instanceof Error && caught.name === 'FinaliseBlocked'
            ? caught.message
            : describeClinicalError(caught),
        forbidden: caught instanceof ApiForbiddenError,
      });
    } finally {
      setPending(false);
    }
  }

  function goTo(fieldId: string) {
    goingToField.current = true;
    onOpenChange(false);
    requestAnimationFrame(() => document.getElementById(fieldId)?.focus());
  }

  return (
    <Dialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      <DialogContent
        returnFocusRef={returnFocusRef}
        onCloseAutoFocus={(event) => {
          if (goingToField.current) event.preventDefault();
          goingToField.current = false;
        }}
        className="max-h-[90vh] overflow-y-auto"
      >
        <DialogHeader>
          <DialogTitle>Finalise {catName}’s visit?</DialogTitle>
          <DialogDescription>
            The note, problems, vitals, treatments and photos become permanent. Corrections will be
            added as addenda.
          </DialogDescription>
        </DialogHeader>

        <ul
          aria-label="Before you finalise"
          className="m-0 mb-3 flex list-none flex-col gap-1.5 p-0"
        >
          {checklist.map((item) => (
            <li key={item.id} className="flex items-center gap-2 text-sm">
              {item.done ? (
                <CircleCheck size={16} className="flex-none text-accent-ink" aria-hidden="true" />
              ) : (
                <Circle size={16} className="flex-none text-ink-muted" aria-hidden="true" />
              )}
              {item.done ? (
                <span className="text-ink">{item.label} (done)</span>
              ) : (
                <button
                  type="button"
                  onClick={() => goTo(item.fieldId)}
                  className="text-left font-semibold text-accent-ink underline underline-offset-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                >
                  {item.label} (missing — go to it)
                </button>
              )}
            </li>
          ))}
        </ul>

        {pendingOrders > 0 && (
          <p
            role="status"
            className="mb-3 rounded-[var(--radius-sm)] bg-warning-bg p-2.5 text-sm text-warning-ink"
          >
            {pendingOrders} diagnostic {pendingOrders === 1 ? 'order is' : 'orders are'} still
            pending — results can be added after finalising.
          </p>
        )}
        {hasUnsavedChanges && (
          <p className="mb-3 text-sm text-ink-muted">
            Your unsaved changes will be saved first, then the visit is finalised.
          </p>
        )}
        {error && (
          <p
            role="alert"
            className="mb-3 flex items-start gap-2 text-sm font-semibold text-danger-ink"
          >
            {error.forbidden && (
              <ShieldAlert size={16} className="mt-0.5 flex-none" aria-hidden="true" />
            )}
            {error.text}
          </p>
        )}

        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={pending}>
            Cancel
          </Button>
          <Button onClick={confirm} disabled={!ready || pending} aria-busy={pending}>
            {pending ? 'Finalising…' : 'Finalise visit'}
          </Button>
        </DialogFooter>
        {!ready && (
          <p className="mt-2 text-xs text-ink-muted">
            Finalise stays unavailable until the items above are done.
          </p>
        )}
      </DialogContent>
    </Dialog>
  );
}
