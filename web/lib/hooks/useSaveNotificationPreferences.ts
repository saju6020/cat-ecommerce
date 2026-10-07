import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  changeNotificationPreferences,
  type NotificationPreferenceChange,
} from '@/lib/api/account';

/**
 * HO-NTF-002 §11.2 / §13 ("new hooks only") — saves the consent matrix AND the preferred channel in
 * ONE `PUT`, which is what makes it the same explicit Save (ADR-FE-008). HO-ACC-002's
 * `useChangeNotificationPreferences` (changes only) is left untouched.
 */
export interface SaveNotificationPreferencesInput {
  changes: NotificationPreferenceChange[];
  preferredChannel?: string;
}

export function useSaveNotificationPreferences() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ changes, preferredChannel }: SaveNotificationPreferencesInput) =>
      changeNotificationPreferences(changes, preferredChannel),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['account', 'notification-preferences'] }),
  });
}
