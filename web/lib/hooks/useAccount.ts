import { useQuery } from '@tanstack/react-query';
import {
  getCustomerProfile,
  getDeliveryAddresses,
  getNotificationPreferences,
} from '@/lib/api/account';

/** Read-side account-area queries (US-ACC-06) — HO-CAT-002 §8.2 rule 3. */

export function useCustomerProfile() {
  return useQuery({
    queryKey: ['account', 'profile'],
    queryFn: getCustomerProfile,
  });
}

export function useDeliveryAddresses() {
  return useQuery({
    queryKey: ['account', 'addresses'],
    queryFn: getDeliveryAddresses,
  });
}

export function useNotificationPreferences() {
  return useQuery({
    queryKey: ['account', 'notification-preferences'],
    queryFn: getNotificationPreferences,
  });
}
