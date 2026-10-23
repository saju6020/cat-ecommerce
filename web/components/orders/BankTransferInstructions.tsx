'use client';

import { useState } from 'react';
import { Info, Landmark } from 'lucide-react';
import { Amount } from '@/components/billing/Amount';
import { FormRootError, TextField } from '@/components/billing/Fields';
import { guardedText } from '@/components/billing/schemas';
import { bannerInfoClass } from '@/components/hearth/card';
import { IconTile } from '@/components/hearth/IconTile';
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
import { cn } from '@/lib/utils';
import { CopyButton } from './CopyButton';
import { TransferCountdown } from './TransferCountdown';

const MAX_REFERENCE = 100;

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-2.5 border-b border-dashed border-border py-2.5 last:border-0">
      <div className="flex min-w-0 flex-col">
        <dt className="text-xs text-ink-muted">{label}</dt>
        <dd className="m-0 text-sm font-semibold text-ink [overflow-wrap:anywhere]">{value}</dd>
      </div>
      <CopyButton value={value} label={label.toLowerCase()} />
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
      return setError(
        looksLikeCardNumber(text)
          ? CARD_NUMBER_MESSAGE
          : `Use at most ${MAX_REFERENCE} characters.`,
      );
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
      className="flex flex-col gap-3.5 rounded-card border border-border bg-surface p-5 sm:px-7 sm:py-6"
    >
      <div className="flex items-start gap-3.5">
        <IconTile shape="tile" size="md">
          <Landmark size={20} aria-hidden="true" />
        </IconTile>
        <div>
          <h2 id="transfer-heading" className="m-0 font-display text-[22px] font-semibold text-ink">
            Pay by bank transfer
          </h2>
          <p className="m-0 text-[13px] text-ink-muted">
            Transfer from your own bank or mobile banking app. Use the payment reference so we can
            match it to your order.
          </p>
        </div>
      </div>
      {paymentDueBy && <TransferCountdown dueBy={paymentDueBy} zone={zone} onExpired={onExpired} />}
      <div>
        <p className="m-0 text-[11px] font-bold uppercase tracking-[0.07em] text-ink-muted">
          Payment reference
        </p>
        <div className="mt-1.5 flex flex-wrap items-center justify-between gap-2 rounded-[18px] bg-accent-tint py-2.5 pl-5 pr-2.5">
          <p className="m-0 font-mono text-[22px] font-bold text-accent-ink [overflow-wrap:anywhere]">
            {instructions.paymentReference}
          </p>
          <CopyButton value={instructions.paymentReference} label="payment reference" />
        </div>
        <p className="m-0 mt-1.5 text-[13px] text-ink-muted">
          Put this in the transfer&rsquo;s reference or note so we can match your payment.
        </p>
      </div>
      <div className="flex items-center justify-between gap-3">
        <p className="m-0">
          <span className="text-[11px] font-bold uppercase tracking-[0.07em] text-ink-muted">
            Amount to transfer
          </span>
          <br />
          <b className="text-[28px] text-ink">
            <Amount money={instructions.amountDue} />
          </b>
        </p>
        <CopyButton value={instructions.amountDue.amount} label="amount" />
      </div>
      <div>
        <p className="m-0 text-[11px] font-bold uppercase tracking-[0.07em] text-ink-muted">
          Our account
        </p>
        <dl className="m-0">
          <Row label="Account name" value={instructions.accountName} />
          <Row label="Account number" value={instructions.accountNumber} />
          <Row label="Bank" value={instructions.bankName} />
          <Row label="Branch" value={instructions.branchName} />
          {instructions.routingNumber && (
            <Row label="Routing number" value={instructions.routingNumber} />
          )}
        </dl>
      </div>
      {reported ? (
        <p role="status" className={cn(bannerInfoClass, 'm-0 font-semibold')}>
          <Info size={18} aria-hidden="true" className="mt-px flex-none" />
          <span>
            Thanks — we&rsquo;ll confirm within one working day.
            {reportedReference ? ` Your reference: ${reportedReference}.` : ''}
          </span>
        </p>
      ) : null}
      <div>
        <Button
          variant={reported ? 'pillSecondary' : 'pill'}
          size="pillMd"
          onClick={() => setOpen(true)}
        >
          {reported ? 'Change my reference' : 'I’ve made the transfer'}
        </Button>
      </div>
      <Dialog open={open} onOpenChange={(next) => !pending && setOpen(next)}>
        <DialogContent
          data-theme="customer"
          returnFocusRef={returnFocusRef}
          className="rounded-3xl p-7"
        >
          <DialogHeader>
            <DialogTitle className="text-2xl">Tell us about your transfer</DialogTitle>
            <DialogDescription>
              Enter the reference your bank gave the transfer, so we can find it quickly. Never
              enter a card number.
            </DialogDescription>
          </DialogHeader>
          <form
            id="transfer-reference-form"
            onSubmit={(event) => void submit(event)}
            noValidate
            className="flex flex-col gap-3"
          >
            <FormRootError message={error ?? undefined} />
            <TextField
              id="transfer-reference"
              label="Your bank’s reference"
              required
              maxLength={MAX_REFERENCE}
              autoComplete="off"
              hint="Up to 100 characters. It helps us find your payment faster."
              className="min-h-11 rounded-full px-4"
              value={reference}
              onChange={(event) => setReference(event.target.value)}
            />
          </form>
          <DialogFooter>
            <Button
              variant="pillSecondary"
              size="pillMd"
              onClick={() => setOpen(false)}
              disabled={pending}
            >
              Cancel
            </Button>
            <Button
              type="submit"
              form="transfer-reference-form"
              variant="pill"
              size="pillMd"
              disabled={pending}
              aria-busy={pending}
            >
              {pending ? 'Sending…' : 'Send reference'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </section>
  );
}
