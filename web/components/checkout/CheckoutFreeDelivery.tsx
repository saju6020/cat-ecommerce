'use client';

import Link from 'next/link';
import { FreeDeliveryProgress } from '@/components/hearth/FreeDeliveryProgress';
import type { CheckoutQuote } from '@/lib/api/checkout';
import { formatMoney } from '@/lib/format/money';
import { moneySign } from '@/lib/money';

/**
 * The free-delivery nudge on the checkout review (docs/design/orders/CheckoutReview.dc.html), fed ONLY
 * from the quote's selected service level: `amountToFreeDelivery` is the server's own figure (threshold
 * minus goods value, never below zero, `null` when the zone has no threshold). The card is absent when
 * there is no such amount or it is zero, and for collection (no service level is selected). The browser
 * never works out a threshold or a percentage (ADR-FE-105), so the card shows the sentence only (no
 * percentage is passed, so the shared component draws no bar).
 */
export function CheckoutFreeDelivery({ quote }: { quote: CheckoutQuote }) {
  const level = quote.delivery.serviceLevels.find(
    (entry) => entry.level === quote.selectedServiceLevel,
  );
  const amount = level?.amountToFreeDelivery;
  if (!amount || moneySign(amount.amount) === 'zero') return null;
  return (
    <FreeDeliveryProgress
      remaining={formatMoney(amount)}
      zoneLabel={quote.delivery.zoneName ?? undefined}
    >
      <Link
        href="/shop"
        className="-mb-2 -mt-1.5 inline-flex min-h-11 items-center self-start text-[13px] font-semibold text-accent-ink hover:text-accent"
      >
        Keep shopping
      </Link>
    </FreeDeliveryProgress>
  );
}
