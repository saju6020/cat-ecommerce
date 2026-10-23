'use client';

import * as React from 'react';
import Link from 'next/link';
import { Minus, Plus, ShoppingBasket } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { AddToCartFeedback } from '@/components/cart/AddToCartFeedback';
import { useAddToCart } from '@/lib/hooks/useAddToCart';
import { cn } from '@/lib/utils';

export interface PurchaseRowProps {
  skuId: string;
  productName: string;
  requiresPrescription: boolean;
  disabled?: boolean;
  /** Why it is disabled (e.g. out of stock), written next to the row and tied by `aria-describedby`. */
  disabledReason?: string;
  /** The most the shopper may add at once. */
  quantityMax?: number;
  /** `undefined` while the session is still loading. */
  signedIn?: boolean;
  /** Cats the server says are authorised for this food (from `usePurchasability`). */
  eligibleCatIds?: string[];
  signInHref?: string;
  /** The round wishlist button, drawn last in the row. */
  trailing?: React.ReactNode;
}

/**
 * The product page's buy row (docs/design/shop/ProductPage.dc.html, owner decision 2026-10-16): a
 * pill quantity stepper (- / number input / +, 48px), a full-width teal *Add to cart* and the round wishlist
 * button. It shares `useAddToCart` (the cart mutation, the `PrescriptionCatDialog`, the messages)
 * with HO-ORD-002's `AddToCartButton` and keeps its behaviour: a prescription SKU asks which cat first (signed in) or
 * says to sign in (signed out), and the server decides everything else. For an out-of-stock size
 * the stepper and button are disabled with the reason beside them.
 */
export function PurchaseRow({
  skuId,
  productName,
  requiresPrescription,
  disabled,
  disabledReason,
  quantityMax = 20,
  signedIn,
  eligibleCatIds = [],
  signInHref = '/sign-in',
  trailing,
}: PurchaseRowProps) {
  const state = useAddToCart({
    skuId,
    productName,
    requiresPrescription,
    disabled,
    disabledReason,
    quantityMax,
    signedIn,
    eligibleCatIds,
  });
  const { quantity, setQuantity } = state;
  // While the shopper is typing the field may be empty; it snaps back to the real quantity on blur.
  const [draft, setDraft] = React.useState<string | null>(null);
  function typeQuantity(raw: string, parsed: number) {
    if (Number.isInteger(parsed) && parsed >= 1) {
      const next = Math.min(quantityMax, parsed);
      setQuantity(next);
      setDraft(String(next));
    } else {
      setDraft(raw);
    }
  }
  const stepperButton =
    'grid size-12 place-items-center rounded-pill border-0 bg-transparent p-0 text-ink hover:bg-surface-sunken disabled:opacity-40';

  return (
    <div className="flex flex-col gap-2.5" data-slot="add-to-cart">
      <div className="flex flex-wrap items-center gap-3">
        {state.guestRx ? (
          <Link
            href={signInHref}
            className="inline-flex min-h-12 items-center text-sm font-semibold text-accent-ink underline"
          >
            Sign in to buy prescription food
          </Link>
        ) : (
          <>
            {!requiresPrescription && (
              <div
                className={cn(
                  'flex min-h-12 items-center rounded-pill border border-border-strong bg-surface',
                  disabled && 'opacity-60',
                )}
              >
                <button
                  type="button"
                  aria-label="One fewer"
                  disabled={disabled || quantity <= 1}
                  onClick={() => {
                    setDraft(null);
                    setQuantity(quantity - 1);
                  }}
                  className={stepperButton}
                >
                  <Minus size={18} aria-hidden="true" />
                </button>
                <input
                  type="number"
                  inputMode="numeric"
                  aria-label="Quantity"
                  min={1}
                  max={quantityMax}
                  value={draft ?? quantity}
                  disabled={disabled}
                  onChange={(event) => typeQuantity(event.target.value, event.target.valueAsNumber)}
                  onBlur={() => setDraft(null)}
                  className="w-12 border-0 bg-transparent p-0 text-center text-[15px] font-bold tabular-nums text-ink [appearance:textfield] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none"
                />
                <button
                  type="button"
                  aria-label="One more"
                  disabled={disabled || quantity >= quantityMax}
                  onClick={() => {
                    setDraft(null);
                    setQuantity(quantity + 1);
                  }}
                  className={stepperButton}
                >
                  <Plus size={18} aria-hidden="true" />
                </button>
              </div>
            )}
            <Button
              variant="pill"
              className="min-h-12 min-w-[180px] flex-1 px-6 text-[15px]"
              disabled={state.isDisabled}
              aria-busy={state.pending}
              aria-describedby={state.describedBy}
              onClick={state.onAdd}
            >
              <ShoppingBasket size={18} aria-hidden="true" />
              {state.pending ? 'Adding…' : 'Add to cart'}
            </Button>
          </>
        )}
        {trailing}
      </div>
      <AddToCartFeedback
        state={state}
        requiresPrescription={requiresPrescription && !state.guestRx}
      />
    </div>
  );
}
