'use client';

import { useState } from 'react';
import { Amount } from '@/components/billing/Amount';
import { FormRootError, TextField } from '@/components/billing/Fields';
import { guardedText } from '@/components/billing/schemas';
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
import type { TransferInstructions } from '@/lib/api/checkout';
import { describeOrderingError } from '@/lib/api/errors';
import { CARD_NUMBER_MESSAGE, looksLikeCardNumber } from '@/lib/validation/cardNumberGuard';
import { CopyButton } from './CopyButton';
import { TransferCountdown } from './TransferCountdown';

const MAX_REFERENCE = 100;

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 border-b border-dashed border-border py-2 text-sm last:border-0">
      <dt className="text-ink-muted">{label}</dt>
      <dd className="m-0 flex items-center gap-2">
        <span className="font-semibold text-ink [overflow-wrap:anywhere]">{value}</span>
        <CopyButton value={value} label={label.toLowerCase()} />
      </dd>
    </div>
  );
}

/**
 * `components/orders/BankTransferInstructions.tsx` — ⭐ HO-ORD-002 §11.5, ADR-FE-096, US-ORD-19 AC1. The
 * bank-transfer instructions as a first-class screen: the shop's account details (each with **Copy**
 * and a visible value), the **payment reference** (the order number) shown prominently, the amount
 * due, a countdown to the deadline on the server clock, and *"I've made the transfer"* — which submits
 * the customer's own bank reference (≤ 100 characters, never a card number). Nothing here is editable
 * by the client; the account and amount are the server's.
 */
export function BankTransferInstructions({
  instructions,
  paymentDueBy,
  zone,
  reportedReference,
  onSubmitReference,
  onExpired,
}: {
  instructions: TransferInstructions;
  paymentDueBy: string | null;
  zone: string;
  /** The reference the customer already reported, if any. */
  reportedReference: string | null;
  onSubmitReference: (reference: string) => Promise<void>;
  /** The deadline passed — the page refetches the order. */
  onExpired?: () => void;
}) {
  const [open, setOpen] = useState(false);
  const [reference, setReference] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [done, setDone] = useState(false);
  const returnFocusRef = useDialogReturnFocus(open);
  const reported = done || reportedReference !== null;

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const text = reference.trim();
    const parsed = guardedText(MAX_REFERENCE).safeParse(text);
    if (!text) return setError('Enter the reference your bank gave the transfer.');
    if (!parsed.success) {
      return setError(looksLikeCardNumber(text) ? CARD_NUMBER_MESSAGE : `Use at most ${MAX_REFERENCE} characters.`);
    }
    setError(null);
    setPending(true);
    try {
      await onSubmitReference(text);
      setDone(true);
      setOpen(false);
    } catch (failure) {
      setError(describeOrderingError(failure));
    } finally {
      setPending(false);
    }
  }

  return (
    <section
      aria-labelledby="transfer-heading"
      className="flex flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-4 sm:p-5"
    >
      <h2 id="transfer-heading" className="font-display text-lg font-semibold text-ink">
        Pay by bank transfer
      </h2>
      <div className="rounded-[var(--radius-sm)] border border-accent/30 bg-accent-soft p-3">
        <p className="text-xs font-bold uppercase tracking-wide text-accent-ink">Payment reference</p>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="font-display text-2xl font-semibold text-accent-ink [overflow-wrap:anywhere]">
            {instructions.paymentReference}
          </p>
          <CopyButton value={instructions.paymentReference} label="payment reference" />
        </div>
        <p className="mt-1 text-sm text-ink-muted">
          Put this in the transfer&rsquo;s reference or note so we can match your payment.
        </p>
      </div>
      <p className="text-sm">
        Amount to transfer:{' '}
        <b className="text-base text-ink">
          <Amount money={instructions.amountDue} />
        </b>
      </p>
      <dl className="m-0">
        <Row label="Account name" value={instructions.accountName} />
        <Row label="Account number" value={instructions.accountNumber} />
        <Row label="Bank" value={instructions.bankName} />
        <Row label="Branch" value={instructions.branchName} />
        {instructions.routingNumber && <Row label="Routing number" value={instructions.routingNumber} />}
      </dl>
      {paymentDueBy && <TransferCountdown dueBy={paymentDueBy} zone={zone} onExpired={onExpired} />}
      {reported ? (
        <p role="status" className="rounded-[var(--radius-sm)] bg-accent-soft p-3 text-sm font-semibold text-accent-ink">
          Thanks — we&rsquo;ll confirm within one working day.
          {reportedReference ? ` Your reference: ${reportedReference}.` : ''}
        </p>
      ) : null}
      <div>
        <Button variant={reported ? 'secondary' : 'primary'} onClick={() => setOpen(true)}>
          {reported ? 'Change my reference' : 'I’ve made the transfer'}
        </Button>
      </div>
      <Dialog open={open} onOpenChange={(next) => !pending && setOpen(next)}>
        <DialogContent returnFocusRef={returnFocusRef}>
          <DialogHeader>
            <DialogTitle>Tell us about your transfer</DialogTitle>
            <DialogDescription>
              Enter the reference your bank gave the transfer, so we can find it quickly. Never enter a card
              number.
            </DialogDescription>
          </DialogHeader>
          <form id="transfer-reference-form" onSubmit={(event) => void submit(event)} noValidate className="flex flex-col gap-3">
            <FormRootError message={error ?? undefined} />
            <TextField
              id="transfer-reference"
              label="Your bank’s reference"
              required
              maxLength={MAX_REFERENCE}
              autoComplete="off"
              value={reference}
              onChange={(event) => setReference(event.target.value)}
            />
          </form>
          <DialogFooter>
            <Button variant="secondary" onClick={() => setOpen(false)} disabled={pending}>
              Cancel
            </Button>
            <Button type="submit" form="transfer-reference-form" disabled={pending} aria-busy={pending}>
              {pending ? 'Sending…' : 'Send reference'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </section>
  );
}
