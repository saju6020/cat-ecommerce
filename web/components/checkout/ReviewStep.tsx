'use client';

import Link from 'next/link';
import type { ReactNode } from 'react';
import { Amount } from '@/components/billing/Amount';
import { cardClass } from '@/components/hearth/card';
import { mainActionClass } from '@/components/hearth/buttons';
import { Button } from '@/components/ui/button';
import { OrderLines, type DisplayLine } from '@/components/orders/OrderLines';
import { OrderSummary } from '@/components/orders/OrderSummary';
import type { CheckoutQuote } from '@/lib/api/checkout';
import { describeBlockingReason, describeExcludedReason } from '@/lib/api/errors';
import { formatMoney } from '@/lib/format/money';
import { resolveFileUrl } from '@/lib/api/files';
import { cn } from '@/lib/utils';
import { CheckoutFreeDelivery } from './CheckoutFreeDelivery';

/**
 * `components/checkout/ReviewStep.tsx` — ⭐ HO-ORD-002 §11.2 step 4, in the Hearth layout
 * (docs/design/orders/CheckoutReview.dc.html). Left: the delivery and payment cards (`choiceCards`), the
 * prescription / changes panels the screen passes in, the quote's lines (a list, no horizontal scroll)
 * and the free-delivery nudge when the server sent an amount. Right: the six-row `OrderSummary`, the
 * discount and policy panels and **Place order · amount** (the quote's grand total) — which stays
 * disabled while the quote is settling, while any blocking reason stands, or while changes are not
 * acknowledged *for this quote*; every such reason is listed beside the button (`aria-describedby`).
 * On a phone the total and the button are pinned to the bottom; the reasons stay above in the page.
 */
