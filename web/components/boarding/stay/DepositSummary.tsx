import { DepositStateBadge } from '@/components/boarding/shared/StatusBadges';
import { PayByCountdown } from '@/components/boarding/shared/PayByCountdown';
import { DEPOSIT_STATE_HELP } from '@/components/boarding/shared/labels';
import type { BoardingStayStatus, StayDeposit } from '@/lib/api/types';
import type { BoardingMoney } from '@/lib/api/types';
import { formatMoney } from '@/lib/format/money';
import { HowToPay } from '@/components/boarding/booking/HowToPay';

/**
 * `components/boarding/stay/DepositSummary.tsx` — HO-BRD-002 §11.3, U-B1, US-BRD-06: the owner's
 * view of the deposit — the amount, its state in plain language (*Awaiting payment* / *Payment being
 * checked* / *Deposit received*), the pay-by countdown while the stay is `Reserved`, and how to pay.
 * It describes how to pay offline and takes nothing (ADR-FE-106).
 */
export function DepositSummary({
  status,
  deposit,
  balanceDue,
  zone,
}: {
  status: BoardingStayStatus;
  deposit: StayDeposit;
  balanceDue: BoardingMoney | null;
  zone: string;
}) {
  return (
    <section aria-labelledby="deposit-heading" className="flex flex-col gap-3">
      <h2 id="deposit-heading" className="font-display text-lg font-semibold text-ink">
        Deposit
      </h2>
      <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-[10rem_1fr]">
        <dt className="font-semibold text-ink-muted">Deposit</dt>
        {status === 'Enquiry' ? (
          <dd className="font-semibold text-ink">Worked out when you reserve</dd>
        ) : (
          <>
            <dd className="font-semibold text-ink">{formatMoney(deposit.required)}</dd>
            <dt className="font-semibold text-ink-muted">Status</dt>
            <dd>
              <DepositStateBadge state={deposit.state} />
            </dd>
          </>
        )}
        {balanceDue && (
          <>
            <dt className="font-semibold text-ink-muted">Balance due</dt>
            <dd className="text-ink">{formatMoney(balanceDue)}</dd>
          </>
        )}
      </dl>
      <p className="text-sm text-ink-muted">
        {status === 'Enquiry'
          ? 'The deposit is worked out by the clinic when you reserve; nothing is owed yet.'
          : DEPOSIT_STATE_HELP[deposit.state]}
      </p>
      {status === 'Reserved' && deposit.payBy && deposit.state !== 'PendingApproval' && (
        <PayByCountdown payBy={deposit.payBy} zone={zone} />
      )}
      {status === 'Reserved' && deposit.state === 'Awaiting' && <HowToPay />}
    </section>
  );
}
