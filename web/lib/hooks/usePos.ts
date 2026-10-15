'use client';

import { keepPreviousData, useMutation, useQuery } from '@tanstack/react-query';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { getCats } from '@/lib/api/cats';
import { findCustomers } from '@/lib/api/staff-customers';
import {
  completeCounterSale,
  getCounterQuote,
  getReceipt,
  lookupPosSku,
  searchPosSkus,
  type CounterSaleInput,
  type PosLineInput,
} from '@/lib/api/pos';
import { useBranchScope } from './useBranchScope';
import { useDebouncedValue } from './useDebouncedValue';

/**
 * `lib/hooks/usePos.ts` — HO-ORD-002 §8.3, ADR-FE-095. The point of sale. The counter quote is the
 * server's, debounced 300 ms; the basket itself is component state in `/staff/pos` and never storage.
 */
export const POS_QUOTE_DEBOUNCE_MS = 300;

function noRetryOnRefusal(failureCount: number, error: unknown) {
  if (error instanceof ApiForbiddenError || error instanceof ApiNotFoundError) return false;
  return failureCount < 1;
}

export function usePosBarcodeLookup() {
  return useMutation({ mutationFn: (barcode: string) => lookupPosSku(barcode), retry: false });
}

export function usePosSkuSearch(text: string) {
  const { branchId, ready } = useBranchScope();
  const settled = useDebouncedValue(text.trim(), 300);
  return useQuery({
    queryKey: ['pos', 'sku-search', branchId, settled],
    queryFn: () => searchPosSkus(settled),
    enabled: ready && settled.length >= 2,
    placeholderData: keepPreviousData,
    retry: noRetryOnRefusal,
  });
}

export function useCounterQuote(input: {
  customerId?: string;
  lines: PosLineInput[];
  discountCode?: string;
}) {
  const { branchId, ready } = useBranchScope();
  const serialized = JSON.stringify(input);
  const settled = useDebouncedValue(serialized, POS_QUOTE_DEBOUNCE_MS);
  const settledInput = JSON.parse(settled) as typeof input;
  const query = useQuery({
    queryKey: ['pos', 'quote', branchId, settledInput],
    queryFn: () => getCounterQuote(settledInput),
    enabled: ready && settledInput.lines.length > 0,
    staleTime: 0,
    refetchOnWindowFocus: false,
    placeholderData: keepPreviousData,
    retry: noRetryOnRefusal,
  });
  return { ...query, isSettling: serialized !== settled };
}

export function useCompleteCounterSale() {
  return useMutation({
    mutationFn: (input: { sale: CounterSaleInput; idempotencyKey: string }) =>
      completeCounterSale(input.sale, input.idempotencyKey),
    retry: false,
  });
}

export function useReceipt(orderId: string) {
  const { branchId, ready } = useBranchScope();
  return useQuery({
    queryKey: ['pos', 'receipt', branchId, orderId],
    queryFn: () => getReceipt(orderId),
    enabled: ready && !!orderId,
    retry: noRetryOnRefusal,
  });
}

/** "Find an existing customer" by mobile number — runs only when the cashier asks. */
export function usePosCustomerLookup() {
  return useMutation({ mutationFn: (mobile: string) => findCustomers({ mobile: mobile.trim() }), retry: false });
}

/** The attached customer's cats, for a prescription line's cat picker. Off until a customer is attached. */
export function usePosCustomerCats(customerId: string | null) {
  return useQuery({
    queryKey: ['pos', 'customer-cats', customerId],
    queryFn: () => getCats({ ownerId: customerId ?? undefined, pageSize: 50 }),
    enabled: !!customerId,
    retry: noRetryOnRefusal,
  });
}
