'use client';

import * as React from 'react';
import { describeOrderingError } from '@/lib/api/errors';
import { useAddCartLine, useCartCats } from '@/lib/hooks/useCart';

export interface UseAddToCartOptions {
  skuId: string;
  productName: string;
  requiresPrescription: boolean;
  disabled?: boolean;
  disabledReason?: string;
  /** The most the shopper may add at once. */
  quantityMax?: number;
  /** `undefined` while the session is still loading. */
  signedIn?: boolean;
  eligibleCatIds?: string[];
}

/**
 * The add-to-cart behaviour shared by `AddToCartButton` and the product page's `PurchaseRow`
 * (HO-ORD-002 §3.3): the cart mutation, the quantity (clamped to 1..max), the prescription cat
 * dialog, the guest sign-in case, the disabled reason and the Added / error messages. The two
 * components differ only in their markup.
 */
export function useAddToCart({
  skuId,
  productName,
  requiresPrescription,
  disabled,
  disabledReason,
  quantityMax = 20,
  signedIn,
  eligibleCatIds = [],
}: UseAddToCartOptions) {
  const add = useAddCartLine();
  const [quantity, setQuantityState] = React.useState(1);
  const [dialogOpen, setDialogOpen] = React.useState(false);
  const [message, setMessage] = React.useState('');
  const [error, setError] = React.useState<string | null>(null);
  const cats = useCartCats(requiresPrescription && signedIn === true && dialogOpen);

  const setQuantity = React.useCallback(
    (next: number) => {
      if (Number.isInteger(next)) setQuantityState(Math.min(quantityMax, Math.max(1, next)));
    },
    [quantityMax],
  );

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

  const reasonId = `add-${skuId}-why`;
  const showReason = !!disabled && !!disabledReason;
  return {
    quantity,
    setQuantity,
    quantityMax,
    pending: add.isPending,
    guestRx: requiresPrescription && signedIn === false,
    isDisabled: !!disabled || add.isPending || (requiresPrescription && signedIn === undefined),
    reasonId,
    disabledReason,
    showReason,
    describedBy: showReason ? reasonId : undefined,
    /** The Add button's click: ask which cat for a prescription SKU, else add straight away. */
    onAdd: () => (requiresPrescription ? setDialogOpen(true) : submit()),
    message,
    error,
    dialog: {
      open: dialogOpen,
      onOpenChange: setDialogOpen,
      productName,
      cats: (cats.data?.items ?? []).map((cat) => ({
        id: cat.id,
        name: cat.name,
        authorised: eligibleCatIds.includes(cat.id),
      })),
      isLoading: cats.isPending && cats.fetchStatus !== 'idle',
      isError: cats.isError,
      pending: add.isPending,
      error: dialogOpen ? error : null,
      onChoose: (catId: string) => submit(catId),
    },
  };
}

export type AddToCart = ReturnType<typeof useAddToCart>;
