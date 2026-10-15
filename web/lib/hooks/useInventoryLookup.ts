import { useMutation, useQueries } from '@tanstack/react-query';
import {
  createLocation,
  getLocations,
  getStorageProfile,
  lookupSkus,
  setLocationActivation,
  setStorageProfile,
  updateLocation,
  type CreateLocationRequest,
  type SetStorageProfileRequest,
} from '@/lib/api/inventory-lookup';
import { useBranchScope } from './useBranchScope';
import { noRetryOnRefusal, useInventoryQuery, useInvalidateInventory } from './useInventoryQuery';

/** HO-IMP-002 §8.2 — SKU lookup, locations, storage profiles. */

/** Type-ahead (≥ 2 characters; the server returns at most 20 rows). */
export function useSkuSearch(search: string) {
  const term = search.trim();
  return useInventoryQuery('skus', [term], () => lookupSkus({ search: term }), {
    enabled: term.length >= 2,
    staleTime: 30_000,
  });
}

/** Resolves SKU codes to full lookup rows (reorder hand-off, OQ-2) — one exact-code search each. */
export function useSkusByCode(codes: readonly string[]) {
  const { branchId, ready } = useBranchScope();
  return useQueries({
    queries: codes.map((code) => ({
      queryKey: ['inventory', 'skus', 'code', code, branchId],
      queryFn: async () =>
        (await lookupSkus({ search: code })).find((s) => s.skuCode === code) ?? null,
      enabled: ready,
      retry: noRetryOnRefusal,
      staleTime: 30_000,
    })),
  });
}

/** A barcode scan (HW-2): one server lookup. Resolves `null` when no SKU has the barcode. */
export function useBarcodeLookup() {
  return async (barcode: string) => (await lookupSkus({ barcode }))[0] ?? null;
}

export function useLocations() {
  return useInventoryQuery('locations', [], getLocations, { staleTime: 30_000 });
}

export function useCreateLocation() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: (request: CreateLocationRequest) => createLocation(request),
    onSuccess: () => invalidate('locations'),
  });
}

export function useUpdateLocation() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) => updateLocation(id, { name }),
    onSuccess: () => invalidate('locations'),
  });
}

export function useSetLocationActivation() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      setLocationActivation(id, isActive),
    onSuccess: () => invalidate('locations'),
  });
}

export function useStorageProfile(productId: string | null) {
  return useInventoryQuery(
    'storage-profile',
    [productId],
    () => getStorageProfile(productId as string),
    { enabled: !!productId },
  );
}

export function useSetStorageProfile() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: ({
      productId,
      request,
    }: {
      productId: string;
      request: SetStorageProfileRequest;
    }) => setStorageProfile(productId, request),
    onSuccess: () => invalidate('storage-profile', 'skus', 'stock'),
  });
}
