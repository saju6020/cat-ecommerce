'use client';

import Link from 'next/link';
import { useState } from 'react';
import { ShoppingBasket } from 'lucide-react';
import { Amount } from '@/components/billing/Amount';
import { EmptyCatIllustration, EmptyState } from '@/components/common/EmptyState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
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
import type { CartLine } from '@/lib/api/cart';
import { CartLineRow } from './CartLineRow';
import { CartMergeNotice } from './CartMergeNotice';

/**
 * `components/cart/CartScreen.tsx` — ⭐ HO-ORD-002 §11.1 `CartPage`, US-ORD-01, 02. Lines with image,
 * variant, cat chip, quantity stepper, line subtotal and **Remove** (reversible, so no confirm — an
 * *Undo* toast re-adds the line); availability and price changes as text; the footer shows the item
 * count and subtotal (both the server's), *"Delivery and tax are calculated at checkout"* and
 * **Checkout** (signed out → sign-in with `?next=/checkout`). An unavailable line blocks Checkout until
 * it is removed. Loading, empty and error states are all real.
 */
export function CartScreen() {
  const cart = useCart();
  const session = useCustomerSession();
  const update = useUpdateCartLine();
  const remove = useRemoveCartLine();
  const add = useAddCartLine();
  const { notice, dismiss } = useCartMergeNotice();
  const [error, setError] = useState<string | null>(null);
  const busy = update.isPending || remove.isPending || add.isPending;

  if (cart.isPending) return <LoadingState rows={5} label="Loading your cart…" />;
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

  return (
    <div className="flex flex-col gap-5">
      <h1 className="font-display text-2xl font-semibold text-ink">Your cart</h1>
      {notice && <CartMergeNotice result={notice} onDismiss={dismiss} />}
      {error && (
        <p role="alert" className="rounded-[var(--radius-sm)] bg-danger-bg p-3 text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}
      {data.lines.length === 0 ? (
        <EmptyState
          icon={<EmptyCatIllustration />}
          title="Your cart is empty."
          description="Find food, litter and toys for your cat — what you add stays here, even if you sign in later."
          action={
            <Link href="/shop" className={buttonVariants({ variant: 'primary' })}>
              Browse the shop
            </Link>
          }
        />
      ) : (
        <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1.9fr)_minmax(0,1fr)]">
          <section aria-label="Items in your cart" className="min-w-0 rounded-[var(--radius-md)] border border-border bg-surface px-4 sm:px-5">
            <ul className="m-0 list-none p-0">
              {data.lines.map((line) => (
                <CartLineRow
                  key={line.lineId}
                  line={line}
                  maxQuantityPerLine={data.maxQuantityPerLine}
                  busy={busy}
                  onQuantity={(quantity) => changeQuantity(line, quantity)}
                  onRemove={() => removeLine(line)}
                />
              ))}
            </ul>
          </section>
          <aside aria-label="Cart summary" className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4 sm:p-5">
            <h2 className="font-display text-lg font-semibold text-ink">Summary</h2>
            <dl className="m-0 flex items-baseline justify-between gap-3 border-b border-dashed border-border py-1.5 text-sm">
              <dt className="text-ink-muted">{`Items (${data.itemCount})`}</dt>
              <dd className="m-0">
                <Amount money={data.subtotal} />
              </dd>
            </dl>
            <p className="text-sm text-ink-muted">Delivery and tax are calculated at checkout.</p>
            {blocker ? (
              <>
                <p className="rounded-[var(--radius-sm)] bg-warning-bg p-3 text-sm text-warning-ink">
                  Remove the unavailable item to continue to checkout.
                </p>
                <Button size="md" disabled aria-describedby="checkout-why" className="w-full">
                  <ShoppingBasket size={16} aria-hidden="true" />
                  Checkout
                </Button>
                <p id="checkout-why" className="text-center text-xs text-ink-muted">
                  {`Disabled: ${blocker.productName} is unavailable.`}
                </p>
              </>
            ) : (
              <Link href={checkoutHref} className={`${buttonVariants({ variant: 'primary' })} w-full`}>
                <ShoppingBasket size={16} aria-hidden="true" />
                Checkout
              </Link>
            )}
            {!signedIn && !session.isPending && (
              <p className="text-xs text-ink-muted">
                You&rsquo;ll be asked to sign in first — your cart comes with you.
              </p>
            )}
          </aside>
        </div>
      )}
      {signedIn && data.lines.length > 0 && (
        <p className="text-sm text-ink-muted">
          Your cart is saved to your account, so it&rsquo;s there on your other devices too.
        </p>
      )}
    </div>
  );
}
