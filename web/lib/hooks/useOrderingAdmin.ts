'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import {
  createCourier,
  createDeliveryZone,
  createDiscountCode,
  getBankTransferAccount,
  getDeliveryZone,
  listCouriers,
  listDeliveryZones,
  listDiscountCodes,
  resolveDeliveryZone,
  setBankTransferAccount,
  setDeliveryZoneActivation,
  setDiscountCodeActivation,
  updateCourier,
  updateDeliveryZone,
  updateDiscountCode,
  type BankTransferAccountInput,
  type CourierInput,
  type DeliveryZoneInput,
  type DiscountCodeInput,
} from '@/lib/api/ordering-admin';

/**
 * `lib/hooks/useOrderingAdmin.ts` — HO-ORD-002 §8.3. Administration lists are per resource; each
 * mutation invalidates its resource (`['admin','delivery-zones']`, `['admin','couriers']`,
 * `['admin','discount-codes']`, `['admin','bank-transfer-account']`).
 */
function noRetryOnRefusal(failureCount: number, error: unknown) {
  if (error instanceof ApiForbiddenError || error instanceof ApiNotFoundError) return false;
  return failureCount < 1;
}

// ---- delivery zones --------------------------------------------------------------------------------

export function useDeliveryZones() {
  return useQuery({
    queryKey: ['admin', 'delivery-zones'],
    queryFn: listDeliveryZones,
    retry: noRetryOnRefusal,
  });
}

export function useDeliveryZone(zoneId: string) {
  return useQuery({
    queryKey: ['admin', 'delivery-zones', zoneId],
    queryFn: () => getDeliveryZone(zoneId),
    enabled: !!zoneId,
    retry: noRetryOnRefusal,
  });
}

export function useSaveDeliveryZone(zoneId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: DeliveryZoneInput) =>
      zoneId ? updateDeliveryZone(zoneId, input).then(() => ({ zoneId })) : createDeliveryZone(input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'delivery-zones'] }),
  });
}

export function useSetZoneActivation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { zoneId: string; isActive: boolean }) =>
      setDeliveryZoneActivation(input.zoneId, input.isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'delivery-zones'] }),
  });
}

/** The "Test an address" box — runs only when asked. */
export function useResolveZone() {
  return useMutation({
    mutationFn: (input: { district: string; postalCode?: string }) =>
      resolveDeliveryZone(input.district, input.postalCode),
  });
}

// ---- couriers --------------------------------------------------------------------------------------

export function useCouriers() {
  return useQuery({ queryKey: ['admin', 'couriers'], queryFn: listCouriers, retry: noRetryOnRefusal });
}

export function useSaveCourier(courierId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CourierInput) =>
      courierId ? updateCourier(courierId, input).then(() => ({ courierId })) : createCourier(input),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'couriers'] });
      void queryClient.invalidateQueries({ queryKey: ['staff', 'dispatch-couriers'] });
    },
  });
}

// ---- discount codes --------------------------------------------------------------------------------

export function useDiscountCodes() {
  return useQuery({
    queryKey: ['admin', 'discount-codes'],
    queryFn: listDiscountCodes,
    retry: noRetryOnRefusal,
  });
}

export function useSaveDiscountCode(codeId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: DiscountCodeInput) =>
      codeId
        ? updateDiscountCode(codeId, input).then(() => ({ discountCodeId: codeId }))
        : createDiscountCode(input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'discount-codes'] }),
  });
}

export function useSetDiscountActivation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { codeId: string; isActive: boolean }) =>
      setDiscountCodeActivation(input.codeId, input.isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'discount-codes'] }),
  });
}

// ---- bank-transfer account ---------------------------------------------------------------------------

export function useBankTransferAccount() {
  return useQuery({
    queryKey: ['admin', 'bank-transfer-account'],
    queryFn: getBankTransferAccount,
    retry: noRetryOnRefusal,
  });
}

export function useSaveBankTransferAccount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: BankTransferAccountInput) => setBankTransferAccount(input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'bank-transfer-account'] }),
  });
}
