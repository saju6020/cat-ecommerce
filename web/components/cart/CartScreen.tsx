'use client';

import Link from 'next/link';
import { useState } from 'react';
import { TriangleAlert } from 'lucide-react';
import { Amount } from '@/components/billing/Amount';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { bannerWarnClass, summaryCardClass } from '@/components/hearth/card';
import { mainActionClass } from '@/components/hearth/buttons';
import { HearthEmpty } from '@/components/hearth/HearthEmpty';
import type { PackTone } from '@/components/hearth/ProductPackArt';
import { Button, buttonVariants } from '@/components/ui/button';
import { ToastAction } from '@/components/ui/toast';
import { toast } from '@/components/ui/use-toast';
import { describeOrderingError } from '@/lib/api/errors';
import {
  useAddCartLine,
  useCart,
  useCartMergeNotice,
  useRemoveCartLine,
  useUpdateCartLine,
} from '@/lib/hooks/useCart';
import { useCustomerSession } from '@/lib/hooks/useSession';
import { useHydrated } from '@/lib/hooks/useHydrated';
import type { CartLine } from '@/lib/api/cart';
import { formatMoney } from '@/lib/format/money';
import { cn } from '@/lib/utils';
import { CartLineRow } from './CartLineRow';
import { CartMergeNotice } from './CartMergeNotice';

const TILE_TONES: PackTone[] = ['warm', 'sunken', 'teal'];

/**
 * `components/cart/CartScreen.tsx` — ⭐ HO-ORD-002 §11.1 `CartPage`, US-ORD-01, 02, in the Hearth layout
 * (docs/design/orders/Cart.dc.html). Lines as rounded cards with image, variant, cat chip, quantity
 * stepper, line subtotal and **Remove** (reversible, so no confirm — an *Undo* toast re-adds the line);
 * availability and price changes as text; the summary shows the item count and subtotal (both the
 * server's), *"Worked out at checkout"* for delivery and tax, and **Checkout · amount** (signed out →
 * sign-in with `?next=/checkout`). An unavailable line blocks Checkout until it is removed, with the
 * reason beside the button. There is no free-delivery card here: the cart has no address or zone, so
 * the server cannot say what is still needed (the card appears on checkout). Loading, empty and error
 * states are all real. On a phone only the Checkout pill (and a one-line reason) sticks to the bottom.
 */
