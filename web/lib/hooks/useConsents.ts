import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getCatConsents,
  getConsent,
  getConsentForm,
  recordConsent,
  withdrawConsent,
  type ConsentKind,
  type RecordConsentRequest,
} from '@/lib/api/consents';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';

/**
 * `lib/hooks/useConsents.ts` — HO-EMR-002 §8.3 (US-CLN-21). A consent holds a signed `signatureUrl`
 * or file URL, so `['consent', id]` has a 4-minute `staleTime` (URLs live 5).
 */

function noRetryOnRefusal(failureCount: number, error: unknown) {
  if (error instanceof ApiForbiddenError || error instanceof ApiNotFoundError) return false;
  return failureCount < 1;
}

export function useConsentForm(kind: ConsentKind) {
  return useQuery({
    queryKey: ['consent-form', kind],
    queryFn: () => getConsentForm(kind),
    staleTime: 0,
    retry: noRetryOnRefusal,
  });
}

export function useConsent(consentId: string) {
  return useQuery({
    queryKey: ['consent', consentId],
    queryFn: () => getConsent(consentId),
    enabled: !!consentId,
    staleTime: 4 * 60_000,
    retry: noRetryOnRefusal,
  });
}

export function useCatConsents(catId: string) {
  return useQuery({
    queryKey: ['cat', catId, 'consents'],
    queryFn: () => getCatConsents(catId),
    enabled: !!catId,
    retry: noRetryOnRefusal,
  });
}

export function useRecordConsent() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: RecordConsentRequest) => recordConsent(request),
    onSuccess: (_result, request) => {
      void queryClient.invalidateQueries({ queryKey: ['cat', request.catId, 'consents'] });
    },
  });
}

export function useWithdrawConsent(consentId: string, catId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (reason: string) => withdrawConsent(consentId, reason),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['consent', consentId] });
      void queryClient.invalidateQueries({ queryKey: ['cat', catId, 'consents'] });
    },
  });
}
