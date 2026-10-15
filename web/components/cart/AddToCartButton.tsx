'use client';

import Link from 'next/link';
import { useState } from 'react';
import { ShoppingBasket } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { describeOrderingError } from '@/lib/api/errors';
import { useAddCartLine, useCartCats } from '@/lib/hooks/useCart';

import { PrescriptionCatDialog } from './PrescriptionCatDialog';

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
  const add = useAddCartLine();
  const [quantity, setQuantity] = useState(1);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState<string | null>(null);
  const cats = useCartCats(requiresPrescription && signedIn === true && dialogOpen);

  function submit(catId?: string) {
    setError(null);
    setMessage('');
    add.mutate(
      { skuId, quantity, catId },
      {
        onSuccess: () => {
          setDialogOpen(false);
          setMessage(`Added ${quantity} × ${productName} to your cart.`);
        },
        onError: (failure) => setError(describeOrderingError(failure)),
      },
    );
  }

  if (requiresPrescription && signedIn === false) {
    return (
      <Link href={signInHref} className="text-sm font-semibold text-accent-ink underline">
        Sign in to buy prescription food
      </Link>
    );
  }

  const reasonId = `add-${skuId}-why`;
  const isDisabled = disabled || add.isPending || (requiresPrescription && signedIn === undefined);

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
              value={quantity}
              disabled={disabled}
              onChange={(event) => {
                const next = event.target.valueAsNumber;
                if (Number.isInteger(next) && next >= 1) setQuantity(Math.min(next, quantityMax));
              }}
              className="h-10 w-20 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 text-sm tabular-nums text-ink"
            />
          </div>
        )}
        <Button
          disabled={isDisabled}
          aria-busy={add.isPending}
          aria-describedby={disabled && disabledReason ? reasonId : undefined}
          onClick={() => (requiresPrescription ? setDialogOpen(true) : submit())}
        >
          <ShoppingBasket size={16} aria-hidden="true" />
          {add.isPending ? 'Adding…' : 'Add to cart'}
        </Button>
      </div>
      {disabled && disabledReason && (
        <p id={reasonId} className="text-sm text-ink-muted">
          {disabledReason}
        </p>
      )}
      <p role="status" aria-live="polite" className="text-sm font-semibold text-accent-ink">
        {message}
        {message && (
          <>
            {' '}
            <Link href="/cart" className="underline">
              View cart
            </Link>
          </>
        )}
      </p>
      {error && !dialogOpen && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}
      {requiresPrescription && (
        <PrescriptionCatDialog
          open={dialogOpen}
          onOpenChange={setDialogOpen}
          productName={productName}
          cats={(cats.data?.items ?? []).map((cat) => ({
            id: cat.id,
            name: cat.name,
            authorised: eligibleCatIds.includes(cat.id),
          }))}
          isLoading={cats.isPending && cats.fetchStatus !== 'idle'}
          isError={cats.isError}
          pending={add.isPending}
          error={dialogOpen ? error : null}
          onChoose={(catId) => submit(catId)}
        />
      )}
    </div>
  );
}
