'use client';

import Link from 'next/link';
import type { ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { OrderLines, type DisplayLine } from '@/components/orders/OrderLines';
import { OrderSummary } from '@/components/orders/OrderSummary';
import type { CheckoutQuote } from '@/lib/api/checkout';
import { describeBlockingReason, describeExcludedReason } from '@/lib/api/errors';

/**
 * `components/checkout/ReviewStep.tsx` — ⭐ HO-ORD-002 §11.2 step 4. The quote's lines (a list, no
 * horizontal scroll), the prescription / changes / policy / discount panels the screen passes in, the
 * six-row `OrderSummary` and **Place order** — which stays disabled while the quote is settling, while
 * any blocking reason stands, or while changes are not acknowledged *for this quote*; every such reason
 * is listed next to the button (`aria-describedby`). On a phone the button is pinned to the bottom.
 */
export function ReviewStep({
  quote,
  fulfilment,
  settling,
  rxPanel,
  changesPanel,
  policiesPanel,
  discountField,
  needsAcknowledgement,
  acknowledged,
  placing,
  inProgress,
  placeError,
  onPlace,
}: {
  quote: CheckoutQuote;
  fulfilment: string;
  settling: boolean;
  rxPanel: ReactNode;
  changesPanel: ReactNode;
  policiesPanel: ReactNode;
  discountField: ReactNode;
  needsAcknowledgement: boolean;
  acknowledged: boolean;
  placing: boolean;
  inProgress: boolean;
  placeError: string | null;
  onPlace: () => void;
}) {
  const lines: DisplayLine[] = quote.lines.map((line) => ({
    key: line.lineRef,
    productName: line.productName,
    variantLabel: line.variantLabel,
    cat: line.cat,
    quantity: line.quantity,
    unitPrice: line.unitPrice,
    lineTotal: line.lineTotal,
    taxRatePercent: line.taxRatePercent,
  }));

  const reasons: string[] = [];
  if (settling) reasons.push('Updating your total…');
  for (const code of quote.blockingReasons) reasons.push(describeBlockingReason(code));
  if (needsAcknowledgement && !acknowledged) {
    reasons.push('Tick “I’ve reviewed these changes.” above to continue.');
  }
  if (inProgress) reasons.push('This order is already being placed.');
  const disabled = settling || placing || inProgress || !quote.canPlace || (needsAcknowledgement && !acknowledged);

  return (
    <div className="flex flex-col gap-4">
      {rxPanel}
      {changesPanel}
      {quote.excludedLines.length > 0 && (
        <section aria-labelledby="left-out-heading" className="rounded-[var(--radius-md)] border border-border bg-surface-sunken p-4">
          <h3 id="left-out-heading" className="font-display text-base font-semibold text-ink">
            Left out of this order
          </h3>
          <ul className="m-0 mt-1 list-disc pl-5 text-sm text-ink">
            {quote.excludedLines.map((line) => (
              <li key={line.lineRef}>
                <b>{line.productName}</b> — {describeExcludedReason(line.reasonCode)}
              </li>
            ))}
          </ul>
        </section>
      )}
      {lines.length > 0 ? (
        <section aria-label="Items in this order" className="rounded-[var(--radius-md)] border border-border bg-surface px-4">
          <OrderLines lines={lines} caption="Items in this order" />
        </section>
      ) : (
        // Issues.md #9 — every line was left out, so there is nothing to price: say so instead of a column of 0.00s.
        <p className="rounded-[var(--radius-md)] border border-border bg-surface p-4 text-sm text-ink">
          None of the items in your cart can be ordered right now, so there is nothing to price.{' '}
          <Link href="/cart" className="font-semibold text-accent-ink underline">
            Back to your cart
          </Link>
        </p>
      )}
      {policiesPanel}
      {lines.length > 0 && (
        <>
          <div className="rounded-[var(--radius-md)] border border-border bg-surface p-4">{discountField}</div>
          <div className="rounded-[var(--radius-md)] border border-border bg-surface p-4">
            <OrderSummary
              totals={quote.totals}
              discountCode={quote.discount?.applied ? quote.discount.code : null}
              serviceLevel={quote.selectedServiceLevel}
              fulfilment={fulfilment}
            />
          </div>
        </>
      )}
      <div className="sticky bottom-0 z-10 -mx-4 flex flex-col gap-2 border-t border-border bg-surface-page px-4 py-3 sm:static sm:mx-0 sm:border-0 sm:bg-transparent sm:p-0">
        {placeError && (
          <p role="alert" className="rounded-[var(--radius-sm)] bg-danger-bg p-3 text-sm font-semibold text-danger-ink">
            {placeError}
          </p>
        )}
        <Button
          size="md"
          disabled={disabled}
          aria-busy={placing}
          aria-describedby={reasons.length > 0 ? 'place-why' : undefined}
          onClick={onPlace}
          className="w-full sm:w-fit"
        >
          {placing ? 'Placing your order…' : 'Place order'}
        </Button>
        {inProgress && (
          <p className="text-sm">
            <Link href="/portal/orders" className="font-semibold text-accent-ink underline">
              Check my orders
            </Link>{' '}
            to see whether it went through.
          </p>
        )}
        {reasons.length > 0 && (
          <ul id="place-why" className="m-0 list-none p-0 text-sm text-ink-muted">
            {reasons.map((reason) => (
              <li key={reason}>{reason}</li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
