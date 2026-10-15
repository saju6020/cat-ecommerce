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
import { Banner, TextAreaField, TextField } from '@/components/inventory/fields';
import { ExpiryBadge } from '@/components/stock/ExpiryBadge';
import { describeInventoryError } from '@/lib/api/errors';
import { formatQuantity } from '@/lib/format/inventory';
import type { BatchDetail } from '@/lib/api/inventory-stock';
import type { DeclareRecallRequest } from '@/lib/api/inventory-recalls';

/**
 * `components/recall/DeclareRecallDialog.tsx` — HO-IMP-002 §11.7, US-IMP-11 AC1, ADR-FE-046. Opened
 * from a batch. Shows the batch, its lot, expiry and where its stock is; the button stays disabled
 * until the person TYPES THE LOT NUMBER exactly; two texts are required — the reason (internal) and
 * the advice customers will receive. The consequence is stated plainly and nobody is messaged here:
 * notifying is a separate, later step. Built on `components/ui/dialog.tsx`.
 */
export function DeclareRecallDialog({
  open,
  onOpenChange,
  batch,
  onDeclare,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  batch: BatchDetail;
  onDeclare: (request: DeclareRecallRequest) => Promise<unknown>;
}) {
  const [typedLot, setTypedLot] = React.useState('');
  const [reason, setReason] = React.useState('');
  const [advice, setAdvice] = React.useState('');
  const [error, setError] = React.useState<string | null>(null);
  const [pending, setPending] = React.useState(false);
  const returnFocusRef = useDialogReturnFocus(open);

  React.useEffect(() => {
    if (!open) {
      setTypedLot('');
      setReason('');
      setAdvice('');
      setError(null);
    }
  }, [open]);

  const lotMatches = typedLot === batch.lotNumber;
  const canDeclare = lotMatches && reason.trim() !== '' && advice.trim() !== '';

  async function declare() {
    setPending(true);
    setError(null);
    try {
      await onDeclare({ confirmLotNumber: typedLot, reason: reason.trim(), customerAdvice: advice.trim() });
      onOpenChange(false);
    } catch (caught) {
      setError(describeInventoryError(caught));
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      <DialogContent hideClose returnFocusRef={returnFocusRef} className="max-h-[90vh] max-w-xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Declare a recall of lot {batch.lotNumber}</DialogTitle>
          <DialogDescription>
            All remaining stock of lot {batch.lotNumber} becomes unusable immediately, everywhere. This cannot be undone — only withdrawn.
          </DialogDescription>
        </DialogHeader>
        <div className="flex flex-col gap-3 text-sm">
          <div className="rounded-[var(--radius-sm)] bg-surface-sunken p-3">
            <p className="font-semibold text-ink">
              {batch.displayName} <span className="font-normal text-ink-muted">— {batch.skuCode}</span>
            </p>
            <p className="mt-1">Lot {batch.lotNumber}</p>
            <div className="mt-1">
              <ExpiryBadge expiresOn={batch.expiresOn} isExpired={batch.isExpired} daysToExpiry={batch.daysToExpiry} />
            </div>
            <p className="mt-2 text-ink-muted">Where it is now:</p>
            <ul className="list-disc pl-5">
              {batch.positions.length === 0 && <li>No stock is held.</li>}
              {batch.positions.map((p) => (
                <li key={p.locationId}>
                  {p.locationCode}: {formatQuantity(p.onHand, batch.stockUnit)}
                </li>
              ))}
            </ul>
          </div>
          <Banner tone="neutral">No one is messaged by declaring a recall. Notifying customers is a separate step you take afterwards.</Banner>
          <TextAreaField id="recall-reason" label="Reason (internal)" required value={reason} onChange={(e) => setReason(e.target.value)} />
          <TextAreaField id="recall-advice" label="Advice to customers" required hint="This is the text customers receive when you notify them." value={advice} onChange={(e) => setAdvice(e.target.value)} />
          <TextField
            id="recall-confirm-lot"
            label={`Type the lot number, ${batch.lotNumber}, to confirm`}
            required
            autoComplete="off"
            value={typedLot}
            error={typedLot !== '' && !lotMatches ? "The lot number you typed doesn't match." : undefined}
            onChange={(e) => setTypedLot(e.target.value)}
          />
        </div>
        {error && (
          <p role="alert" className="mt-3 text-sm font-semibold text-danger-ink">
            {error}
          </p>
        )}
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={pending}>
            Cancel
          </Button>
          <Button variant="destructive" onClick={() => void declare()} disabled={!canDeclare || pending} aria-busy={pending}>
            {pending ? 'Declaring…' : 'Declare recall'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
