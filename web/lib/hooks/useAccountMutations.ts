import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  updateCustomerProfile,
  addDeliveryAddress,
  updateDeliveryAddress,
  removeDeliveryAddress,
  setDefaultDeliveryAddress,
  changeNotificationPreferences,
  exportPersonalData,
  deleteAccount,
  type UpdateCustomerProfileRequest,
  type DeliveryAddressRequest,
  type NotificationPreferenceChange,
  type DeleteAccountRequest,
} from '@/lib/api/account';

/** Account-area mutations (US-ACC-06/13) — each invalidates its own resource (§8.4). */

export function useUpdateCustomerProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateCustomerProfileRequest) => updateCustomerProfile(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['account', 'profile'] }),
  });
}

function invalidateAddresses(queryClient: ReturnType<typeof useQueryClient>) {
  return queryClient.invalidateQueries({ queryKey: ['account', 'addresses'] });
}

export function useAddDeliveryAddress() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: DeliveryAddressRequest) => addDeliveryAddress(request),
    onSuccess: () => invalidateAddresses(queryClient),
  });
}

/**
 * ARCH gate HIGH 3 (first pass) — takes `addressId` per mutate call now, not baked into the hook
 * at creation time: `AddressList` (which owns "which address is currently being edited" as its
 * own local state) can no longer call this hook itself, so the page has to call it once and pass
 * the mutate function down — which only works if the id travels with each call, matching
 * `useRemoveDeliveryAddress`/`useSetDefaultDeliveryAddress` below.
 */
export function useUpdateDeliveryAddress() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ addressId, request }: { addressId: string; request: DeliveryAddressRequest }) =>
      updateDeliveryAddress(addressId, request),
    onSuccess: () => invalidateAddresses(queryClient),
  });
}

export function useRemoveDeliveryAddress() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (addressId: string) => removeDeliveryAddress(addressId),
    onSuccess: () => invalidateAddresses(queryClient),
  });
}

export function useSetDefaultDeliveryAddress() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (addressId: string) => setDefaultDeliveryAddress(addressId),
    onSuccess: () => invalidateAddresses(queryClient),
  });
}

export function useChangeNotificationPreferences() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (changes: NotificationPreferenceChange[]) => changeNotificationPreferences(changes),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['account', 'notification-preferences'] }),
  });
}

/** US-ACC-13. The exported JSON is never put into the query cache (§11.8) — a plain mutation
 *  whose result the caller turns straight into a Blob download, nothing else. */
export function useExportPersonalData() {
  return useMutation({
    mutationFn: () => exportPersonalData(),
  });
}

export function useDeleteAccount() {
  return useMutation({
    mutationFn: (request: DeleteAccountRequest) => deleteAccount(request),
  });
}
