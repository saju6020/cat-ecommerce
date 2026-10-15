'use client';

import Link from 'next/link';
import { Hourglass } from 'lucide-react';
import { DepositStateBadge } from '@/components/boarding/shared/StatusBadges';
import { PayByCountdown } from '@/components/boarding/shared/PayByCountdown';
import type { BoardingStayBase } from '@/lib/api/types';
import { formatMoney } from '@/lib/format/money';
import { portalStayHref } from '@/lib/routing/boarding-links';
import { HowToPay } from './HowToPay';

/**
 * `components/boarding/booking/ReservedPanel.tsx` — HO-BRD-002 §11.1 step 7, US-BRD-06 AC1/AC2:
 * *"Reserved — not yet confirmed."* The deposit amount, the pay-by countdown, **how to pay** (the
 * content page), the promise that the clinic confirms once it records the payment, and a link to the
 * stay. There is **no payment control anywhere in this panel** (ADR-FE-106, U-A9).
 */
export function ReservedPanel({
  stay,
}: {
  stay: Pick<BoardingStayBase, 'id' | 'deposit' | 'balanceDue' | 'branchTimeZoneId'>;
}) {
  return (
    <section
      aria-labelledby="reserved-heading"
      className="flex flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-6"
    >
      <div className="flex items-start gap-3">
        <Hourglass size={22} className="mt-0.5 flex-none text-warning-ink" aria-hidden="true" />
        <div>
          <h2 id="reserved-heading" className="font-display text-xl font-semibold text-ink">
            Reserved — not yet confirmed
          </h2>
          <p className="mt-1 text-sm text-ink-muted">
            We’ll confirm as soon as the clinic records your payment.
          </p>
        </div>
      </div>

      <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-[10rem_1fr]">
        <dt className="font-semibold text-ink-muted">Deposit to pay</dt>
        <dd className="text-lg font-bold text-ink">{formatMoney(stay.deposit.required)}</dd>
        <dt className="font-semibold text-ink-muted">Status</dt>
        <dd>
          <DepositStateBadge state={stay.deposit.state} />
        </dd>
        {stay.balanceDue && (
          <>
            <dt className="font-semibold text-ink-muted">Balance due</dt>
            <dd className="text-ink">{formatMoney(stay.balanceDue)}</dd>
          </>
        )}
      </dl>

      {stay.deposit.payBy && (
        <PayByCountdown payBy={stay.deposit.payBy} zone={stay.branchTimeZoneId} />
      )}

      <HowToPay />

      <p>
        <Link
          href={portalStayHref(stay.id)}
          className="text-sm font-semibold text-accent-ink underline"
        >
          View this stay
        </Link>
      </p>
    </section>
  );
}