export function ReviewStep({
  quote,
  fulfilment,
  settling,
  choiceCards,
  rxPanel,
  changesPanel,
  policiesPanel,
  discountField,
  needsAcknowledgement,
  acknowledged,
  placing,
  inProgress,
  placeError,
  paymentMethod,
  imageBySku,
  onPlace,
}: {
  quote: CheckoutQuote;
  fulfilment: string;
  settling: boolean;
  /** The delivery and payment cards with their Change links. */
  choiceCards?: ReactNode;
  rxPanel: ReactNode;
  changesPanel: ReactNode;
  policiesPanel: ReactNode;
  discountField: ReactNode;
  needsAcknowledgement: boolean;
  acknowledged: boolean;
  placing: boolean;
  inProgress: boolean;
  placeError: string | null;
  paymentMethod?: string;
  /** SKU id → photo URL from the cart (the quote carries no images). */
  imageBySku?: ReadonlyMap<string, string | null>;
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
    imageUrl: resolveFileUrl(imageBySku?.get(line.skuId)),
  }));
  const hasSummary = lines.length > 0;

  const reasons: string[] = [];
  if (settling) reasons.push('Updating your total…');
  for (const code of quote.blockingReasons) reasons.push(describeBlockingReason(code));
  if (needsAcknowledgement && !acknowledged) {
    reasons.push('Tick “I’ve reviewed these changes.” above to continue.');
  }
  if (inProgress) reasons.push('This order is already being placed.');
  const disabled =
    settling || placing || inProgress || !quote.canPlace || (needsAcknowledgement && !acknowledged);

  return (
    <div className="grid items-start gap-x-7 gap-y-3.5 lg:grid-cols-[minmax(0,1.6fr)_minmax(0,1fr)] lg:grid-rows-[auto_1fr] lg:gap-y-0">
      <div className="flex min-w-0 flex-col gap-[18px] lg:row-span-2">
        {choiceCards}
        {rxPanel}
        {changesPanel}
        {quote.excludedLines.length > 0 && (
          <section
            aria-labelledby="left-out-heading"
            className="rounded-card bg-surface-sunken p-5 sm:px-6"
          >
            <h3 id="left-out-heading" className="m-0 text-base font-bold text-ink">
              Left out of this order
            </h3>
            <ul className="m-0 mt-1.5 list-disc pl-5 text-sm text-ink">
              {quote.excludedLines.map((line) => (
                <li key={line.lineRef}>
                  <b>{line.productName}</b> — {describeExcludedReason(line.reasonCode)}
                </li>
              ))}
            </ul>
          </section>
        )}
        {hasSummary ? (
          <section aria-labelledby="order-items-heading" className={cn(cardClass, 'py-2')}>
            <h2 id="order-items-heading" className="m-0 mb-1 mt-3.5 text-base font-bold text-ink">
              {`Your items (${lines.length})`}
            </h2>
            <OrderLines lines={lines} caption="Items in this order" appearance="hearth" />
          </section>
        ) : (
          // Issues.md #9 — every line was left out, so there is nothing to price: say so instead of a column of 0.00s.
          <p className={cn(cardClass, 'm-0 text-sm text-ink')}>
            None of the items in your cart can be ordered right now, so there is nothing to price.{' '}
            <Link href="/cart" className="font-semibold text-accent-ink underline">
              Back to your cart
            </Link>
          </p>
        )}
        {policiesPanel}
        <CheckoutFreeDelivery quote={quote} />
      </div>

      {hasSummary && (
        <div className="flex min-w-0 flex-col gap-4 lg:rounded-t-card lg:border lg:border-b-0 lg:border-border lg:bg-surface lg:p-6 lg:pb-0">
          <OrderSummary
            appearance="hearth"
            className="lg:rounded-none lg:border-0 lg:p-0"
            totals={quote.totals}
            discountCode={quote.discount?.applied ? quote.discount.code : null}
            serviceLevel={quote.selectedServiceLevel}
            fulfilment={fulfilment}
          />
          <div className="lg:pb-1">{discountField}</div>
        </div>
      )}

      {/* Below md this wrapper vanishes (`contents`) so the sticky bar below is pinned against the
          whole page, not just this block. From md up it is the ordinary action block. */}
      <div
        className={cn(
          'flex min-w-0 flex-col gap-3 max-md:contents',
          hasSummary
            ? 'lg:rounded-b-card lg:border lg:border-t-0 lg:border-border lg:bg-surface lg:px-6 lg:pb-6'
            : 'lg:rounded-card lg:border lg:border-border lg:bg-surface lg:p-6',
        )}
      >
        {placeError && (
          <p
            role="alert"
            className="m-0 rounded-2xl bg-danger-bg px-4 py-3 text-sm font-semibold text-danger-ink"
          >
            {placeError}
          </p>
        )}
        {reasons.length > 0 && (
          <div
            id="place-why"
            className="rounded-2xl bg-surface-sunken px-4 py-3 text-[13px] text-ink lg:order-3"
          >
            <p className="m-0">To place your order:</p>
            <ul className="m-0 mt-1.5 list-disc pl-[18px]">
              {reasons.map((reason) => (
                <li key={reason} className="mt-0.5">
                  {reason}
                </li>
              ))}
            </ul>
          </div>
        )}
        {inProgress && (
          <p className="m-0 text-sm lg:order-2">
            <Link href="/portal/orders" className="font-semibold text-accent-ink underline">
              Check my orders
            </Link>{' '}
            to see whether it went through.
          </p>
        )}
        {/* Phone: just the total, the Place order pill and (when it is disabled) a one-line pointer
            to the reasons above. The scroll-padding for this bar is in globals.css. */}
        <div
          data-sticky-bar=""
          className="flex min-w-0 flex-col gap-1.5 max-md:sticky max-md:bottom-0 max-md:z-10 max-md:-mx-4 max-md:border-t max-md:border-border max-md:bg-surface max-md:px-4 max-md:pb-4 max-md:pt-3 lg:order-1"
        >
          <div className="flex items-center gap-3 max-md:justify-between md:flex-col md:items-stretch">
            {hasSummary && (
              <p className="m-0 flex flex-col text-sm text-ink-muted md:flex-row md:items-baseline md:justify-between lg:hidden">
                <span>Total</span>
                <b className="text-lg text-ink">
                  <Amount money={quote.totals.grandTotal} />
                </b>
              </p>
            )}
            <Button
              variant="pill"
              size="pillLg"
              disabled={disabled}
              aria-busy={placing}
              aria-describedby={reasons.length > 0 ? 'place-why' : undefined}
              onClick={onPlace}
              className={cn(mainActionClass, 'max-md:w-auto max-md:flex-1 max-md:px-5')}
            >
              {placing
                ? 'Placing your order…'
                : `Place order · ${formatMoney(quote.totals.grandTotal)}`}
            </Button>
          </div>
          {disabled && reasons.length > 0 && (
            <p className="m-0 truncate text-xs text-ink-muted md:hidden">
              Can&rsquo;t place yet &mdash; see the reasons above.
            </p>
          )}
        </div>
        {paymentMethod === 'BankTransfer' && hasSummary && (
          <p className="m-0 text-center text-xs leading-relaxed text-ink-muted max-lg:hidden lg:order-4">
            {`You transfer ${formatMoney(quote.totals.grandTotal)} to our bank after placing it. Nothing is charged on this website.`}
          </p>
        )}
      </div>
    </div>
  );
}
