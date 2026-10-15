import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getMyPrescription,
  listMyDietAuthorisations,
  listMyPrescriptions,
  requestRepeat,
  withdrawRepeat,
  type MyScope,
} from '@/lib/api/my-prescriptions';
import { noRetryOnRefusal } from './useInventoryQuery';

/** `lib/hooks/useMyPrescriptions.ts` — HO-PHM-002 §8.3: `['me', 'prescriptions', scope, catId]`, `['me', 'prescription', id]`. */
export function useMyPrescriptions(scope: MyScope, catId?: string) {
  return useQuery({
    queryKey: ['me', 'prescriptions', scope, catId ?? null],
    queryFn: () => listMyPrescriptions({ scope, catId }),
    retry: noRetryOnRefusal,
  });
}

export function useMyPrescription(id: string) {
  return useQuery({
    queryKey: ['me', 'prescription', id],
    queryFn: () => getMyPrescription(id),
    retry: noRetryOnRefusal,
  });
}

export function useMyDietAuthorisations(scope: MyScope, enabled = true) {
  return useQuery({
    queryKey: ['me', 'diet-authorisations', scope],
    queryFn: () => listMyDietAuthorisations({ scope }),
    enabled,
    retry: noRetryOnRefusal,
  });
}

function useInvalidateMine() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: ['me'] });
}

export function useRequestRepeat(id: string) {
  const invalidate = useInvalidateMine();
  return useMutation({
    mutationFn: (body: { preferredBranchId: string; note?: string | null }) =>
      requestRepeat(id, body),
    onSuccess: invalidate,
  });
}

export function useWithdrawRepeat(id: string) {
  const invalidate = useInvalidateMine();
  return useMutation({
    mutationFn: (requestId: string) => withdrawRepeat(id, requestId),
    onSuccess: invalidate,
  });
}
