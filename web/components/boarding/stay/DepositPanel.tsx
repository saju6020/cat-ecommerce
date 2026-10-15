'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Button, buttonVariants } from '@/components/ui/button';
import { DepositStateBadge } from '@/components/boarding/shared/StatusBadges';
import { PayByCountdown } from '@/components/boarding/shared/PayByCountdown';
import { describeBoardingError } from '@/lib/api/errors';
import type { StaffStayDetail } from '@/lib/api/types';
import { formatMoney } from '@/lib/format/money';
import { useConfirmStay } from '@/lib/hooks/useStaffBoarding';
import { cn } from '@/lib/utils';

/**
 * `components/boarding/stay/DepositPanel.tsx` — ⭐ HO-BRD-002 §11.6, US-BRD-06, ADR-FE-106. The
 * deposit as staff see it: required, accepted and outstanding, the pay-by time, and a state. The
 * flow is **Record payment** (a link to Billing's payment screen for this customer — HO-BIL-002 owns
 * the dialog; nothing is entered into Boarding), then **Confirm stay**. A `DepositNotYetReceived` is
 * said with the outstanding amount; a payment awaiting a second approval says so and the button
 * becomes **Check again**. The stay is never called confirmed until the server says so.
 */
export function DepositPanel({ stay }: { stay: StaffStayDetail }) {
  const confirm = useConfirmStay(stay.id);
  const [message, setMessage] = useState<{ tone: 'ok' | 'bad'; text: string } | null>(null);
  const awaiting = stay.status === 'Reserved';
  const canConfirm = stay.allowedTransitions.includes('Confirm');
  const pending = stay.deposit.state === 'PendingApproval';

  async function run() {
    setMessage(null);
    try {
      const result = await confirm.mutateAsync();
      setMessage(
        result.status === 'Confirmed'
          ? { tone: 'ok', text: 'Confirmed — the owner has been notified.' }
          : {
              tone: 'ok',
              text: 'A payment is waiting for a second approval in Billing. The reservation won’t expire while it is.',
            },
      );
    } catch (e) {
      setMessage({ tone: 'bad', text: describeBoardingError(e) });
    }
  }

  return (
    <section
      aria-labelledby="staff-deposit-heading"
      className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      <h2 id="staff-deposit-heading" className="font-display text-lg font-semibold text-ink">
        Deposit
      </h2>
      <dl className="grid gap-x-6 gap-y-1 text-sm sm:grid-cols-[9rem_1fr]">
        {stay.status === 'Enquiry' ? (
          <>
            <dt className="font-semibold text-ink-muted">Required</dt>
            <dd>Worked out when the stay is reserved</dd>
          </>
        ) : (
          <>
            <dt className="font-semibold text-ink-muted">Required</dt>
            <dd>{formatMoney(stay.deposit.required)}</dd>
            <dt className="font-semibold text-ink-muted">Accepted</dt>
            <dd>{formatMoney(stay.deposit.accepted)}</dd>
            <dt className="font-semibold text-ink-muted">Outstanding</dt>
            <dd className="font-semibold">{formatMoney(stay.deposit.outstanding)}</dd>
            <dt className="font-semibold text-ink-muted">Status</dt>
            <dd>
              <DepositStateBadge state={stay.deposit.state} stayStatus={stay.status} />
            </dd>
          </>
        )}
      </dl>
      {awaiting && stay.deposit.payBy && !pending && (
        <PayByCountdown payBy={stay.deposit.payBy} zone={stay.branchTimeZoneId} />
      )}
      {pending && (
        <p role="status" className="text-sm font-semibold text-ink">
          A payment is waiting for a second approval in Billing. The reservation won’t expire while
          it is.
        </p>
      )}
      {awaiting && (
        <div className="flex flex-wrap items-center gap-3">
          <Link
            href={`/staff/billing/payments/new?customerId=${encodeURIComponent(stay.customerId)}`}
            className={cn(buttonVariants({ variant: 'secondary' }))}
          >
            Record payment in Billing
          </Link>
          {canConfirm && (
            <Button
              onClick={() => void run()}
              disabled={confirm.isPending}
              aria-busy={confirm.isPending}
            >
              {confirm.isPending ? 'Checking…' : pending ? 'Check again' : 'Confirm stay'}
            </Button>
          )}
        </div>
      )}
      {awaiting && (
        <p className="text-xs text-ink-muted">
          Record the deposit in Billing against this stay (reference {stay.id.slice(0, 8)}), then
          press Confirm stay. Boarding only reads what Billing has recorded.
        </p>
      )}
      {message && (
        <p
          role={message.tone === 'bad' ? 'alert' : 'status'}
          className={`text-sm font-semibold ${message.tone === 'bad' ? 'text-danger-ink' : 'text-accent-ink'}`}
        >
          {message.text}
        </p>
      )}
    </section>
  );
}
