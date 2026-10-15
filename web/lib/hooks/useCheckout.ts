'use client';

import { useCallback, useMemo } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiForbiddenError, ApiNotFoundError, ApiRateLimitedError } from '@/lib/api/client';
import {
  getCheckoutQuote,
  placeOrder,
  type CheckoutChoices,
  type FulfilmentChoice,
  type PaymentMethodChoice,
  type PlaceOrderRequest,
  type ServiceLevel,
} from '@/lib/api/checkout';
import { CART_QUERY_KEY } from './useCart';
import { useDebouncedValue } from './useDebouncedValue';

/**
 * `lib/hooks/useCheckout.ts` — HO-ORD-002 §8.3, ADR-FE-090/091/097.
 *
 * - **Choices live in the URL** (`useCheckoutUrlState`): reload-safe and back-button-safe, and a
 *   validation error never loses them (NFR-USA-5). Nothing here decides what is allowed.
 * - **The quote is the single source of truth**: `['checkout','quote', params]`, `staleTime: 0`,
 *   debounced 300 ms on a change of choices (NFR-PRF-1), `keepPreviousData` so the summary does not
 *   flash. The server limits quotes to 10/min per IP, so there is no polling and no refetch on focus.
 * - **Placement is single-shot**: no retry, the key is owned by the caller (`useIdempotencyKey`).
 */
export const QUOTE_DEBOUNCE_MS = 300;

export const CHECKOUT_STEPS = ['fulfilment', 'slot', 'payment', 'review'] as const;
export type CheckoutStep = (typeof CHECKOUT_STEPS)[number];

export interface CheckoutUrlState extends CheckoutChoices {
  step: CheckoutStep;
  recipientName?: string;
  recipientPhone?: string;
}

const FULFILMENTS: FulfilmentChoice[] = ['Delivery', 'Pickup'];
const PAYMENTS: PaymentMethodChoice[] = ['CashOnDelivery', 'BankTransfer', 'PayOnCollection'];
const LEVELS: ServiceLevel[] = ['Standard', 'Express'];

function parseState(params: URLSearchParams): CheckoutUrlState {
  const fulfilment = params.get('fulfilment');
  const payment = params.get('paymentMethod');
  const level = params.get('serviceLevel');
  const step = params.get('step');
  return {
    step: CHECKOUT_STEPS.includes(step as CheckoutStep) ? (step as CheckoutStep) : 'fulfilment',
    fulfilment: FULFILMENTS.includes(fulfilment as FulfilmentChoice)
      ? (fulfilment as FulfilmentChoice)
      : 'Delivery',
    addressId: params.get('addressId') ?? undefined,
    serviceLevel: LEVELS.includes(level as ServiceLevel) ? (level as ServiceLevel) : undefined,
    slotDate: params.get('slotDate') ?? undefined,
    slotWindowStart: params.get('slotWindowStart') ?? undefined,
    pickupBranchId: params.get('pickupBranchId') ?? undefined,
    paymentMethod: PAYMENTS.includes(payment as PaymentMethodChoice)
      ? (payment as PaymentMethodChoice)
      : undefined,
    discountCode: params.get('discountCode') ?? undefined,
    recipientName: params.get('recipientName') ?? undefined,
    recipientPhone: params.get('recipientPhone') ?? undefined,
  };
}

/** The checkout's choices, read from and written to the URL's search params. */
export function useCheckoutUrlState(): {
  state: CheckoutUrlState;
  update: (patch: Partial<Record<keyof CheckoutUrlState, string | undefined>>) => void;
} {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const query = searchParams.toString();
  const state = useMemo(() => parseState(new URLSearchParams(query)), [query]);

  const update = useCallback(
    (patch: Partial<Record<keyof CheckoutUrlState, string | undefined>>) => {
      const next = new URLSearchParams(window.location.search);
      for (const [key, value] of Object.entries(patch)) {
        if (value === undefined || value === '') next.delete(key);
        else next.set(key, value);
      }
      const text = next.toString();
      router.replace(text ? `${pathname}?${text}` : pathname, { scroll: false });
    },
    [pathname, router],
  );

  return { state, update };
}

function noRetryOnRefusal(failureCount: number, error: unknown) {
  if (
    error instanceof ApiForbiddenError ||
    error instanceof ApiNotFoundError ||
    error instanceof ApiRateLimitedError
  ) {
    return false;
  }
  return failureCount < 1;
}

export function useCheckoutQuote(choices: CheckoutChoices, enabled = true) {
  const serialized = JSON.stringify(choices);
  const settled = useDebouncedValue(serialized, QUOTE_DEBOUNCE_MS);
  const settledChoices = useMemo(() => JSON.parse(settled) as CheckoutChoices, [settled]);
  const query = useQuery({
    queryKey: ['checkout', 'quote', settledChoices],
    queryFn: () => getCheckoutQuote(settledChoices),
    enabled,
    staleTime: 0,
    refetchOnWindowFocus: false,
    placeholderData: keepPreviousData,
    retry: noRetryOnRefusal,
  });
  return { ...query, isSettling: serialized !== settled };
}

export function usePlaceOrder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { request: PlaceOrderRequest; idempotencyKey: string }) =>
      placeOrder(input.request, input.idempotencyKey),
    retry: false,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: CART_QUERY_KEY });
      void queryClient.invalidateQueries({ queryKey: ['me', 'orders'] });
    },
  });
}