export function CartScreen() {
  const cart = useCart();
  const hydrated = useHydrated();
  const session = useCustomerSession();
  const update = useUpdateCartLine();
  const remove = useRemoveCartLine();
  const add = useAddCartLine();
  const { notice, dismiss } = useCartMergeNotice();
  const [error, setError] = useState<string | null>(null);
  const busy = update.isPending || remove.isPending || add.isPending;

  // The header's CartBadge may already have the cart when this page hydrates: keep the server's loading state
  // for the hydration render, then show the cart.
  if (!hydrated || cart.isPending) return <LoadingState rows={5} label="Loading your cart…" />;
  if (cart.isError || !cart.data) {
    return (
      <ErrorState
        title="We couldn't load your cart."
        description="Your items are safe — please try again."
        headingLevel={2}
        onRetry={() => void cart.refetch()}
      />
    );
  }

  const data = cart.data;
  const signedIn = !!session.data;
  const blocker = data.lines.find((line) => line.availability === 'Unavailable');

  function changeQuantity(line: CartLine, quantity: number) {
    setError(null);
    update.mutate(
      { lineId: line.lineId, quantity },
      { onError: (failure) => setError(describeOrderingError(failure)) },
    );
  }

  function removeLine(line: CartLine) {
    setError(null);
    remove.mutate(line.lineId, {
      onSuccess: () => {
        const label = `${line.productName} ${line.variantLabel}`.trim();
        toast({
          title: `Removed ${label} from your cart.`,
          action: (
            <ToastAction
              altText={`Undo removing ${label}`}
              onClick={() =>
                add.mutate({
                  skuId: line.skuId,
                  quantity: line.quantity,
                  catId: line.cat?.id,
                })
              }
            >
              Undo
            </ToastAction>
          ),
        });
      },
      onError: (failure) => setError(describeOrderingError(failure)),
    });
  }

  const checkoutHref = signedIn ? '/checkout' : `/sign-in?next=${encodeURIComponent('/checkout')}`;
  const checkoutLabel = `${signedIn || session.isPending ? 'Checkout' : 'Sign in to check out'} · ${formatMoney(data.subtotal)}`;
  const hasLines = data.lines.length > 0;

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center gap-3.5">
        <h1 className="m-0 font-display text-[32px] font-semibold leading-[1.1] text-ink md:text-[40px]">
          Your cart
        </h1>
        {hasLines && (
          <span className="rounded-pill bg-warm-soft px-3.5 py-1.5 text-[13px] font-bold text-warm-ink">
            {`${data.itemCount} ${data.itemCount === 1 ? 'item' : 'items'}`}
          </span>
        )}
      </div>
      {notice && <CartMergeNotice result={notice} onDismiss={dismiss} />}
      {error && (
        <p
          role="alert"
          className="rounded-2xl bg-danger-bg px-[18px] py-3.5 text-sm font-semibold text-danger-ink"
        >
          {error}
        </p>
      )}
      {!hasLines ? (
        <HearthEmpty
          title="Your cart is empty."
          description="Find food, litter and toys for your cat — what you add stays here, even if you sign in later."
          action={
            <Link href="/shop" className={buttonVariants({ variant: 'pill', size: 'pillLg' })}>
              Browse the shop
            </Link>
          }
        />
      ) : (
        <div className="grid items-start gap-7 lg:grid-cols-[minmax(0,1.6fr)_minmax(0,1fr)]">
          <section aria-label="Items in your cart" className="min-w-0">
            <ul className="m-0 flex list-none flex-col gap-3.5 p-0">
              {data.lines.map((line, index) => (
                <CartLineRow
                  key={line.lineId}
                  line={line}
                  maxQuantityPerLine={data.maxQuantityPerLine}
                  busy={busy}
                  tone={TILE_TONES[index % TILE_TONES.length]}
                  onQuantity={(quantity) => changeQuantity(line, quantity)}
                  onRemove={() => removeLine(line)}
                />
              ))}
            </ul>
          </section>
          {/* Below md the two wrappers vanish (`contents`) so the sticky bar is pinned against the
              whole page; the summary, warning and notes stay in normal flow above it. */}
          <div className="flex min-w-0 flex-col gap-4 max-md:contents">
            <aside
              aria-label="Cart summary"
              className={cn(summaryCardClass, 'flex flex-col gap-4 max-md:contents')}
            >
              <div className="flex flex-col gap-4 max-md:rounded-card max-md:border max-md:border-border max-md:bg-surface max-md:p-5">
                <h2 className="m-0 font-display text-2xl font-semibold text-ink">Order summary</h2>
                <dl className="m-0">
                  <div className="flex items-baseline justify-between gap-3 border-b border-dashed border-border py-[9px] text-sm">
                    <dt className="text-ink-muted">{`Items subtotal (${data.itemCount})`}</dt>
                    <dd className="m-0 whitespace-nowrap">
                      <Amount money={data.subtotal} />
                    </dd>
                  </div>
                  <div className="flex items-baseline justify-between gap-3 border-b border-dashed border-border py-[9px] text-sm">
                    <dt className="text-ink-muted">Delivery</dt>
                    <dd className="m-0 text-right">Worked out at checkout</dd>
                  </div>
                  <div className="flex items-baseline justify-between gap-3 py-[9px] text-sm">
                    <dt className="text-ink-muted">Tax</dt>
                    <dd className="m-0 text-right">Worked out at checkout</dd>
                  </div>
                </dl>
                {blocker && (
                  <p className={cn(bannerWarnClass, 'm-0')}>
                    <TriangleAlert size={15} aria-hidden="true" className="mt-0.5 flex-none" />
                    Remove the unavailable item to continue to checkout.
                  </p>
                )}
              </div>
              {!signedIn && !session.isPending && (
                <p className="m-0 text-center text-xs leading-relaxed text-ink-muted md:order-3">
                  You&rsquo;ll be asked to sign in first — your cart comes with you.
                </p>
              )}
              {/* Phone: the bar holds only the Checkout pill (its label carries the subtotal) and,
                  when it is disabled, a one-line reason. Scroll-padding is in globals.css. */}
              <div
                data-sticky-bar=""
                className="flex min-w-0 flex-col gap-1.5 max-md:sticky max-md:bottom-0 max-md:z-10 max-md:-mx-4 max-md:border-t max-md:border-border max-md:bg-surface max-md:px-4 max-md:pb-4 max-md:pt-3 md:order-2"
              >
                {blocker ? (
                  <>
                    <Button
                      variant="pill"
                      size="pillLg"
                      disabled
                      aria-describedby="checkout-why"
                      className={mainActionClass}
                    >
                      {checkoutLabel}
                    </Button>
                    <p id="checkout-why" className="m-0 text-center text-xs text-ink-muted">
                      {`Disabled: ${blocker.productName} is unavailable.`}
                    </p>
                  </>
                ) : (
                  <Link
                    href={checkoutHref}
                    className={cn(
                      buttonVariants({ variant: 'pill', size: 'pillLg' }),
                      mainActionClass,
                    )}
                  >
                    {checkoutLabel}
                  </Link>
                )}
              </div>
            </aside>
            {signedIn && (
              <p className="m-0 text-sm text-ink-muted max-lg:hidden">
                Your cart is saved to your account, so it&rsquo;s there on your other devices too.
              </p>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
