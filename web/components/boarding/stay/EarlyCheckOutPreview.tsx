'use client';

import { LoadingState } from '@/components/common/LoadingState';
import { Button } from '@/components/ui/button';
import type { CheckOutPreview } from '@/lib/api/types';
import { formatMoney } from '@/lib/format/money';
import { formatBranchInstant } from '@/lib/format/boarding';

const ACTION_TEXT = {
  Keep: 'stays as it is',
  Void: 'no longer applies, removed',
  Record: 'new charge',
} as const;

/**
 * `components/boarding/stay/EarlyCheckOutPreview.tsx` — HO-BRD-002 §11.8, US-BRD-16 AC1/AC2, owner
 * decision 2026-10-14 (“show before”): the SERVER's recalculation of an early check-out, shown on the
 * summary step **before** Check out is pressed. `GET …/check-out-preview` runs the very code the
 * check-out runs (HO-BRD-001 1.1): nights charged of nights booked, each line with what happens to it
 * (kept / removed / new), the previous and new totals and the credit with its outcome. Nothing is
 * worked out here (ADR-FE-105).
 */
export function EarlyCheckOutPreview({
  query,
  zone = 'Asia/Dhaka',
  onRetry,
}: {
  query: { data: CheckOutPreview | undefined; isPending: boolean; isError: boolean };
  zone?: string;
  onRetry?: () => void;
}) {
  if (query.isPending)
    return <LoadingState variant="card" rows={3} label="Recalculating the charges…" />;
  if (query.isError || !query.data) {
    return (
      <div
        role="alert"
        className="flex flex-col gap-2 rounded-[var(--radius-sm)] bg-warning-bg px-3 py-2 text-sm text-warning-ink"
      >
        <p className="font-semibold">We couldn’t load the server’s recalculation of the charges.</p>
        <p>
          If this stay is leaving early or late, the server still recalculates the nights and the
          charges when you check out, and shows the result straight after.
        </p>
        {onRetry && (
          <div>
            <Button size="sm" variant="secondary" onClick={onRetry}>
              Try again
            </Button>
          </div>
        )}
      </div>
    );
  }
  const data = query.data;
  if (!data.isEarly && !data.isLate) return null;
  // The server's own outcome decides whether a credit or refund is shown (None / Credit / Refund).
  const hasCredit = data.creditOutcome !== 'None';
  return (
    <section
      aria-labelledby="early-preview-heading"
      className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface-sunken p-4"
    >
      <h3 id="early-preview-heading" className="font-display text-base font-semibold text-ink">
        If you check out now
      </h3>
      <p className="text-sm text-ink">
        {data.isLate && !data.isEarly
          ? `This stay is past its booked departure (${data.bookedDepartureOn}). ${data.nightsCharged} nights are charged`
          : `${data.nightsCharged} of ${data.nightsBooked} nights are charged`}
        {data.quote ? `, on the ${data.quote.basis.toLowerCase()} basis` : ''}. The stay ends on{' '}
        {data.chargedDepartureOn}
        {Date.parse(data.unitFreeFrom) > Date.now()
          ? ` and the unit is free from ${formatBranchInstant(data.unitFreeFrom, zone)}`
          : ' and the unit is free now'}
        . The server recalculated these lines:
      </p>
      <ul aria-label="Recalculated charge lines" className="flex flex-col gap-1.5 text-sm">
        {data.lines.map((line) => (
          <li key={line.lineRef} className="flex justify-between gap-3">
            <span className={line.action === 'Void' ? 'text-ink-muted line-through' : 'text-ink'}>
              {line.description}{' '}
              <span className="text-xs text-ink-muted">({ACTION_TEXT[line.action]})</span>
            </span>
            <span className="whitespace-nowrap font-semibold tabular-nums">
              {formatMoney(line.net)}
            </span>
          </li>
        ))}
      </ul>
      <p className="text-sm text-ink">
        Total {formatMoney(data.previousTotal)} to <strong>{formatMoney(data.newTotal)}</strong>
      </p>
      {hasCredit && (
        <p role="status" className="text-sm font-semibold text-accent-ink">
          {data.creditOutcome === 'Refund' ? 'Refund due' : 'Credit'} {formatMoney(data.credit)}
        </p>
      )}
    </section>
  );
}
