'use client';

import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { getPublicBranches } from '@/lib/api/admin-branches';
import { CART_QUERY_KEY } from './useCart';
import {
  cancelMyOrder,
  getMyOrder,
  getMyReturn,
  listMyOrders,
  reorderOrder,
  requestMyReturn,
  submitTransferReference,
  type MyOrdersFilters,
  type ReturnLineInput,
} from '@/lib/api/my-orders';

/**
 * `lib/hooks/useMyOrders.ts` — HO-ORD-002 §8.3. The signed-in customer's orders: keys
 * `['me','orders', filters]`, `['me','order', id]`, `['me','return', id]`. A `404` (including someone
 * else's order) is never retried — the page renders "not found".
 */
function noRetryOnRefusal(failureCount: number, error: unknown) {
  if (error instanceof ApiForbiddenError || error instanceof ApiNotFoundError) return false;
  return failureCount < 1;
}

export function useMyOrders(filters: MyOrdersFilters) {
  return useQuery({
    queryKey: ['me', 'orders', filters],
    queryFn: () => listMyOrders(filters),
    placeholderData: keepPreviousData,
    retry: noRetryOnRefusal,
  });
}

export function useMyOrder(orderId: string) {
  return useQuery({
    queryKey: ['me', 'order', orderId],
    queryFn: () => getMyOrder(orderId),
    enabled: !!orderId,
    // An order moves on without the customer (confirmed, expired, dispatched): never trust a cached copy.
    staleTime: 0,
    retry: noRetryOnRefusal,
  });
}

/** Refetch one order (the transfer deadline passed). */
export function useRefreshMyOrder(orderId: string) {
  const queryClient = useQueryClient();
  return () => void queryClient.invalidateQueries({ queryKey: ['me', 'order', orderId] });
}

export function useMyReturn(returnRequestId: string) {
  return useQuery({
    queryKey: ['me', 'return', returnRequestId],
    queryFn: () => getMyReturn(returnRequestId),
    enabled: !!returnRequestId,
    retry: noRetryOnRefusal,
  });
}

function useInvalidateMyOrders() {
  const queryClient = useQueryClient();
  return (orderId: string) => {
    void queryClient.invalidateQueries({ queryKey: ['me', 'order', orderId] });
    void queryClient.invalidateQueries({ queryKey: ['me', 'orders'] });
  };
}

export function useSubmitTransferReference(orderId: string) {
  const invalidate = useInvalidateMyOrders();
  return useMutation({
    mutationFn: (reference: string) => submitTransferReference(orderId, reference),
    onSuccess: () => invalidate(orderId),
  });
}

export function useCancelMyOrder(orderId: string) {
  const invalidate = useInvalidateMyOrders();
  return useMutation({
    mutationFn: () => cancelMyOrder(orderId),
    onSuccess: () => invalidate(orderId),
  });
}

/** Re-order puts the available lines back in the cart at today's prices — no order is created. */
export function useReorder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { orderId: string; idempotencyKey: string }) =>
      reorderOrder(input.orderId, input.idempotencyKey),
    retry: false,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: CART_QUERY_KEY }),
  });
}

export function useRequestMyReturn(orderId: string) {
  const invalidate = useInvalidateMyOrders();
  return useMutation({
    mutationFn: (lines: ReturnLineInput[]) => requestMyReturn(orderId, lines),
    onSuccess: () => invalidate(orderId),
  });
}

/** The public branch list (`GET /api/branches`) — phone and email for "contact the shop". */
export function usePublicBranches() {
  return useQuery({
    queryKey: ['public', 'branches'],
    queryFn: getPublicBranches,
    staleTime: 10 * 60_000,
  });
}
