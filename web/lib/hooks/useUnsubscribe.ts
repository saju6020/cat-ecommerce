import { useMutation } from '@tanstack/react-query';
import { unsubscribeFromMarketing } from '@/lib/api/unsubscribe';

/** HO-NTF-002 §11.4, ADR-FE-161 (handover ADR-FE-021). A mutation, never a query: nothing is sent
 *  until the person clicks. */
export function useUnsubscribe() {
  return useMutation({ mutationFn: (token: string) => unsubscribeFromMarketing(token) });
}
