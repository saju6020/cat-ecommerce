'use client';

import { useState } from 'react';
import { Amount } from '@/components/billing/Amount';
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
import type { Money } from '@/lib/format/money';
import { OFFLINE_CANT_SAVE_LABEL, useWriteEnabled } from '@/lib/hooks/useConnectivity';

/**
 * `components/orders/CancelOrderDialog.tsx` — HO-ORD-002 §11.6, UI-3 (docs/design/orders/CancelOrder.dc.html).
 * Names the order, says what happens ("Your reserved items are released.") and, when money has been
 * received, that the shop refunds it and how much was received. Neutral colours and a red confirm: a
 * cancellation is a consequence, so no warm band and no cat. Built like `ConfirmDialog` (offline switches
 * the confirm off with a visible reason; the dialog stays open until the request settles) but with the
 * Hearth shapes; it portals out of the page, so it carries `data-theme="customer"` itself.
 */
export function CancelOrderDialog({
  open,
  onOpenChange,
  orderNumber,
  amountPaid,
  onConfirm,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  orderNumber: string;
  /** The amount already received, or `null` when nothing has been paid. */
  amountPaid: Money | null;
  onConfirm: () => Promise<void>;
}) {
  const [isPending, setIsPending] = useState(false);
  const returnFocusRef = useDialogReturnFocus(open);
  const writeEnabled = useWriteEnabled();

  async function handleConfirm() {
    setIsPending(true);
    try {
      await onConfirm();
    } finally {
      setIsPending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(next) => !isPending && onOpenChange(next)}>
      <DialogContent
        hideClose
        data-theme="customer"
        returnFocusRef={returnFocusRef}
        className="max-w-[420px] rounded-3xl p-7"
      >
        <DialogHeader className="gap-2.5">
          <p className="m-0 text-[11px] font-bold uppercase tracking-[0.07em] text-ink-muted">
            {amountPaid ? 'Money already received' : 'Nothing paid yet'}
          </p>
          <DialogTitle className="text-2xl leading-tight">{`Cancel order ${orderNumber}?`}</DialogTitle>
          <DialogDescription>
            Your reserved items are released. Other customers can then buy them. This can&rsquo;t be
            undone.
          </DialogDescription>
        </DialogHeader>
        {amountPaid && (
          <p className="m-0 rounded-[14px] bg-surface-sunken px-4 py-3 text-sm">
            <b>Any money you have paid will be refunded by the shop.</b> We received{' '}
            <Amount money={amountPaid} />.
          </p>
        )}
        <DialogFooter className="mt-4">
          {!writeEnabled && (
            <p className="w-full text-sm text-ink-muted">{OFFLINE_CANT_SAVE_LABEL}</p>
          )}
          <Button
            variant="pillSecondary"
            size="pillMd"
            onClick={() => onOpenChange(false)}
            disabled={isPending}
          >
            Keep order
          </Button>
          <Button
            variant="destructive"
            size="pillMd"
            onClick={handleConfirm}
            disabled={isPending || !writeEnabled}
            aria-busy={isPending}
          >
            {isPending ? 'Cancelling…' : 'Cancel order'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
