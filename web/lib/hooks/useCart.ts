'use client';

import { useEffect, useRef } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  addCartLine,
  getCart,
  mergeGuestCart,
  removeCartLine,
  updateCartLine,
  type AddCartLineRequest,
  type MergeCartResult,
} from '@/lib/api/cart';
import { getCats } from '@/lib/api/cats';

/**
 * `lib/hooks/useCart.ts` — HO-ORD-002 §8.3, ADR-FE-089. The cart is SERVER state: one `['cart']`
 * query for guests and customers (the guest identity is the API's `HttpOnly` cookie, which this code
 * never reads). Every cart mutation invalidates it. When the cart says `guestCartPending` (a customer
 * session and a live guest cookie are both present), `useCart` calls the merge endpoint ONCE and
 * refetches; the merge outcome is parked in the query cache so the cart page can show it after
 * navigation (`useCartMergeNotice`).
 */
export const CART_QUERY_KEY = ['cart'] as const;
const MERGE_NOTICE_KEY = ['cart-merge-notice'] as const;
const MERGE_MUTATION_KEY = ['cart', 'merge'] as const;

export function useCart() {
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: CART_QUERY_KEY,
    queryFn: getCart,
    staleTime: 10_000,
  });

  const merge = useMutation({
    mutationKey: MERGE_MUTATION_KEY,
    mutationFn: mergeGuestCart,
    // Returning the invalidation promise keeps the mutation `pending` until the cart has been
    // refetched, so a second `useCart` (the header badge and the page) cannot start another merge.
    onSuccess: async (result) => {
      queryClient.setQueryData<MergeCartResult | null>(MERGE_NOTICE_KEY, result);
      await queryClient.invalidateQueries({ queryKey: CART_QUERY_KEY });
    },
  });

  const attempted = useRef(false);
  const pending = query.data?.guestCartPending === true;
  useEffect(() => {
    if (!pending || attempted.current) return;
    if (queryClient.isMutating({ mutationKey: MERGE_MUTATION_KEY }) > 0) return;
    attempted.current = true;
    merge.mutate();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pending]);

  return query;
}

/** The outcome of the last guest-cart merge (`null` once dismissed or when there was none). */
export function useCartMergeNotice() {
  const queryClient = useQueryClient();
  const query = useQuery<MergeCartResult | null>({
    queryKey: MERGE_NOTICE_KEY,
    queryFn: () => null,
    enabled: false,
    staleTime: Infinity,
    gcTime: Infinity,
  });
  return {
    notice: query.data ?? null,
    dismiss: () => queryClient.setQueryData<MergeCartResult | null>(MERGE_NOTICE_KEY, null),
  };
}

export function useAddCartLine() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: AddCartLineRequest) => addCartLine(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: CART_QUERY_KEY }),
  });
}

export function useUpdateCartLine() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { lineId: string; quantity?: number; catId?: string }) =>
      updateCartLine(input.lineId, { quantity: input.quantity, catId: input.catId }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: CART_QUERY_KEY }),
  });
}

export function useRemoveCartLine() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (lineId: string) => removeCartLine(lineId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: CART_QUERY_KEY }),
  });
}

/** The signed-in customer's cats for `PrescriptionCatDialog` (loaded only when the dialog opens). */
export function useCartCats(enabled: boolean) {
  return useQuery({
    queryKey: ['cats', { pageSize: 50, source: 'cart' }],
    queryFn: () => getCats({ pageSize: 50 }),
    enabled,
  });
}
