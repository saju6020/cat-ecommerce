'use client';

import Link from 'next/link';
import { ShoppingBasket } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useAddToCart } from '@/lib/hooks/useAddToCart';

import { AddToCartFeedback } from './AddToCartFeedback';

/**
 * `components/cart/AddToCartButton.tsx` — HO-ORD-002 §3.3, the component HO-SHP-002's buy box mounts.
 * Props `{ skuId, requiresPrescription, disabled?, quantityMax? }` plus what the product page already
 * knows: the product name, whether anyone is signed in, and the server's `eligibleCats`. States: idle,
 * pending, *Added* (announced in a live region), and disabled **with its reason in text**. A
 * prescription SKU opens `PrescriptionCatDialog` (signed in) or says *"Sign in to buy prescription
 * food"* (signed out). It posts to `/api/cart/lines`; the server decides everything else.
 */
export interface AddToCartButtonProps {
  skuId: string;
  productName: string;
  requiresPrescription: boolean;
  disabled?: boolean;
  /** Why it is disabled (e.g. out of stock), shown beside the button. */
  disabledReason?: string;
  /** The most the shopper may add at once (the server's `maxQuantityPerLine` when known). */
  quantityMax?: number;
  /** `undefined` while the session is still loading. */
  signedIn?: boolean;
  /** Cats the server says are authorised for this food (from `usePurchasability`). */
  eligibleCatIds?: string[];
  signInHref?: string;
}

export function AddToCartButton({
  skuId,
  productName,
  requiresPrescription,
  disabled,
  disabledReason,
  quantityMax = 20,
  signedIn,
  eligibleCatIds = [],
  signInHref = '/sign-in',
}: AddToCartButtonProps) {
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

  if (state.guestRx) {
    return (
      <Link href={signInHref} className="text-sm font-semibold text-accent-ink underline">
        Sign in to buy prescription food
      </Link>
    );
  }

  return (
    <div className="flex flex-col gap-2" data-slot="add-to-cart">
      <div className="flex flex-wrap items-end gap-3">
        {!requiresPrescription && (
          <div className="flex flex-col gap-1">
            <label htmlFor={`qty-${skuId}`} className="text-xs font-semibold text-ink-muted">
              Quantity
            </label>
            <input
              id={`qty-${skuId}`}
              type="number"
              min={1}
              max={quantityMax}
              value={state.quantity}
              disabled={disabled}
              onChange={(event) => state.setQuantity(event.target.valueAsNumber)}
              className="h-10 w-20 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 text-sm tabular-nums text-ink"
            />
          </div>
        )}
        <Button
          disabled={state.isDisabled}
          aria-busy={state.pending}
          aria-describedby={state.describedBy}
          onClick={state.onAdd}
        >
          <ShoppingBasket size={16} aria-hidden="true" />
          {state.pending ? 'Adding…' : 'Add to cart'}
        </Button>
      </div>
      <AddToCartFeedback state={state} requiresPrescription={requiresPrescription} />
    </div>
  );
}
