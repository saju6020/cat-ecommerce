'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { FormErrorAlert } from '@/components/boarding/shared/FormBits';
import { QuoteBreakdown } from '@/components/boarding/shared/QuoteBreakdown';
import { UNIT_TYPE_LABEL } from '@/components/boarding/shared/labels';
import { CatHeaderCompact } from '@/components/boarding/shared/CatHeaderCompact';
import { describeBoardingError } from '@/lib/api/errors';
import type { BoardingStayBase } from '@/lib/api/types';
import { formatStayDates } from '@/lib/format/boarding';
import { formatMoney } from '@/lib/format/money';

/**
 * `components/boarding/booking/ReviewAndReserve.tsx` — HO-BRD-002 §11.1 step 6, US-BRD-06. Dates,
 * unit type, cats with a profile summary, the server's quote, and the boarding terms link. **Reserve**
 * opens a confirmation (UI-3) that names the unit type, the cats, the dates and the total.
 *
 * The deposit amount and the time to pay are decided by the server when the stay is reserved
 * (HO-BRD-001 §8.10) — the enquiry carries only the quote — so the review says exactly that rather
 * than working a deposit out here (ADR-FE-105). Failures that send the owner back a step
 * (`VaccinationGateBlocked`, `CareProfileIncomplete`, `UnitNoLongerAvailable`, `CatAlreadyBoarding`)
 * are handled by the flow through `onReserve`'s rejection.
 */
export function ReviewAndReserve({
  stay,
  onReserve,
  onBack,
  reserveLabel = 'Reserve',
  quotePending = false,
  termsHref = '/policies/terms',
}: {
  stay: Pick<
    BoardingStayBase,
    'arrivalOn' | 'departureOn' | 'nights' | 'requestedUnitType' | 'cats' | 'quote'
  >;
  /** Resolves on success; a rejection is shown in words unless the flow already redirected. */
  onReserve: () => Promise<void>;
  onBack: () => void;
  reserveLabel?: string;
  /** The price is being fetched again (after a reload): Reserve waits for it. */
  quotePending?: boolean;
  termsHref?: string;
}) {
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const unit = UNIT_TYPE_LABEL[stay.requestedUnitType];
  const names = stay.cats.map((c) => c.catName).join(' and ');
  const total = stay.quote ? formatMoney(stay.quote.total) : null;

  async function confirm() {
    setError(null);
    try {
      await onReserve();
      setConfirming(false);
    } catch (e) {
      setConfirming(false);
      setError(describeBoardingError(e));
    }
  }

  return (
    <section aria-labelledby="review-heading" className="flex flex-col gap-5">
      <h2 id="review-heading" className="font-display text-lg font-semibold text-ink">
        Review and reserve
      </h2>

      <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-[10rem_1fr]">
        <dt className="font-semibold text-ink-muted">Dates</dt>
        <dd className="text-ink">
          {formatStayDates(stay.arrivalOn, stay.departureOn, stay.nights)}
        </dd>
        <dt className="font-semibold text-ink-muted">Unit</dt>
        <dd className="text-ink">{unit}</dd>
      </dl>

      <ul aria-label="Cats and care profiles" className="flex flex-col gap-3">
        {stay.cats.map((cat) => (
          <li
            key={cat.catId}
            className="rounded-[var(--radius-sm)] border border-border bg-surface p-4"
          >
            <CatHeaderCompact
              name={cat.catName}
              isHighRisk={cat.catIsHighRisk}
              handlingNote={cat.handlingNote}
            />
            {cat.careProfile ? (
              <p className="mt-2 text-sm text-ink-muted">
                {cat.careProfile.feedings.length} feeding
                {cat.careProfile.feedings.length === 1 ? '' : 's'} a day ·{' '}
                {cat.careProfile.medications.length === 0
                  ? 'no medication'
                  : `${cat.careProfile.medications.length} medicine${cat.careProfile.medications.length === 1 ? '' : 's'}`}{' '}
                · vet visit {cat.careProfile.vetVisitAuthorised ? 'authorised' : 'not authorised'},
                limit {formatMoney(cat.careProfile.vetSpendingLimit)}
              </p>
            ) : (
              <p className="mt-2 text-sm font-semibold text-danger-ink">
                Care profile not finished.
              </p>
            )}
          </li>
        ))}
      </ul>

      {stay.quote && (
        <div className="rounded-[var(--radius-md)] border border-border bg-surface p-5">
          <QuoteBreakdown quote={stay.quote} />
        </div>
      )}

      <p className="text-sm text-ink-muted">
        When you reserve, we show the deposit you owe and the time you have to pay it. You pay the
        deposit at the clinic or by bank transfer — nothing is taken online. See the{' '}
        <Link href={termsHref} className="font-semibold text-accent-ink underline">
          terms and cancellation policy
        </Link>
        .
      </p>

      <FormErrorAlert message={error} />
      <div className="flex flex-wrap gap-3">
        <Button variant="secondary" onClick={onBack}>
          Back
        </Button>
        <Button onClick={() => setConfirming(true)} disabled={quotePending || !stay.quote}>
          {quotePending ? 'Loading the price…' : reserveLabel}
        </Button>
      </div>

      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title={`Reserve ${unit} for ${names}?`}
        consequence={`Reserve ${unit} for ${names}, ${formatStayDates(stay.arrivalOn, stay.departureOn, stay.nights)}${total ? `, ${total}` : ''}. The unit is held while you pay the deposit; if it is not recorded in time the reservation is released.`}
        confirmLabel="Reserve"
        pendingLabel="Reserving…"
        onConfirm={confirm}
      />
    </section>
  );
}
